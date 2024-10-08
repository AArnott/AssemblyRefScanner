// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace AssemblyRefScanner;

/// <summary>
/// Scans for assemblies that reference a given assembly name, and prints a report grouped by referenced assembly version.
/// </summary>
internal class AssemblyReferenceScanner : ScannerBase
{
    internal required string SimpleAssemblyName { get; init; }

    internal required string Path { get; init; }

    internal async Task<int> Execute(CancellationToken cancellationToken)
    {
        var refReader = this.CreateProcessAssembliesBlock(
            mdReader => (from referenceHandle in mdReader.AssemblyReferences
                         let reference = mdReader.GetAssemblyReference(referenceHandle).GetAssemblyName()
                         group reference by reference.Name).ToImmutableDictionary(kv => kv.Key, kv => kv.ToImmutableArray(), StringComparer.OrdinalIgnoreCase),
            cancellationToken);

        var versionsReferenced = new Dictionary<Version, List<string>>();
        var aggregator = this.CreateReportBlock(
            refReader,
            (assemblyPath, results) =>
            {
                if (results.TryGetValue(this.SimpleAssemblyName, out ImmutableArray<AssemblyName> interestingRefs))
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
            },
            cancellationToken);

        int exitCode = await this.Scan(this.Path, startingBlock: refReader, terminalBlock: aggregator, cancellationToken);
        if (exitCode == 0)
        {
            Console.WriteLine($"The {this.SimpleAssemblyName} assembly is referenced as follows:");
            foreach (var item in versionsReferenced.OrderBy(kv => kv.Key))
            {
                Console.WriteLine(item.Key);
                foreach (var referencingPath in item.Value)
                {
                    Console.WriteLine($"\t{TrimBasePath(referencingPath, this.Path)}");
                }

                Console.WriteLine();
            }
        }

        return exitCode;
    }
}
