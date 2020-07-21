using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.CommandLine;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace AssemblyRefScanner
{
    class Program
    {
        static async Task<int> Main(string[] args)
        {
            var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (s, e) =>
            {
                Console.WriteLine("Canceling...");
                cts.Cancel();
                e.Cancel = true;
            };

            string? path = null;
            string? simpleAssemblyName = null;
            var argSyntax = ArgumentSyntax.Parse(args, syntax =>
            {
                syntax.DefineOption("r|findReferences", ref simpleAssemblyName, "Searches for references to the assembly with the specified simple name. Without this switch, all assemblies that reference multiple versions of *any* assembly will be printed.");
                syntax.DefineParameter("path", ref path, "The path to the directory to search for assembly references.");
            });
            
            if (path is null)
            {
                Console.Error.WriteLine(argSyntax.GetHelpText(Console.WindowWidth));
                return 1;
            }

            var refReader = new TransformBlock<string, (string AssemblyPath, ImmutableDictionary<string, ImmutableArray<AssemblyName>>? References)>(
                assemblyPath => (assemblyPath, ScanAssemblyReferences(assemblyPath)),
                new ExecutionDataflowBlockOptions
                {
                    MaxMessagesPerTask = 5,
                    BoundedCapacity = Environment.ProcessorCount * 4,
                    SingleProducerConstrained = true,
                    CancellationToken = cts.Token,
                    MaxDegreeOfParallelism = Environment.ProcessorCount,
                });

            var filterBlock = new TransformManyBlock<(string AssemblyPath, ImmutableDictionary<string, ImmutableArray<AssemblyName>>? References), (string AssemblyPath, ImmutableDictionary<string, ImmutableArray<AssemblyName>> References)>(
                input =>
                {
                    var result = ImmutableList.Create<(string AssemblyPath, ImmutableDictionary<string, ImmutableArray<AssemblyName>> References)>();
                    return input.References is object ? result.Add((input.AssemblyPath, input.References)) : result;
                },
                new ExecutionDataflowBlockOptions
                {
                    MaxMessagesPerTask = 20,
                    CancellationToken = cts.Token,
                });
            refReader.LinkTo(filterBlock, new DataflowLinkOptions { PropagateCompletion = true });

            var reportGraph = simpleAssemblyName is null
                ? PrintAssembliesWithMultiVersionedDependencies(cts.Token)
                : PrintAssembliesWithInterestingReference(simpleAssemblyName, cts.Token);

            filterBlock.LinkTo(reportGraph.ReceivingBlock, new DataflowLinkOptions { PropagateCompletion = true });

            var timer = Stopwatch.StartNew();
            int dllCount = 0;
            try
            {
                foreach (var file in Directory.EnumerateFiles(path, "*.dll", SearchOption.AllDirectories))
                {
                    await refReader.SendAsync(file);
                    dllCount++;
                }

                refReader.Complete();
            }
            catch (Exception ex)
            {
                ITargetBlock<string> failBlock = refReader;
                failBlock.Fault(ex);
            }

            try
            {
                await reportGraph.ReportComplete;
                Console.WriteLine($"All done ({dllCount} libraries scanned in {timer.Elapsed:g}, or {dllCount / timer.Elapsed.TotalSeconds:0,0} libraries per second)!");
            }
            catch (OperationCanceledException)
            {
                Console.Error.WriteLine("Canceled.");
                return 2;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Fault encountered during scan: ");
                Console.Error.WriteLine(ex);
            }

            return 0;
        }

        /// <summary>
        /// Returns a map of reference assembly simple names to the set of full names actually referenced.
        /// </summary>
        /// <param name="assemblyPath">The path to the assembly to load.</param>
        /// <returns>A sequence of references key'd by simple name.</returns>
        static ImmutableDictionary<string, ImmutableArray<AssemblyName>>? ScanAssemblyReferences(string assemblyPath)
        {
            using (var assemblyStream = File.OpenRead(assemblyPath))
            {
                try
                {
                    var peReader = new PEReader(assemblyStream);
                    var mdReader = peReader.GetMetadataReader();
                    return (from referenceHandle in mdReader.AssemblyReferences
                            let reference = mdReader.GetAssemblyReference(referenceHandle).GetAssemblyName()
                            group reference by reference.Name).ToImmutableDictionary(kv => kv.Key, kv => kv.ToImmutableArray(), StringComparer.OrdinalIgnoreCase);
                }
                catch (InvalidOperationException) { /* Not a PE file */ }
            }

            return null;
        }

        static (ITargetBlock<(string, ImmutableDictionary<string, ImmutableArray<AssemblyName>>)> ReceivingBlock, Task ReportComplete) PrintAssembliesWithInterestingReference(string interestingReferenceName, CancellationToken cancellationToken)
        {
            var versionsReferenced = new Dictionary<Version, List<string>>();
            var aggregatingBlock = new ActionBlock<(string AssemblyPath, ImmutableDictionary<string, ImmutableArray<AssemblyName>> References)>(
                item =>
                {
                    if (item.References.TryGetValue(interestingReferenceName, out ImmutableArray<AssemblyName> interestingRefs))
                    {
                        foreach (var reference in interestingRefs)
                        {
                            if (reference.Version is null)
                            {
                                continue;
                            }

                            if (!versionsReferenced.TryGetValue(reference.Version, out List<string>? referencingPaths))
                                versionsReferenced.Add(reference.Version, referencingPaths = new List<string>());

                            referencingPaths.Add(item.AssemblyPath);
                        }
                    }
                },
                new ExecutionDataflowBlockOptions
                {
                    BoundedCapacity = 16,
                    MaxMessagesPerTask = 5,
                    CancellationToken = cancellationToken,
                });

            var reportTask = Task.Run(
                async delegate
                {
                    await aggregatingBlock.Completion;

                    Console.WriteLine($"The {interestingReferenceName} assembly is referenced as follows:");
                    foreach (var item in versionsReferenced.OrderBy(kv => kv.Key))
                    {
                        Console.WriteLine(item.Key);
                        foreach (var referencingPath in item.Value)
                        {
                            Console.WriteLine($"\t{referencingPath}");
                        }

                        Console.WriteLine();
                    }
                });
            return (aggregatingBlock, reportTask);
        }

        static readonly HashSet<string> runtimeAssemblySimpleNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
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

        static (ITargetBlock<(string, ImmutableDictionary<string, ImmutableArray<AssemblyName>>)> ReceivingBlock, Task ReportComplete) PrintAssembliesWithMultiVersionedDependencies(CancellationToken cancellationToken)
        {
            var reportBlock = new ActionBlock<(string AssemblyPath, ImmutableDictionary<string, ImmutableArray<AssemblyName>> References)>(
                item =>
                {
                    foreach (var referencesByName in item.References)
                    {
                        if (runtimeAssemblySimpleNames.Contains(referencesByName.Key))
                        {
                            // We're not interested in multiple versions referenced from mscorlib, etc.
                            continue;
                        }

                        if (referencesByName.Value.Length > 1)
                        {
                            Console.WriteLine(item.AssemblyPath);
                            foreach (var reference in referencesByName.Value)
                            {
                                Console.WriteLine($"\t{reference}");
                            }

                            Console.WriteLine();
                        }
                    }
                },
                new ExecutionDataflowBlockOptions
                {
                    BoundedCapacity = 16,
                    MaxMessagesPerTask = 5,
                    CancellationToken = cancellationToken,
                });
            return (reportBlock, reportBlock.Completion);
        }
    }
}
