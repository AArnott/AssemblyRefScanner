// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace AssemblyRefScanner
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.IO;
    using System.Reflection.Metadata;
    using System.Reflection.PortableExecutable;
    using System.Threading;
    using System.Threading.Tasks;

    internal class EmbeddedTypeScanner : ScannerBase
    {
        public EmbeddedTypeScanner(CancellationToken cancellationToken)
            : base(cancellationToken)
        {
        }

        internal async Task<int> Execute(string path, IList<string> embeddableAssemblies)
        {
            HashSet<string> embeddableTypeNames = new();
            foreach (string assemblyPath in embeddableAssemblies)
            {
                CollectTypesFrom(embeddableTypeNames, assemblyPath);
            }

            var typeScanner = this.CreateProcessAssembliesBlock(
                mdReader =>
                {
                    MemberReferenceHandle? typeIdentifierCtor = GetTypeIdentifierAttributeCtor(mdReader);

                    if (typeIdentifierCtor is null)
                    {
                        return ImmutableHashSet<string>.Empty;
                    }

                    var embeddedTypeNames = ImmutableHashSet.CreateBuilder<string>();
                    foreach (TypeDefinitionHandle typeDefHandle in mdReader.TypeDefinitions)
                    {
                        TypeDefinition typeDef = mdReader.GetTypeDefinition(typeDefHandle);
                        foreach (CustomAttributeHandle attHandle in typeDef.GetCustomAttributes())
                        {
                            CustomAttribute att = mdReader.GetCustomAttribute(attHandle);
                            if (att.Constructor.Kind == HandleKind.MemberReference && typeIdentifierCtor.Value.Equals((MemberReferenceHandle)att.Constructor))
                            {
                                string fullyQualifiedName = mdReader.GetString(typeDef.Namespace) + "." + mdReader.GetString(typeDef.Name);
                                if (embeddableTypeNames.Contains(fullyQualifiedName))
                                {
                                    embeddedTypeNames.Add(fullyQualifiedName);
                                }
                            }
                        }
                    }

                    return embeddedTypeNames.ToImmutable();
                });
            var reporter = this.CreateReportBlock(
                typeScanner,
                (assemblyPath, results) =>
                {
                    if (!results.IsEmpty)
                    {
                        Console.WriteLine(assemblyPath);
                    }
                });
            return await this.Scan(path, typeScanner, reporter);
        }

        private static void CollectTypesFrom(HashSet<string> embeddableTypeNames, string assemblyPath)
        {
            using var assemblyStream = File.OpenRead(assemblyPath);
            var peReader = new PEReader(assemblyStream);
            var mdReader = peReader.GetMetadataReader();

            MemberReferenceHandle? typeIdentifierCtor = GetTypeIdentifierAttributeCtor(mdReader);
            foreach (TypeDefinitionHandle tdh in mdReader.TypeDefinitions)
            {
                TypeDefinition td = mdReader.GetTypeDefinition(tdh);
                bool isEmbeddedType = false;
                if (typeIdentifierCtor.HasValue)
                {
                    foreach (CustomAttributeHandle attHandle in td.GetCustomAttributes())
                    {
                        CustomAttribute att = mdReader.GetCustomAttribute(attHandle);
                        if (att.Constructor.Kind == HandleKind.MemberReference && typeIdentifierCtor.Value.Equals((MemberReferenceHandle)att.Constructor))
                        {
                            isEmbeddedType = true;
                            break;
                        }
                    }
                }

                if (!isEmbeddedType)
                {
                    string fullyQualifiedName = mdReader.GetString(td.Namespace) + "." + mdReader.GetString(td.Name);
                    embeddableTypeNames.Add(fullyQualifiedName);
                }
            }
        }

        private static MemberReferenceHandle? GetTypeIdentifierAttributeCtor(MetadataReader mdReader)
        {
            MemberReferenceHandle? typeIdentifierCtor = null;
            foreach (MemberReferenceHandle memberRefHandle in mdReader.MemberReferences)
            {
                MemberReference memberRef = mdReader.GetMemberReference(memberRefHandle);
                if (mdReader.StringComparer.Equals(memberRef.Name, ".ctor") &&
                    memberRef.Parent.Kind == HandleKind.TypeReference)
                {
                    TypeReference tr = mdReader.GetTypeReference((TypeReferenceHandle)memberRef.Parent);
                    if (mdReader.StringComparer.Equals(tr.Name, "TypeIdentifierAttribute"))
                    {
                        typeIdentifierCtor = memberRefHandle;
                        break;
                    }
                }
            }

            return typeIdentifierCtor;
        }
    }
}
