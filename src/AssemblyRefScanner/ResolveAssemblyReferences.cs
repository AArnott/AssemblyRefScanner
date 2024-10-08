// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection.PortableExecutable;
using Nerdbank.NetStandardBridge;

namespace AssemblyRefScanner;

internal class ResolveAssemblyReferences : ScannerBase
{
    internal required string AssemblyPath { get; init; }

    internal required bool Transitive { get; init; }

    internal required string? Config { get; init; }

    internal required string? BaseDir { get; init; }

    internal required string[] RuntimeDir { get; init; }

    internal required bool ExcludeRuntime { get; init; }

    public void Execute(CancellationToken cancellationToken)
    {
        string baseDir = this.BaseDir ?? (this.Config is not null ? Path.GetDirectoryName(this.Config)! : Path.GetDirectoryName(this.AssemblyPath)!);
        TrimTrailingSlashes(this.RuntimeDir);

        NetFrameworkAssemblyResolver? alc = this.Config is null ? null : new(this.Config, baseDir);
        HashSet<string> resolvedPaths = new(StringComparer.OrdinalIgnoreCase);
        HashSet<string> unresolvedNames = new(StringComparer.OrdinalIgnoreCase);

        EnumerateAndReportReferences(this.AssemblyPath);

        void EnumerateAndReportReferences(string assemblyPath)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // The .NET runtime includes references to assemblies that are only needed to support .NET Framework-targeted assemblies
            // and are therefore expected to come from the app directory. Thus, any unresolved references coming *from* the runtime directory
            // will be considered By Design and not reported to stderr.
            string assemblyPathDirectory = Path.GetDirectoryName(assemblyPath)!.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            bool isThisUnderRuntimeFolder = this.RuntimeDir.Contains(assemblyPathDirectory, StringComparer.OrdinalIgnoreCase);

            foreach (AssemblyName reference in this.EnumerateReferences(assemblyPath))
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Always try the runtime directories first, since no custom assembly resolver or .config processing
                // will apply at runtime when the assembly is found in the runtime folder.
                // When matching these, the .NET runtime disregards all details in the assembly name except the simple name, so we do too.
                if (this.RuntimeDir.Select(dir => Path.Combine(dir, reference.Name + ".dll")).FirstOrDefault(File.Exists) is string runtimeDirMatch)
                {
                    ReportResolvedReference(runtimeDirMatch, !this.ExcludeRuntime);
                    continue;
                }

                if (alc is not null)
                {
                    try
                    {
                        AssemblyName? resolvedAssembly = alc.GetAssemblyNameByPolicy(reference);

#pragma warning disable SYSLIB0044 // Type or member is obsolete
                        if (resolvedAssembly?.CodeBase is not null && File.Exists(resolvedAssembly.CodeBase))
                        {
                            ReportResolvedReference(resolvedAssembly.CodeBase);
                        }
                        else
                        {
                            ReportUnresolvedReference(resolvedAssembly ?? reference, !isThisUnderRuntimeFolder);
                        }
#pragma warning restore SYSLIB0044 // Type or member is obsolete
                    }
                    catch (InvalidOperationException ex)
                    {
                        Console.Error.WriteLine(ex.Message);
                        ReportUnresolvedReference(reference, !isThisUnderRuntimeFolder);
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
                    ReportUnresolvedReference(reference, !isThisUnderRuntimeFolder);
                }
            }
        }

        void ReportResolvedReference(string path, bool emitToOutput = true)
        {
            if (resolvedPaths.Add(path))
            {
                if (emitToOutput)
                {
                    Console.WriteLine(path);
                }

                if (this.Transitive)
                {
                    EnumerateAndReportReferences(path);
                }
            }
        }

        void ReportUnresolvedReference(AssemblyName reference, bool emitToOutput)
        {
            if (reference.Name is not null && unresolvedNames.Add(reference.Name))
            {
                if (emitToOutput)
                {
                    Console.Error.WriteLine($"Missing referenced assembly: {reference}");
                }
            }
        }
    }

    private static void TrimTrailingSlashes(string[] paths)
    {
        for (int i = 0; i < paths.Length; i++)
        {
            paths[i] = paths[i].TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
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
