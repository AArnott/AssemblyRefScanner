// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace AssemblyRefScanner;

using System.Reflection.PortableExecutable;
using Nerdbank.NetStandardBridge;

internal class ResolveAssemblyReferences : ScannerBase
{
    public ResolveAssemblyReferences(CancellationToken cancellationToken)
        : base(cancellationToken)
    {
    }

    public void Execute(string assemblyPath, bool transitive, string? config, string? baseDir, string[] runtimeDir)
    {
        baseDir ??= config is not null ? Path.GetDirectoryName(config)! : Path.GetDirectoryName(assemblyPath)!;

        NetFrameworkAssemblyResolver? alc = config is null ? null : new(config, baseDir);
        HashSet<string> resolvedPaths = new(StringComparer.OrdinalIgnoreCase);
        HashSet<string> unresolvedNames = new(StringComparer.OrdinalIgnoreCase);

        EnumerateAndReportReferences(assemblyPath);

        void EnumerateAndReportReferences(string assemblyPath)
        {
            foreach (AssemblyName reference in this.EnumerateReferences(assemblyPath))
            {
                AssemblyName? resolvedAssembly = alc?.GetAssemblyNameByPolicy(reference);
                if (resolvedAssembly?.CodeBase is not null && File.Exists(resolvedAssembly.CodeBase))
                {
                    ReportResolvedReference(resolvedAssembly.CodeBase);
                }
                else if (runtimeDir.Select(dir => Path.Combine(dir, (resolvedAssembly ?? reference).Name + ".dll")).FirstOrDefault(File.Exists) is string runtimeDirMatch)
                {
                    ReportResolvedReference(runtimeDirMatch);
                }
                else if (alc is null && File.Exists(Path.Combine(baseDir, reference.Name + ".dll")))
                {
                    // We only find assemblies in the same directory if no config file was specified.
                    ReportResolvedReference(Path.Combine(baseDir, reference.Name + ".dll"));
                }
                else
                {
                    ReportUnresolvedReference(resolvedAssembly ?? reference);
                }
            }
        }

        void ReportResolvedReference(string path)
        {
            if (resolvedPaths.Add(path))
            {
                Console.WriteLine(path);
                if (transitive)
                {
                    EnumerateAndReportReferences(path);
                }
            }
        }

        void ReportUnresolvedReference(AssemblyName reference)
        {
            if (reference.Name is not null && unresolvedNames.Add(reference.Name))
            {
                Console.Error.WriteLine($"Missing referenced assembly: {reference}");
            }
        }
    }

    private IEnumerable<AssemblyName> EnumerateReferences(string assemblyPath)
    {
        using (var assemblyStream = File.OpenRead(assemblyPath))
        {
            using PEReader peReader = new(assemblyStream);
            MetadataReader mdReader = peReader.GetMetadataReader();
            foreach (AssemblyReferenceHandle arh in mdReader.AssemblyReferences)
            {
                AssemblyReference ar = mdReader.GetAssemblyReference(arh);
                yield return ar.GetAssemblyName();
            }
        }
    }
}
