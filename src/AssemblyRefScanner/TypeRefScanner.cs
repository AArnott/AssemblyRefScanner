// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace AssemblyRefScanner;

internal class TypeRefScanner : ScannerBase
{
    internal required string Path { get; init; }

    internal required string? DeclaringAssembly { get; init; }

    internal required string? Namespace { get; init; }

    internal required string TypeName { get; init; }

    internal async Task<int> Execute(CancellationToken cancellationToken)
    {
        var scanner = this.CreateProcessAssembliesBlock(
            mdReader =>
            {
                if (GetBreakingChangedTypeReference(mdReader, this.DeclaringAssembly, this.Namespace, this.TypeName) is TypeReferenceHandle interestingTypeHandle)
                {
                    return true;
                }

                return false;
            },
            cancellationToken);
        var report = this.CreateReportBlock(
            scanner,
            (assemblyPath, result) =>
            {
                if (result)
                {
                    Console.WriteLine(TrimBasePath(assemblyPath, this.Path));
                }
            },
            cancellationToken);
        return await this.Scan(this.Path, scanner, report, cancellationToken);
    }

    private static TypeReferenceHandle? GetBreakingChangedTypeReference(MetadataReader mdReader, string? declaringAssembly, string? typeNamespace, string typeName)
    {
        if (declaringAssembly is not null && !HasAssemblyReference(mdReader, declaringAssembly))
        {
            return null;
        }

        foreach (TypeReferenceHandle typeRefHandle in mdReader.TypeReferences)
        {
            TypeReference typeRef = mdReader.GetTypeReference(typeRefHandle);
            if (mdReader.StringComparer.Equals(typeRef.Name, typeName) &&
                (typeNamespace is null || mdReader.StringComparer.Equals(typeRef.Namespace, typeNamespace)))
            {
                return typeRefHandle;
            }
        }

        return null;
    }
}
