// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable SA1402 // File may only contain a single type
#pragma warning disable SA1649 // File name should match first type name

namespace AssemblyRefScanner;

internal struct GenericContext
{
    internal static readonly GenericContext Instance = default(GenericContext);
}

internal class SignatureTypeProvider : ISignatureTypeProvider<TypeHandleInfo, GenericContext>
{
    internal static readonly SignatureTypeProvider Instance = new();

    private SignatureTypeProvider()
    {
    }

    public TypeHandleInfo GetArrayType(TypeHandleInfo elementType, ArrayShape shape) => new ArrayTypeHandleInfo(elementType, shape);

    public TypeHandleInfo GetByReferenceType(TypeHandleInfo elementType) => new ByRefTypeHandleInfo(elementType);

    public TypeHandleInfo GetFunctionPointerType(MethodSignature<TypeHandleInfo> signature) => new FunctionPointerHandleInfo(signature);

    public TypeHandleInfo GetGenericInstantiation(TypeHandleInfo genericType, ImmutableArray<TypeHandleInfo> typeArguments) => new GenericTypeHandleInfo((NamedTypeHandleInfo)genericType, typeArguments);

    public TypeHandleInfo GetGenericMethodParameter(GenericContext genericContext, int index) => new GenericMethodParameter(index);

    public TypeHandleInfo GetGenericTypeParameter(GenericContext genericContext, int index) => new GenericTypeParameter(index);

    public TypeHandleInfo GetModifiedType(TypeHandleInfo modifier, TypeHandleInfo unmodifiedType, bool isRequired) => unmodifiedType;

    public TypeHandleInfo GetPinnedType(TypeHandleInfo elementType) => throw new NotImplementedException();

    public TypeHandleInfo GetPointerType(TypeHandleInfo elementType) => new PointerTypeHandleInfo(elementType);

    public TypeHandleInfo GetPrimitiveType(PrimitiveTypeCode typeCode) => new PrimitiveTypeHandleInfo(typeCode);

    public TypeHandleInfo GetSZArrayType(TypeHandleInfo elementType) => new ArrayTypeHandleInfo(elementType);

    public TypeHandleInfo GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind) => new DefinitionTypeHandleInfo(reader, handle);

    public TypeHandleInfo GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind) => new ReferenceTypeHandleInfo(reader, handle);

    public TypeHandleInfo GetTypeFromSpecification(MetadataReader reader, GenericContext genericContext, TypeSpecificationHandle handle, byte rawTypeKind) => reader.GetTypeSpecification(handle).DecodeSignature(SignatureTypeProvider.Instance, GenericContext.Instance);
}

internal abstract class TypeHandleInfo
{
}

internal abstract class NamedTypeHandleInfo : TypeHandleInfo
{
    /// <summary>
    /// Gets the full namespace of the type.
    /// </summary>
    internal abstract ReadOnlyMemory<char> Namespace { get; }

    /// <summary>
    /// Gets the type name (without any arity suffix).
    /// </summary>
    internal abstract ReadOnlyMemory<char> Name { get; }

    /// <summary>
    /// Gets the type that nests this one.
    /// </summary>
    internal abstract TypeHandleInfo? NestingType { get; }
}

internal class DefinitionTypeHandleInfo(MetadataReader reader, TypeDefinitionHandle handle) : NamedTypeHandleInfo
{
    private ReadOnlyMemory<char>? @namespace;
    private ReadOnlyMemory<char>? name;
    private NamedTypeHandleInfo? nestingType;

    internal override ReadOnlyMemory<char> Namespace => this.@namespace ??= reader.GetString(reader.GetTypeDefinition(handle).Namespace).AsMemory();

    internal override ReadOnlyMemory<char> Name => this.name ??= reader.GetString(reader.GetTypeDefinition(handle).Name).AsMemory();

#if NET
    internal override NamedTypeHandleInfo? NestingType
#else
    internal override TypeHandleInfo? NestingType
#endif
    {
        get
        {
            if (this.nestingType is null && reader.GetTypeDefinition(handle).GetDeclaringType() is { IsNil: false } nestingTypeHandle)
            {
                this.nestingType = new DefinitionTypeHandleInfo(reader, nestingTypeHandle);
            }

            return this.nestingType;
        }
    }
}

internal class ReferenceTypeHandleInfo(MetadataReader reader, TypeReferenceHandle handle) : NamedTypeHandleInfo
{
    private ReadOnlyMemory<char>? @namespace;
    private ReadOnlyMemory<char>? name;
    private TypeHandleInfo? nestingType;

    internal override ReadOnlyMemory<char> Namespace => this.@namespace ??= reader.GetString(reader.GetTypeReference(handle).Namespace).AsMemory();

    internal override ReadOnlyMemory<char> Name => this.name ??= reader.GetString(reader.GetTypeReference(handle).Name).AsMemory();

    internal override TypeHandleInfo? NestingType
    {
        get
        {
            if (this.nestingType is null && reader.GetTypeReference(handle) is { ResolutionScope: { IsNil: false, Kind: HandleKind.TypeReference or HandleKind.TypeDefinition } scope })
            {
                this.nestingType = new ReferenceTypeHandleInfo(reader, (TypeReferenceHandle)scope);
            }

            return this.nestingType;
        }
    }
}

internal class PrimitiveTypeHandleInfo(PrimitiveTypeCode typeCode) : NamedTypeHandleInfo
{
    internal override ReadOnlyMemory<char> Namespace => "System".AsMemory();

    internal override ReadOnlyMemory<char> Name => typeCode switch
    {
        PrimitiveTypeCode.Int16 => "Int16".AsMemory(),
        PrimitiveTypeCode.Int32 => "Int32".AsMemory(),
        PrimitiveTypeCode.Int64 => "Int64".AsMemory(),
        PrimitiveTypeCode.UInt16 => "UInt16".AsMemory(),
        PrimitiveTypeCode.UInt32 => "UInt32".AsMemory(),
        PrimitiveTypeCode.UInt64 => "UInt64".AsMemory(),
        PrimitiveTypeCode.Single => "Single".AsMemory(),
        PrimitiveTypeCode.Double => "Double".AsMemory(),
        PrimitiveTypeCode.Boolean => "Boolean".AsMemory(),
        PrimitiveTypeCode.Char => "Char".AsMemory(),
        PrimitiveTypeCode.Byte => "Byte".AsMemory(),
        PrimitiveTypeCode.SByte => "SByte".AsMemory(),
        PrimitiveTypeCode.IntPtr => "IntPtr".AsMemory(),
        PrimitiveTypeCode.UIntPtr => "UIntPtr".AsMemory(),
        PrimitiveTypeCode.Object => "Object".AsMemory(),
        PrimitiveTypeCode.String => "String".AsMemory(),
        PrimitiveTypeCode.Void => "Void".AsMemory(),
        PrimitiveTypeCode.TypedReference => "TypedReference".AsMemory(),
        _ => throw new NotImplementedException($"{typeCode}"),
    };

    internal override TypeHandleInfo? NestingType => null;
}

internal class PointerTypeHandleInfo(TypeHandleInfo elementType) : TypeHandleInfo
{
    internal TypeHandleInfo ElementType => elementType;
}

internal class ByRefTypeHandleInfo(TypeHandleInfo elementType) : TypeHandleInfo
{
    internal TypeHandleInfo ElementType => elementType;
}

internal class ArrayTypeHandleInfo(TypeHandleInfo elementType, ArrayShape? shape = null) : TypeHandleInfo
{
    internal TypeHandleInfo ElementType => elementType;

    internal ArrayShape? Shape => shape;
}

internal class GenericTypeHandleInfo(NamedTypeHandleInfo genericType, ImmutableArray<TypeHandleInfo> typeArguments) : TypeHandleInfo
{
    internal NamedTypeHandleInfo GenericType => genericType;

    internal ImmutableArray<TypeHandleInfo> TypeArguments => typeArguments;
}

internal class GenericMethodParameter(int index) : TypeHandleInfo
{
    internal int Position => index;
}

internal class GenericTypeParameter(int index) : TypeHandleInfo
{
    internal int Position => index;
}

internal class FunctionPointerHandleInfo(MethodSignature<TypeHandleInfo> signature) : TypeHandleInfo
{
    internal MethodSignature<TypeHandleInfo> Signature => signature;
}
