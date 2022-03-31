// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace AssemblyRefScanner;

/// <summary>
/// Scans for assemblies that reference another assembly more than once, for purposes of referencing multiple versions simultaneously.
/// </summary>
internal class MultiVersionOfOneAssemblyNameScanner : ScannerBase
{
    private static readonly HashSet<string> RuntimeAssemblySimpleNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "mscorlib",
        "system",
        "System.Private.CoreLib",
        "System.Drawing",
        "System.Collections",
        "System.Data",
        "System.Data.Odbc",
        "System.Windows.Forms",
        "System.Net.Http",
        "System.Net.Primitives",
        "System.IO.Compression",
    };

    internal async Task Execute(string path, InvocationContext invocationContext, CancellationToken cancellationToken)
    {
        var refReader = this.CreateProcessAssembliesBlock(
            mdReader => (from referenceHandle in mdReader.AssemblyReferences
                         let reference = mdReader.GetAssemblyReference(referenceHandle).GetAssemblyName()
                         group reference by reference.Name).ToImmutableDictionary(kv => kv.Key, kv => kv.ToImmutableArray(), StringComparer.OrdinalIgnoreCase),
            cancellationToken);
        var aggregator = this.CreateReportBlock(
            refReader,
            (assemblyPath, results) =>
            {
                foreach (var referencesByName in results)
                {
                    if (RuntimeAssemblySimpleNames.Contains(referencesByName.Key))
                    {
                        // We're not interested in multiple versions referenced from mscorlib, etc.
                        continue;
                    }

                    if (referencesByName.Value.Length > 1)
                    {
                        Console.WriteLine(TrimBasePath(assemblyPath, path));
                        foreach (var reference in referencesByName.Value)
                        {
                            Console.WriteLine($"\t{reference}");
                        }

                        Console.WriteLine();
                    }
                }
            },
            cancellationToken);

        invocationContext.ExitCode = await this.Scan(path, refReader, aggregator, cancellationToken);
    }
}
