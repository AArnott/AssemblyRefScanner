// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace AssemblyRefScanner;

internal class ApiRefScanner : ScannerBase
{
    internal required string Path { get; init; }

    internal required string? DeclaringAssembly { get; init; }

    internal required string[] DocIds { get; init; }

    internal async Task<int> Execute(CancellationToken cancellationToken)
    {
        DocId.Descriptor[] descriptors = [.. this.DocIds.Select(DocId.Parse)];
        var scanner = this.CreateProcessAssembliesBlock(
            mdReader =>
            {
                // Skip assemblies that don't reference the declaring assembly.
                if (this.DeclaringAssembly is not null && !HasAssemblyReference(mdReader, this.DeclaringAssembly))
                {
                    return false;
                }

                return descriptors.Any(d => HasReferenceTo(mdReader, d));
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

    private static bool HasReferenceTo(MetadataReader referencingAssemblyReader, DocId.Descriptor api)
    {
        switch (api.Kind)
        {
            case DocId.ApiKind.Type:
                foreach (TypeReferenceHandle handle in referencingAssemblyReader.TypeReferences)
                {
                    if (api.IsMatch(handle, referencingAssemblyReader))
                    {
                        return true;
                    }
                }

                break;
            case DocId.ApiKind.Method:
            case DocId.ApiKind.Property:
            case DocId.ApiKind.Field:
            case DocId.ApiKind.Event:
                foreach (MemberReferenceHandle handle in referencingAssemblyReader.MemberReferences)
                {
                    if (api.IsMatch(handle, referencingAssemblyReader))
                    {
                        return true;
                    }
                }

                break;
        }

        return false;
    }
}
