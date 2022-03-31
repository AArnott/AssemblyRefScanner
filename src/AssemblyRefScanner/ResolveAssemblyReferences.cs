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
                // Always try the runtime directories first, since no custom assembly resolver or .config processing
                // will apply at runtime when the assembly is found in the runtime folder.
                // When matching these, the .NET runtime disregards all details in the assembly name except the simple name, so we do too.
                if (runtimeDir.Select(dir => Path.Combine(dir, reference.Name + ".dll")).FirstOrDefault(File.Exists) is string runtimeDirMatch)
                {
                    ReportResolvedReference(runtimeDirMatch);
                    continue;
                }

                if (alc is not null)
                {
                    try
                    {
                        AssemblyName? resolvedAssembly = alc.GetAssemblyNameByPolicy(reference);

                        if (resolvedAssembly?.CodeBase is not null && File.Exists(resolvedAssembly.CodeBase))
                        {
                            ReportResolvedReference(resolvedAssembly.CodeBase);
                        }
                        else
                        {
                            ReportUnresolvedReference(resolvedAssembly ?? reference);
                        }
                    }
                    catch (InvalidOperationException ex)
                    {
                        Console.Error.WriteLine(ex.Message);
                        ReportUnresolvedReference(reference);
                        continue;
                    }
                }
                else if (File.Exists(Path.Combine(baseDir, reference.Name + ".dll")))
                {
                    // We only find assemblies in the same directory if no config file was specified.
                    ReportResolvedReference(Path.Combine(baseDir, reference.Name + ".dll"));
                    continue;
                }
                else
                {
                    ReportUnresolvedReference(reference);
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
