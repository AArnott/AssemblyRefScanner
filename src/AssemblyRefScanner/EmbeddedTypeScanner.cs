﻿// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.CommandLine.Invocation;
using System.Reflection.PortableExecutable;

namespace AssemblyRefScanner;

internal class EmbeddedTypeScanner : ScannerBase
{
    internal async Task Execute(string path, IList<string> embeddableAssemblies, InvocationContext invocationContext, CancellationToken cancellationToken)
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
            },
            cancellationToken);
        var reporter = this.CreateReportBlock(
            typeScanner,
            (assemblyPath, results) =>
            {
                if (!results.IsEmpty)
                {
                    Console.WriteLine(TrimBasePath(assemblyPath, path));
                }
            },
            cancellationToken);
        invocationContext.ExitCode = await this.Scan(path, typeScanner, reporter, cancellationToken);
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
