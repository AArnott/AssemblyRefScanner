// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace AssemblyRefScanner;

internal class CustomAttributeTypeProvider : ICustomAttributeTypeProvider<Type>
{
    public Type GetPrimitiveType(PrimitiveTypeCode typeCode)
    {
        return typeCode switch
        {
            PrimitiveTypeCode.String => typeof(string),
            PrimitiveTypeCode.Boolean => typeof(bool),
            PrimitiveTypeCode.Byte => typeof(byte),
            PrimitiveTypeCode.Char => typeof(char),
            PrimitiveTypeCode.Single => typeof(float),
            PrimitiveTypeCode.Double => typeof(double),
            _ => throw new NotImplementedException(),
        };
    }

    public Type GetSystemType() => typeof(Type);

    public Type GetSZArrayType(Type elementType)
    {
        throw new NotImplementedException();
    }

    public Type GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind)
    {
        throw new NotImplementedException();
    }

    public Type GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind)
    {
        throw new NotImplementedException();
    }

    public Type GetTypeFromSerializedName(string name)
    {
        throw new NotImplementedException();
    }

    public PrimitiveTypeCode GetUnderlyingEnumType(Type type)
    {
        throw new NotImplementedException();
    }

    public bool IsSystemType(Type type) => type == typeof(Type);
}
