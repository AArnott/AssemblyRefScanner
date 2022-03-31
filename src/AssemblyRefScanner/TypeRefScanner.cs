// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace AssemblyRefScanner;

internal class TypeRefScanner : ScannerBase
{
    internal async Task Execute(string path, string? declaringAssembly, string? @namespace, string typeName, InvocationContext invocationContext, CancellationToken cancellationToken)
    {
        var scanner = this.CreateProcessAssembliesBlock(
            mdReader =>
            {
                if (GetBreakingChangedTypeReference(mdReader, declaringAssembly, @namespace, typeName) is TypeReferenceHandle interestingTypeHandle)
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
                    Console.WriteLine(TrimBasePath(assemblyPath, path));
                }
            },
            cancellationToken);
        invocationContext.ExitCode = await this.Scan(path, scanner, report, cancellationToken);
    }

    private static TypeReferenceHandle? GetBreakingChangedTypeReference(MetadataReader mdReader, string? declaringAssembly, string? typeNamespace, string typeName)
    {
        if (declaringAssembly is object)
        {
            bool found = false;
            foreach (AssemblyReferenceHandle refHandle in mdReader.AssemblyReferences)
            {
                AssemblyReference assemblyReference = mdReader.GetAssemblyReference(refHandle);
                AssemblyName an = assemblyReference.GetAssemblyName();
                if (string.Equals(an.Name, declaringAssembly, StringComparison.OrdinalIgnoreCase))
                {
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                return null;
            }
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
