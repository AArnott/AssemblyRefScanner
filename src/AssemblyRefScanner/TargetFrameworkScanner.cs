// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace AssemblyRefScanner;

using System.Runtime.Versioning;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;

internal class TargetFrameworkScanner : ScannerBase
{
    private const string DgmlNamespace = "http://schemas.microsoft.com/vs/2009/dgml";
    private static readonly ReadOnlyMemory<byte>[] DotnetRuntimePublicKeyTokens = new ReadOnlyMemory<byte>[]
    {
        new byte[] { 0xb7, 0x7a, 0x5c, 0x56, 0x19, 0x34, 0xe0, 0x89 }, // b77a5c561934e089
        new byte[] { 0x31, 0xbf, 0x38, 0x56, 0xad, 0x36, 0x4e, 0x35 }, // 31bf3856ad364e35
        new byte[] { 0x89, 0x84, 0x5d, 0xcd, 0x80, 0x80, 0xcc, 0x91 }, // 89845dcd8080cc91
        new byte[] { 0xcc, 0x7b, 0x13, 0xff, 0xcd, 0x2d, 0xdd, 0x51 }, // cc7b13ffcd2ddd51
    };

    private enum TargetFrameworkIdentifiers
    {
        // These are sorted in order of increasing preference.
        Unknown,
        NETFramework,
        NETPortable,
        NETStandard,
        NETCore,
    }

    public async Task Execute(string path, string? dgml, string? json, InvocationContext invocationContext, CancellationToken cancellationToken)
    {
        CustomAttributeTypeProvider customAttributeTypeProvider = new();
        var scanner = this.CreateProcessAssembliesBlock(
            mdReader =>
            {
                string assemblyName = mdReader.GetString(mdReader.GetAssemblyDefinition().Name);
                bool isRuntimeAssembly = IsRuntimeAssemblyPublicKeyToken(mdReader.GetAssemblyDefinition().GetAssemblyName().GetPublicKeyToken()) || IsRuntimeAssemblyName(assemblyName);

                FrameworkName? targetFramework = null;
                foreach (CustomAttributeHandle attHandle in mdReader.CustomAttributes)
                {
                    CustomAttribute att = mdReader.GetCustomAttribute(attHandle);
                    if (att.Parent.Kind == HandleKind.AssemblyDefinition)
                    {
                        if (att.Constructor.Kind == HandleKind.MemberReference)
                        {
                            MemberReference memberReference = mdReader.GetMemberReference((MemberReferenceHandle)att.Constructor);
                            if (memberReference.Parent.Kind == HandleKind.TypeReference)
                            {
                                TypeReference typeReference = mdReader.GetTypeReference((TypeReferenceHandle)memberReference.Parent);
                                if (mdReader.StringComparer.Equals(typeReference.Name, "TargetFrameworkAttribute"))
                                {
                                    CustomAttributeValue<Type> value = att.DecodeValue(customAttributeTypeProvider);
                                    if (value.FixedArguments[0].Value is string tfm)
                                    {
                                        targetFramework = new(tfm);
                                    }

                                    break;
                                }
                            }
                        }
                    }
                }

                List<string> referencesList = new();
                if (!isRuntimeAssembly)
                {
                    foreach (AssemblyReferenceHandle refHandle in mdReader.AssemblyReferences)
                    {
                        AssemblyReference assemblyReference = mdReader.GetAssemblyReference(refHandle);

                        // Skip references into the .NET runtime since those aren't particularly related to TargetFramework analysis.
                        ReadOnlyMemory<byte> publicKeyToken = mdReader.GetBlobContent(assemblyReference.PublicKeyOrToken).AsMemory();
                        if (!IsRuntimeAssemblyPublicKeyToken(publicKeyToken))
                        {
                            string referencedAssemblyName = mdReader.GetString(assemblyReference.Name);
                            if (!IsRuntimeAssemblyName(referencedAssemblyName))
                            {
                                referencesList.Add(referencedAssemblyName);
                            }
                        }
                    }
                }

                bool IsRuntimeAssemblyPublicKeyToken(ReadOnlyMemory<byte> publicKeyToken)
                {
                    return DotnetRuntimePublicKeyTokens.Any(m => Equals(m.Span, publicKeyToken.Span));
                }

                static bool IsRuntimeAssemblyName(string assemblyName) => assemblyName == "System" || assemblyName.StartsWith("System.", StringComparison.Ordinal);

                return new AssemblyInfo(assemblyName, targetFramework, referencesList, isRuntimeAssembly);
            },
            cancellationToken);
        Dictionary<string, AssemblyInfo> bestTargetFrameworkPerAssembly = new(StringComparer.OrdinalIgnoreCase);
        var report = this.CreateReportBlock(
            scanner,
            (assemblyPath, result) =>
            {
                if (result.AssemblyName.EndsWith(".resources", StringComparison.OrdinalIgnoreCase) || result.IsRuntimeAssembly)
                {
                    return;
                }

                result.AssemblyPath = assemblyPath;

                if (!bestTargetFrameworkPerAssembly.TryGetValue(result.AssemblyName, out AssemblyInfo? lastBestFound) || lastBestFound.TargetFrameworkIdentifier < result.TargetFrameworkIdentifier)
                {
                    bestTargetFrameworkPerAssembly[result.AssemblyName] = result;
                }
            },
            cancellationToken);

        invocationContext.ExitCode = await this.Scan(path, scanner, report, cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();
        Dictionary<TargetFrameworkIdentifiers, int> targetFrameworkPopularity = new();


        if (json is not null)
        {
            var serializedResults = JsonSerializer.Serialize(bestTargetFrameworkPerAssembly);
            File.WriteAllText(json, serializedResults);
        }

        var groupedByTFM = from item in bestTargetFrameworkPerAssembly
                           orderby item.Value.AssemblyName
                           group item.Value by item.Value.TargetFrameworkIdentifier into groups
                           orderby groups.Key
                           select groups;
        foreach (var item in groupedByTFM)
        {
            Console.WriteLine(item.Key);
            int count = 0;
            foreach (AssemblyInfo assembly in item)
            {
                count++;
                Console.WriteLine($"\t{assembly.AssemblyName}");
            }

            targetFrameworkPopularity.Add(item.Key, count);
        }

        Console.WriteLine("Summary:");
        foreach (KeyValuePair<TargetFrameworkIdentifiers, int> item in targetFrameworkPopularity.OrderByDescending(kv => kv.Value))
        {
            Console.WriteLine($"{item.Key,-25}{item.Value,4} ({item.Value * 100 / bestTargetFrameworkPerAssembly.Count,3}%)");
        }

        Console.WriteLine($"Total:{bestTargetFrameworkPerAssembly.Count,23}");

        if (dgml is not null)
        {
            static XElement TFICategory(TargetFrameworkIdentifiers identifier, string color) => new(XName.Get("Category", DgmlNamespace), new XAttribute("Id", identifier), new XAttribute("Background", color));

            XElement nodesElement = new(XName.Get("Nodes", DgmlNamespace));
            XElement linksElement = new(XName.Get("Links", DgmlNamespace));
            XElement categoriesElement = new(
                XName.Get("Categories", DgmlNamespace),
                TFICategory(TargetFrameworkIdentifiers.Unknown, "Red"),
                TFICategory(TargetFrameworkIdentifiers.NETFramework, "Red"),
                TFICategory(TargetFrameworkIdentifiers.NETCore, "Green"),
                TFICategory(TargetFrameworkIdentifiers.NETStandard, "LightGreen"),
                TFICategory(TargetFrameworkIdentifiers.NETPortable, "Lime"));

            foreach (KeyValuePair<string, AssemblyInfo> item in bestTargetFrameworkPerAssembly)
            {
                nodesElement.Add(new XElement(
                    XName.Get("Node", DgmlNamespace),
                    new XAttribute("Id", item.Value.AssemblyName),
                    new XAttribute("Category", item.Value.TargetFrameworkIdentifier)));

                foreach (string reference in item.Value.References)
                {
                    if (dgml is not null)
                    {
                        linksElement.Add(new XElement(
                            XName.Get("Link", DgmlNamespace),
                            new XAttribute("Source", item.Value.AssemblyName),
                            new XAttribute("Target", reference)));
                    }
                }
            }

            XElement root = new(
                XName.Get("DirectedGraph", DgmlNamespace),
                new XAttribute("Title", "Assembly dependency graph with TargetFrameworks"),
                nodesElement,
                linksElement,
                categoriesElement);
            using FileStream dgmlFile = new(dgml, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true);
            await root.SaveAsync(dgmlFile, SaveOptions.None, cancellationToken);
        }
    }

    private static bool Equals(ReadOnlySpan<byte> array1, ReadOnlySpan<byte> array2)
    {
        if (array1.Length != array2.Length)
        {
            return false;
        }

        for (int i = 0; i < array1.Length; i++)
        {
            if (array1[i] != array2[i])
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Used to invoke from the debugger to formulate the string to include in <see cref="DotnetRuntimePublicKeyTokens"/>.
    /// </summary>
    private static string ByteArrayToCSharp(ReadOnlySpan<byte> buffer)
    {
        StringBuilder builder = new();
        builder.Append("new byte[] { ");
        for (int i = 0; i < buffer.Length; i++)
        {
            if (i > 0)
            {
                builder.Append(", ");
            }

            builder.Append($"0x{buffer[i]:x2}");
        }

        builder.Append(" }, // ");

        for (int i = 0; i < buffer.Length; i++)
        {
            builder.Append($"{buffer[i]:x2}");
        }

        return builder.ToString();
    }

    private record AssemblyInfo(string AssemblyName, FrameworkName? TargetFramework, List<string> References, bool IsRuntimeAssembly)
    {
        public string? AssemblyPath { get; set; }

        internal TargetFrameworkIdentifiers TargetFrameworkIdentifier
        {
            get
            {
                return
                    this.TargetFramework is null ? TargetFrameworkIdentifiers.NETFramework :
                    ".NETFramework".Equals(this.TargetFramework.Identifier, StringComparison.OrdinalIgnoreCase) ? TargetFrameworkIdentifiers.NETFramework :
                    ".NETStandard".Equals(this.TargetFramework.Identifier, StringComparison.OrdinalIgnoreCase) ? TargetFrameworkIdentifiers.NETStandard :
                    ".NETCoreApp".Equals(this.TargetFramework.Identifier, StringComparison.OrdinalIgnoreCase) ? TargetFrameworkIdentifiers.NETCore :
                    ".NETPortable".Equals(this.TargetFramework.Identifier, StringComparison.OrdinalIgnoreCase) ? TargetFrameworkIdentifiers.NETPortable :
                    throw new NotSupportedException("Unrecognized target framework identifier: " + this.TargetFramework.Identifier);
            }
        }
    }
}
