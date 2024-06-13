// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace AssemblyRefScanner;

internal class ApiRefScanner : ScannerBase
{
    internal async Task Execute(string path, string? declaringAssembly, string[] docIds, InvocationContext invocationContext, CancellationToken cancellationToken)
    {
        DocId.Descriptor[] descriptors = [.. docIds.Select(DocId.Parse)];
        var scanner = this.CreateProcessAssembliesBlock(
            mdReader =>
            {
                // Skip assemblies that don't reference the declaring assembly.
                if (declaringAssembly is not null && !HasAssemblyReference(mdReader, declaringAssembly))
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
                    Console.WriteLine(TrimBasePath(assemblyPath, path));
                }
            },
            cancellationToken);
        invocationContext.ExitCode = await this.Scan(path, scanner, report, cancellationToken);
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
