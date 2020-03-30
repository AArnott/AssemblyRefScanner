using Microsoft;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace AssemblyRefScanner
{
    class Program
    {
        private const string baseDir = @"C:\Program Files (x86)\Microsoft Visual Studio\2019\IntPreview";
        private const string interestingReferenceName = "StreamJsonRpc";

        static async Task Main(string[] args)
        {
            var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (s, e) =>
            {
                Console.WriteLine("Canceling...");
                cts.Cancel();
                e.Cancel = true;
            };

            var refReader = new TransformBlock<string, (string AssemblyPath, ImmutableDictionary<string, ImmutableArray<string>>? References)>(
                assemblyPath => (assemblyPath, ScanAssemblyReferences(assemblyPath)),
                new ExecutionDataflowBlockOptions
                {
                    MaxMessagesPerTask = 5,
                    BoundedCapacity = Environment.ProcessorCount * 4,
                    SingleProducerConstrained = true,
                    CancellationToken = cts.Token,
                    MaxDegreeOfParallelism = Environment.ProcessorCount,
                });

            var filter = new ActionBlock<(string AssemblyPath, ImmutableDictionary<string, ImmutableArray<string>>? References)>(
                item =>
                {
                    if (item.References is null)
                    {
                        return;
                    }

                    //PrintAssembliesWithMultiVersionedDependencies(item.AssemblyPath, item.References);
                    PrintAssembliesWithInterestingReference(item.AssemblyPath, item.References);
                },
                new ExecutionDataflowBlockOptions
                {
                    BoundedCapacity = 16,
                    MaxMessagesPerTask = 5,
                    SingleProducerConstrained = true,
                    CancellationToken = cts.Token
                });
            refReader.LinkTo(filter, new DataflowLinkOptions { PropagateCompletion = true });

            int count = 0;
            try
            {
                foreach (var file in Directory.EnumerateFiles(baseDir, "*.dll", SearchOption.AllDirectories))
                {
                    await refReader.SendAsync(file);
                    count++;
                }

                refReader.Complete();
            }
            catch (Exception ex)
            {
                ITargetBlock<string> failBlock = refReader;
                failBlock.Fault(ex);
            }

            await filter.Completion;
            Console.WriteLine($"All done ({count} libraries scanned)!");
            Console.ReadLine();
        }

        /// <summary>
        /// Returns a map of reference assembly simple names to the set of full names actually referenced.
        /// </summary>
        /// <param name="assemblyPath">The path to the assembly to load.</param>
        /// <returns>A sequence of references key'd by simple name.</returns>
        static ImmutableDictionary<string, ImmutableArray<string>>? ScanAssemblyReferences(string assemblyPath)
        {
            try
            {
                Assembly a = new AssemblyLoadContext(assemblyPath, isCollectible: true).LoadFromAssemblyPath(assemblyPath);
                return (from reference in a.GetReferencedAssemblies()
                        group reference.FullName by reference.Name).ToImmutableDictionary(kv => kv.Key, kv => kv.ToImmutableArray());
            }
            catch (BadImageFormatException) { }
            catch (FileNotFoundException) { }
            catch (FileLoadException) { }

            return null;
        }

        static void PrintAssembliesWithInterestingReference(string assemblyPath, ImmutableDictionary<string, ImmutableArray<string>> references)
        {
            if (references.TryGetValue(interestingReferenceName, out ImmutableArray<string> interestingRefs))
            {
                Console.WriteLine(assemblyPath);
                foreach (var reference in interestingRefs)
                {
                    Console.WriteLine($"\t{reference}");
                }

                Console.WriteLine();
            }
        }

        static void PrintAssembliesWithMultiVersionedDependencies(string assemblyPath, ImmutableDictionary<string, ImmutableArray<string>> references)
        {
            foreach (var referencesByName in references)
            {
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
        }
    }
}
