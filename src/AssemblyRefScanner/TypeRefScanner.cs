// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace AssemblyRefScanner
{
    using System;
    using System.Reflection;
    using System.Reflection.Metadata;
    using System.Threading;
    using System.Threading.Tasks;

    internal class TypeRefScanner : ScannerBase
    {
        public TypeRefScanner(CancellationToken cancellationToken)
            : base(cancellationToken)
        {
        }

        internal async Task<int> Execute(string path, string? declaringAssembly, string? @namespace, string typeName)
        {
            var scanner = this.CreateProcessAssembliesBlock(
                mdReader =>
                {
                    if (GetBreakingChangedTypeReference(mdReader, declaringAssembly, @namespace, typeName) is TypeReferenceHandle interestingTypeHandle)
                    {
                        return true;
                    }

                    return false;
                });
            var report = this.CreateReportBlock(
                scanner,
                (assemblyPath, result) =>
                {
                    if (result)
                    {
                        Console.WriteLine(TrimBasePath(assemblyPath, path));
                    }
                });
            return await this.Scan(path, scanner, report);
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
}
