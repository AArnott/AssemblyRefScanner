// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection.PortableExecutable;

namespace AssemblyRefScanner;

/// <summary>
/// Scans for assemblies whose file name matches a given simple assembly name and reports version metadata.
/// </summary>
internal class AssemblyScanner : ScannerBase
{
    /// <summary>
    /// Gets the simple assembly name to search for.
    /// </summary>
    internal required string SimpleAssemblyName { get; init; }

    /// <summary>
    /// Gets the directory path to search.
    /// </summary>
    internal required string Path { get; init; }

    /// <summary>
    /// Determines whether a file path is a candidate assembly path for the specified simple assembly name.
    /// </summary>
    /// <param name="assemblyPath">The file path to inspect.</param>
    /// <param name="simpleAssemblyName">The simple assembly name being searched for.</param>
    /// <returns><see langword="true"/> if the file name matches; otherwise <see langword="false"/>.</returns>
    internal static bool IsMatchingAssemblyFileName(string assemblyPath, string simpleAssemblyName)
    {
        string extension = global::System.IO.Path.GetExtension(assemblyPath);
        if (!extension.Equals(".dll", StringComparison.OrdinalIgnoreCase) && !extension.Equals(".exe", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return string.Equals(global::System.IO.Path.GetFileNameWithoutExtension(assemblyPath), simpleAssemblyName, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Reads version metadata from a managed assembly.
    /// </summary>
    /// <param name="assemblyPath">The assembly path to read.</param>
    /// <param name="searchPath">The root search path for relative path formatting.</param>
    /// <param name="match">Receives the extracted metadata when successful.</param>
    /// <returns><see langword="true"/> if metadata could be read; otherwise <see langword="false"/>.</returns>
    internal static bool TryReadAssemblyMatch(string assemblyPath, string searchPath, out OwnAssemblyMatch match)
    {
        try
        {
            using FileStream assemblyStream = File.OpenRead(assemblyPath);
            using PEReader peReader = new(assemblyStream);
            MetadataReader metadataReader = peReader.GetMetadataReader();
            AssemblyDefinition assemblyDefinition = metadataReader.GetAssemblyDefinition();

            match = new OwnAssemblyMatch(
                GetDisplayPath(assemblyPath, searchPath),
                assemblyDefinition.Version,
                Version.TryParse(GetAssemblyAttributeValue(metadataReader, "AssemblyFileVersionAttribute"), out Version? fileVersion) ? fileVersion : null,
                GetAssemblyAttributeValue(metadataReader, "AssemblyInformationalVersionAttribute") ?? string.Empty);
            return true;
        }
        catch (BadImageFormatException)
        {
        }
        catch (InvalidOperationException)
        {
        }

        match = default;
        return false;
    }

    /// <summary>
    /// Formats the report table for matched assemblies.
    /// </summary>
    /// <param name="matches">The matches to render.</param>
    /// <returns>The formatted table text.</returns>
    internal static string FormatReport(IEnumerable<OwnAssemblyMatch> matches)
    {
        List<string> lines = new();
        foreach (IGrouping<VersionGroupKey, OwnAssemblyMatch> group in matches.GroupBy(m => new VersionGroupKey(m.FileVersion, m.AssemblyVersion, m.InformationalVersion)).OrderBy(g => g.Key, VersionGroupKeyComparer.Instance))
        {
            lines.Add($"File version: {FormatVersion(group.Key.FileVersion)}");
            lines.Add($"Assembly version: {group.Key.AssemblyVersion}");
            lines.Add($"Informational version: {FormatInformationalVersion(group.Key.InformationalVersion)}");

            foreach (OwnAssemblyMatch match in group.OrderBy(m => m.Path, StringComparer.OrdinalIgnoreCase))
            {
                lines.Add($"\t{match.Path}");
            }

            lines.Add(string.Empty);
        }

        if (lines.Count > 0)
        {
            lines.RemoveAt(lines.Count - 1);
        }

        return string.Join(Environment.NewLine, lines);
    }

    /// <summary>
    /// Executes the scanner.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The process exit code.</returns>
    internal Task<int> Execute(CancellationToken cancellationToken)
    {
        List<OwnAssemblyMatch> matches = new();
        foreach (string assemblyPath in Directory.EnumerateFiles(this.Path, "*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!IsMatchingAssemblyFileName(assemblyPath, this.SimpleAssemblyName))
            {
                continue;
            }

            if (TryReadAssemblyMatch(assemblyPath, this.Path, out OwnAssemblyMatch match))
            {
                matches.Add(match);
            }
        }

        if (matches.Count == 0)
        {
            Console.WriteLine($"No assemblies named {this.SimpleAssemblyName} were found.");
            return Task.FromResult(0);
        }

        Console.WriteLine($"Assemblies named {this.SimpleAssemblyName} found as follows:");
        Console.WriteLine(FormatReport(matches));
        return Task.FromResult(0);
    }

    private static string GetDisplayPath(string assemblyPath, string searchPath)
    {
        string relativePath = TrimBasePath(assemblyPath, searchPath);
        string? directoryPath = global::System.IO.Path.GetDirectoryName(relativePath);
        return string.IsNullOrEmpty(directoryPath) ? "." : directoryPath;
    }

    private static string? GetAssemblyAttributeValue(MetadataReader metadataReader, string attributeTypeName)
    {
        CustomAttributeTypeProvider typeProvider = new();
        foreach (CustomAttributeHandle attributeHandle in metadataReader.CustomAttributes)
        {
            CustomAttribute attribute = metadataReader.GetCustomAttribute(attributeHandle);
            if (attribute.Parent.Kind != HandleKind.AssemblyDefinition || attribute.Constructor.Kind != HandleKind.MemberReference)
            {
                continue;
            }

            MemberReference constructor = metadataReader.GetMemberReference((MemberReferenceHandle)attribute.Constructor);
            if (constructor.Parent.Kind != HandleKind.TypeReference)
            {
                continue;
            }

            TypeReference attributeType = metadataReader.GetTypeReference((TypeReferenceHandle)constructor.Parent);
            if (!metadataReader.StringComparer.Equals(attributeType.Namespace, "System.Reflection") || !metadataReader.StringComparer.Equals(attributeType.Name, attributeTypeName))
            {
                continue;
            }

            CustomAttributeValue<Type> value = attribute.DecodeValue(typeProvider);
            if (value.FixedArguments.Length > 0 && value.FixedArguments[0].Value is string stringValue)
            {
                return stringValue;
            }
        }

        return null;
    }

    private static string FormatInformationalVersion(string informationalVersion)
    {
        return string.IsNullOrEmpty(informationalVersion) ? "<unknown>" : informationalVersion;
    }

    private static string FormatVersion(Version? version)
    {
        return version?.ToString() ?? "<unknown>";
    }

    /// <summary>
    /// Describes a matched assembly and the version metadata extracted from it.
    /// </summary>
    /// <param name="Path">The relative path to the assembly.</param>
    /// <param name="AssemblyVersion">The assembly version.</param>
    /// <param name="FileVersion">The assembly file version.</param>
    /// <param name="InformationalVersion">The assembly informational version.</param>
    internal readonly record struct OwnAssemblyMatch(string Path, Version AssemblyVersion, Version? FileVersion, string InformationalVersion);

    private readonly record struct VersionGroupKey(Version? FileVersion, Version AssemblyVersion, string InformationalVersion);

    private sealed class VersionGroupKeyComparer : IComparer<VersionGroupKey>
    {
        internal static readonly VersionGroupKeyComparer Instance = new();

        public int Compare(VersionGroupKey x, VersionGroupKey y)
        {
            int result;
            if (x.FileVersion is null)
            {
                result = y.FileVersion is null ? 0 : -1;
            }
            else if (y.FileVersion is null)
            {
                result = 1;
            }
            else
            {
                result = x.FileVersion.CompareTo(y.FileVersion);
            }

            if (result != 0)
            {
                return result;
            }

            result = x.AssemblyVersion.CompareTo(y.AssemblyVersion);
            if (result != 0)
            {
                return result;
            }

            return StringComparer.OrdinalIgnoreCase.Compare(x.InformationalVersion, y.InformationalVersion);
        }
    }
}
