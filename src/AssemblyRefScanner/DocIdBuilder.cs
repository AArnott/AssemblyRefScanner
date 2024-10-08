// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;

namespace AssemblyRefScanner;

/// <summary>
/// Builds a <see href="https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/language-specification/documentation-comments#d42-id-string-format">doc comment ID</see>
/// for a given type or member reference.
/// </summary>
public class DocIdBuilder(MetadataReader mdReader)
{
    private const string EventAddPrefix = "add_";
    private const string EventRemovePrefix = "remove_";
    private const string PropertyGetPrefix = "get_";
    private const string PropertySetPrefix = "set_";

    private static readonly ThreadLocal<StringBuilder> Builder = new(() => new());

    /// <summary>
    /// Constructs a <see href="https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/language-specification/documentation-comments#d42-id-string-format">DocID</see> for the given entity handle.
    /// </summary>
    /// <param name="entityHandle">The handle to the entity to construct a DocID for.</param>
    /// <returns>The DocID.</returns>
    /// <exception cref="NotSupportedException">Thrown when <paramref name="entityHandle"/> refers to an entity for which no DocID can be constructed.</exception>
    /// <remarks>
    /// <para>
    /// DocIDs can be constructed for the following entity types:
    /// <list type="bullet">
    /// <item><see cref="HandleKind.TypeDefinition"/></item>
    /// <item><see cref="HandleKind.EventDefinition"/></item>
    /// <item><see cref="HandleKind.FieldDefinition"/></item>
    /// <item><see cref="HandleKind.MethodDefinition"/></item>
    /// <item><see cref="HandleKind.PropertyDefinition"/></item>
    /// <item><see cref="HandleKind.TypeReference"/></item>
    /// <item><see cref="HandleKind.MemberReference"/></item>
    /// </list>
    /// </para>
    /// </remarks>
    public string GetDocumentationCommentId(EntityHandle entityHandle)
    {
        StringBuilder builder = Builder.Value!;
        builder.Clear();

        switch (entityHandle.Kind)
        {
            case HandleKind.TypeDefinition:
                builder.Append("T:");
                this.VisitType(new DefinitionTypeHandleInfo(mdReader, (TypeDefinitionHandle)entityHandle), builder);
                break;
            case HandleKind.EventDefinition:
                builder.Append("E:");
                this.VisitEvent((EventDefinitionHandle)entityHandle, builder);
                break;
            case HandleKind.FieldDefinition:
                builder.Append("F:");
                this.VisitField((FieldDefinitionHandle)entityHandle, builder);
                break;
            case HandleKind.MethodDefinition:
                builder.Append("M:");
                this.VisitMethod((MethodDefinitionHandle)entityHandle, builder);
                break;
            case HandleKind.PropertyDefinition:
                builder.Append("P:");
                this.VisitProperty((PropertyDefinitionHandle)entityHandle, builder);
                break;
            case HandleKind.TypeReference:
                builder.Append("T:");
                this.VisitType((TypeReferenceHandle)entityHandle, builder);
                break;
            case HandleKind.MemberReference:
                MemberReference memberReference = mdReader.GetMemberReference((MemberReferenceHandle)entityHandle);
                switch (memberReference.GetKind())
                {
                    case MemberReferenceKind.Field:
                        builder.Append("F:");
                        this.VisitField(memberReference, builder);
                        break;
                    case MemberReferenceKind.Method when this.IsProperty(memberReference):
                        builder.Append("P:");
                        this.VisitProperty(memberReference, builder, fromAccessorMethod: true);
                        break;
                    case MemberReferenceKind.Method when this.IsEvent(memberReference):
                        builder.Append("E:");
                        this.VisitEvent(memberReference, builder);
                        break;
                    case MemberReferenceKind.Method:
                        builder.Append("M:");
                        this.VisitMethod(memberReference, builder);
                        break;
                    default:
                        throw new NotSupportedException($"Unrecognized member reference kind: {memberReference.GetKind()}");
                }

                break;
            default:
                throw new NotSupportedException($"Unsupported entity kind: {entityHandle.Kind}.");
        }

        return builder.ToString();
    }

    private void VisitType(TypeReferenceHandle typeRefHandle, StringBuilder builder)
    {
        TypeReference typeReference = mdReader.GetTypeReference(typeRefHandle);
        if (typeReference.ResolutionScope.Kind == HandleKind.TypeReference)
        {
            this.VisitType((TypeReferenceHandle)typeReference.ResolutionScope, builder);
            builder.Append('.');
        }
        else if (mdReader.GetString(typeReference.Namespace) is { Length: > 0 } ns)
        {
            builder.Append(ns);
            builder.Append('.');
        }

        builder.Append(mdReader.GetString(typeReference.Name));
    }

    private void VisitType(TypeSpecificationHandle typeSpecHandle, StringBuilder builder)
        => this.VisitType(mdReader.GetTypeSpecification(typeSpecHandle).DecodeSignature(SignatureTypeProvider.Instance, GenericContext.Instance), builder);

    private void VisitType(TypeDefinitionHandle typeDefHandle, StringBuilder builder)
        => this.VisitType(new DefinitionTypeHandleInfo(mdReader, typeDefHandle), builder);

    private void VisitType(TypeHandleInfo typeHandle, StringBuilder builder)
    {
        switch (typeHandle)
        {
            case ArrayTypeHandleInfo arrayType:
                this.VisitType(arrayType.ElementType, builder);
                builder.Append('[');
                if (arrayType.Shape is { } shape)
                {
                    for (int i = 0; i < shape.Rank; i++)
                    {
                        if (i > 0)
                        {
                            builder.Append(',');
                        }

                        if (shape.LowerBounds.Length > i || shape.Sizes.Length > i)
                        {
                            if (shape.LowerBounds.Length > i)
                            {
                                builder.Append(shape.LowerBounds[i]);
                            }

                            builder.Append(':');
                            if (shape.Sizes.Length > i)
                            {
                                builder.Append(shape.Sizes[i]);
                            }
                        }
                    }
                }

                builder.Append(']');

                break;
            case ByRefTypeHandleInfo { ElementType: { } elementType }:
                this.VisitType(elementType, builder);
                builder.Append('@');
                break;
            case PointerTypeHandleInfo { ElementType: { } elementType }:
                this.VisitType(elementType, builder);
                builder.Append('*');
                break;
            case GenericTypeParameter genericTypeParameter:
                builder.Append('`');
                builder.Append(genericTypeParameter.Position);
                break;
            case GenericMethodParameter genericMethodParameter:
                builder.Append("``");
                builder.Append(genericMethodParameter.Position);
                break;
            case GenericTypeHandleInfo genericInstanceType:
                if (genericInstanceType.GenericType.Namespace is { Length: > 0 } ns)
                {
                    builder.Append(ns);
                    builder.Append('.');
                }

                builder.Append(genericInstanceType.GenericType.NameWithoutArity);
                builder.Append('{');

                foreach (var genericArgument in genericInstanceType.TypeArguments)
                {
                    this.VisitType(genericArgument, builder);
                    builder.Append(',');
                }

                if (builder[^1] == ',')
                {
                    builder.Length--;
                }

                builder.Append('}');
                break;
            case NamedTypeHandleInfo namedType:
                if (namedType.NestingType is not null)
                {
                    this.VisitType(namedType.NestingType, builder);
                    builder.Append('.');
                }
                else if (namedType.Namespace is { Length: > 0 })
                {
                    builder.Append(namedType.Namespace);
                    builder.Append('.');
                }

                builder.Append(namedType.Name);
                break;
            default:
                builder.Append($"!:Unsupported type handle: {typeHandle.GetType()}");
                break;
        }
    }

    private void VisitParentType(MemberReference memberReference, StringBuilder builder)
    {
        switch (memberReference.Parent.Kind)
        {
            case HandleKind.TypeReference:
                this.VisitType((TypeReferenceHandle)memberReference.Parent, builder);
                break;
            case HandleKind.TypeSpecification:
                this.VisitType((TypeSpecificationHandle)memberReference.Parent, builder);
                break;
            case HandleKind.TypeDefinition:
                this.VisitType((TypeDefinitionHandle)memberReference.Parent, builder);
                break;
            default:
                throw new NotSupportedException($"Parent type is not supported: {memberReference.Parent.Kind}");
        }
    }

    private void VisitParentType(EntityHandle typeHandle, StringBuilder builder)
    {
        switch (typeHandle.Kind)
        {
            case HandleKind.TypeDefinition:
                this.VisitType(new DefinitionTypeHandleInfo(mdReader, (TypeDefinitionHandle)typeHandle), builder);
                break;
            case HandleKind.TypeReference:
                this.VisitType((TypeReferenceHandle)typeHandle, builder);
                break;
            case HandleKind.TypeSpecification:
                this.VisitType((TypeSpecificationHandle)typeHandle, builder);
                break;
            default:
                throw new NotSupportedException($"{typeHandle.Kind} is not supported.");
        }
    }

    private void VisitMethodHelper(string name, MethodSignature<TypeHandleInfo> signature, StringBuilder builder)
    {
        builder.Append('.');
        int nameStartIndex = builder.Length;
        builder.Append(name);
        builder.Replace('.', '#', nameStartIndex, name.Length);

        if (signature.GenericParameterCount > 0)
        {
            builder.Append("``");
            builder.Append(signature.GenericParameterCount);
        }

        if (signature.ParameterTypes.Length == 0)
        {
            return;
        }

        builder.Append('(');

        foreach (TypeHandleInfo parameterType in signature.ParameterTypes)
        {
            this.VisitType(parameterType, builder);

            if (builder[^1] == '&')
            {
                builder.Length--;
                builder.Append('@');
            }

            builder.Append(',');
        }

        if (builder[^1] == ',')
        {
            builder.Length--;
        }

        builder.Append(')');
    }

    private void VisitMethod(MemberReference methodReference, StringBuilder builder)
    {
        this.VisitParentType(methodReference, builder);

        string name = mdReader.GetString(methodReference.Name);
        MethodSignature<TypeHandleInfo> signature = methodReference.DecodeMethodSignature(SignatureTypeProvider.Instance, GenericContext.Instance);
        this.VisitMethodHelper(name, signature, builder);
    }

    private void VisitMethod(MethodDefinitionHandle handle, StringBuilder builder)
    {
        MethodDefinition methodDef = mdReader.GetMethodDefinition(handle);
        this.VisitParentType(methodDef.GetDeclaringType(), builder);

        MethodSignature<TypeHandleInfo> signature = methodDef.DecodeSignature(SignatureTypeProvider.Instance, GenericContext.Instance);
        this.VisitMethodHelper(mdReader.GetString(methodDef.Name), signature, builder);
    }

#if NETFRAMEWORK
    private void VisitPropertyHelper(string name, MethodSignature<TypeHandleInfo> signature, StringBuilder builder) => this.VisitPropertyHelper(name.AsSpan(), signature, builder);
#endif

    private void VisitPropertyHelper(ReadOnlySpan<char> name, MethodSignature<TypeHandleInfo> signature, StringBuilder builder)
    {
        builder.Append('.');
        builder.Append(name);

        if (signature.ParameterTypes.Length == 0)
        {
            return;
        }

        builder.Append('(');

        foreach (TypeHandleInfo parameterType in signature.ParameterTypes)
        {
            this.VisitType(parameterType, builder);
            builder.Append(',');
        }

        if (builder[^1] == ',')
        {
            builder.Length--;
        }

        builder.Append(')');
    }

    private void VisitProperty(MemberReference propertyReference, StringBuilder builder, bool fromAccessorMethod = false)
    {
        this.VisitParentType(propertyReference, builder);

        string name = mdReader.GetString(propertyReference.Name);
        MethodSignature<TypeHandleInfo> signature = propertyReference.DecodeMethodSignature(SignatureTypeProvider.Instance, GenericContext.Instance);
        this.VisitPropertyHelper(fromAccessorMethod ? name.AsSpan(4) : name.AsSpan(), signature, builder);
    }

    private void VisitProperty(PropertyDefinitionHandle handle, StringBuilder builder)
    {
        PropertyDefinition propertyDef = mdReader.GetPropertyDefinition(handle);

        PropertyAccessors accessors = propertyDef.GetAccessors();
        MethodDefinitionHandle someAccessor =
            accessors.Getter.IsNil == false ? accessors.Getter :
            accessors.Setter.IsNil == false ? accessors.Setter :
            accessors.Others.FirstOrDefault();
        if (someAccessor.IsNil)
        {
            throw new NotSupportedException("Property with no accessors.");
        }

        MethodDefinition accessorMethodDef = mdReader.GetMethodDefinition(someAccessor);
        this.VisitParentType(accessorMethodDef.GetDeclaringType(), builder);

        MethodSignature<TypeHandleInfo> signature = propertyDef.DecodeSignature(SignatureTypeProvider.Instance, GenericContext.Instance);
        this.VisitPropertyHelper(mdReader.GetString(propertyDef.Name), signature, builder);
    }

    private void VisitField(MemberReference fieldReference, StringBuilder builder)
    {
        this.VisitParentType(fieldReference, builder);

        builder.Append('.');
        builder.Append(fieldReference.Name);
    }

    private void VisitField(FieldDefinitionHandle handle, StringBuilder builder)
    {
        FieldDefinition fieldDefinition = mdReader.GetFieldDefinition(handle);
        this.VisitParentType(fieldDefinition.GetDeclaringType(), builder);

        builder.Append('.');
        builder.Append(mdReader.GetString(fieldDefinition.Name));
    }

    private void VisitEvent(MemberReference eventReference, StringBuilder builder)
    {
        this.VisitParentType(eventReference, builder);

        builder.Append('.');
        string name = mdReader.GetString(eventReference.Name);
        builder.Append(
            name.StartsWith(EventAddPrefix) ? name.AsSpan(EventAddPrefix.Length) :
            name.StartsWith(EventRemovePrefix) ? name.AsSpan(EventRemovePrefix.Length) :
            throw new NotImplementedException());
    }

    private void VisitEvent(EventDefinitionHandle handle, StringBuilder builder)
    {
        EventDefinition eventDefinition = mdReader.GetEventDefinition(handle);
        this.VisitParentType(eventDefinition.Type, builder);

        builder.Append(".");
        builder.Append(mdReader.GetString(eventDefinition.Name));
    }

    private bool IsProperty(MemberReference memberReference)
    {
        // Falling back on naming conventions is our only resort when the reference is to an API outside
        // the assembly accessible to the MetadataReader.
        // When it is to an API within the same assembly, we can resolve the reference to the actual API definition
        // to find out what it is.
        // As currently implemented though, we don't bother with the resolving the in-assembly references.
        return mdReader.StringComparer.StartsWith(memberReference.Name, PropertyGetPrefix) || mdReader.StringComparer.StartsWith(memberReference.Name, PropertySetPrefix);
    }

    private bool IsEvent(MemberReference memberReference)
    {
        // Falling back on naming conventions is our only resort when the reference is to an API outside
        // the assembly accessible to the MetadataReader.
        // When it is to an API within the same assembly, we can resolve the reference to the actual API definition
        // to find out what it is.
        // As currently implemented though, we don't bother with the resolving the in-assembly references.
        return mdReader.StringComparer.StartsWith(memberReference.Name, EventAddPrefix) || mdReader.StringComparer.StartsWith(memberReference.Name, EventRemovePrefix);
    }
}
