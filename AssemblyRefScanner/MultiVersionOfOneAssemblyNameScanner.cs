using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AssemblyRefScanner
{
    /// <summary>
    /// Scans for assemblies that reference another assembly more than once, for purposes of referencing multiple versions simultaneously.
    /// </summary>
    internal class MultiVersionOfOneAssemblyNameScanner : ScannerBase
    {
        private static readonly HashSet<string> runtimeAssemblySimpleNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
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

        internal MultiVersionOfOneAssemblyNameScanner(CancellationToken cancellationToken)
            : base(cancellationToken)
        {
        }

        internal async Task<int> Execute(string path)
        {
            var refReader = CreateProcessAssembliesBlock(
                mdReader => (from referenceHandle in mdReader.AssemblyReferences
                             let reference = mdReader.GetAssemblyReference(referenceHandle).GetAssemblyName()
                             group reference by reference.Name).ToImmutableDictionary(kv => kv.Key, kv => kv.ToImmutableArray(), StringComparer.OrdinalIgnoreCase));
            var aggregator = CreateReportBlock(
                refReader,
                (assemblyPath, results) =>
                {
                    foreach (var referencesByName in results)
                    {
                        if (runtimeAssemblySimpleNames.Contains(referencesByName.Key))
                        {
                            // We're not interested in multiple versions referenced from mscorlib, etc.
                            continue;
                        }

                        if (referencesByName.Value.Length > 1)
                        {
                            Console.WriteLine(assemblyPath);
                            foreach (var reference in referencesByName.Value)
                            {
                                Console.WriteLine($"\t{reference}");
                            }

                            Console.WriteLine();
                        }
                    }
                });

            return await this.Scan(path, refReader, aggregator);
        }
    }
}
