// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace AssemblyRefScanner
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Linq;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Scans for assemblies that reference a given assembly name, and prints a report grouped by referenced assembly version.
    /// </summary>
    internal class AssemblyReferenceScanner : ScannerBase
    {
        internal AssemblyReferenceScanner(CancellationToken cancellationToken)
            : base(cancellationToken)
        {
        }

        internal async Task<int> Execute(string simpleAssemblyName, string path)
        {
            var refReader = this.CreateProcessAssembliesBlock(
                mdReader => (from referenceHandle in mdReader.AssemblyReferences
                             let reference = mdReader.GetAssemblyReference(referenceHandle).GetAssemblyName()
                             group reference by reference.Name).ToImmutableDictionary(kv => kv.Key, kv => kv.ToImmutableArray(), StringComparer.OrdinalIgnoreCase));

            var versionsReferenced = new Dictionary<Version, List<string>>();
            var aggregator = this.CreateReportBlock(
                refReader,
                (assemblyPath, results) =>
                {
                    if (results.TryGetValue(simpleAssemblyName, out ImmutableArray<AssemblyName> interestingRefs))
                    {
                        foreach (var reference in interestingRefs)
                        {
                            if (reference.Version is null)
                            {
                                continue;
                            }

                            if (!versionsReferenced.TryGetValue(reference.Version, out List<string>? referencingPaths))
                            {
                                versionsReferenced.Add(reference.Version, referencingPaths = new List<string>());
                            }

                            referencingPaths.Add(assemblyPath);
                        }
                    }
                });

            int exitCode = await this.Scan(path, startingBlock: refReader, terminalBlock: aggregator);
            if (exitCode == 0)
            {
                Console.WriteLine($"The {simpleAssemblyName} assembly is referenced as follows:");
                foreach (var item in versionsReferenced.OrderBy(kv => kv.Key))
                {
                    Console.WriteLine(item.Key);
                    foreach (var referencingPath in item.Value)
                    {
                        Console.WriteLine($"\t{referencingPath}");
                    }

                    Console.WriteLine();
                }
            }

            return exitCode;
        }
    }
}
