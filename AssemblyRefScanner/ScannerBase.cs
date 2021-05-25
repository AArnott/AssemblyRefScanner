using System;
using System.Diagnostics;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace AssemblyRefScanner
{
    internal abstract class ScannerBase
    {
        internal ScannerBase(CancellationToken cancellationToken)
        {
            this.CancellationToken = cancellationToken;
        }

        protected CancellationToken CancellationToken { get; }

        protected TransformManyBlock<string, (string AssemblyPath, T Results)> CreateProcessAssembliesBlock<T>(Func<MetadataReader, T> assemblyReader)
            where T : class?
        {
            return new TransformManyBlock<string, (string AssemblyPath, T Results)>(
                assemblyPath =>
                {
                    using (var assemblyStream = File.OpenRead(assemblyPath))
                    {
                        try
                        {
                            using var peReader = new PEReader(assemblyStream);
                            var mdReader = peReader.GetMetadataReader();
                            return new[] { (assemblyPath, assemblyReader(mdReader)) };
                        }
                        catch (InvalidOperationException)
                        {
                            // Not a PE file.
                            return Array.Empty<(string, T)>();
                        }
                    }
                },
                new ExecutionDataflowBlockOptions
                {
                    MaxMessagesPerTask = 5,
                    BoundedCapacity = Environment.ProcessorCount * 4,
                    SingleProducerConstrained = true,
                    CancellationToken = this.CancellationToken,
                    MaxDegreeOfParallelism = Environment.ProcessorCount,
                });
        }

        protected ITargetBlock<(string AssemblyPath, T Results)> CreateReportBlock<T>(ISourceBlock<(string AssemblyPath, T Results)> previousBlock, Action<string, T> report)
        {
            var block = new ActionBlock<(string AssemblyPath, T Results)>(
                tuple => report(tuple.AssemblyPath, tuple.Results),
                new ExecutionDataflowBlockOptions
                {
                    BoundedCapacity = 16,
                    MaxMessagesPerTask = 5,
                    CancellationToken = this.CancellationToken,
                });
            previousBlock.LinkTo(block, new DataflowLinkOptions { PropagateCompletion = true });
            return block;
        }

        /// <summary>
        /// Feeds all assemblies to a starting block and awaits completion of the terminal block.
        /// </summary>
        /// <param name="path">The path to scan for assemblies.</param>
        /// <param name="startingBlock">The block that should receive paths to all assemblies.</param>
        /// <param name="terminalBlock">The block to await completion of.</param>
        /// <returns>The exit code to return from the calling command.</returns>
        protected async Task<int> Scan(string path, ITargetBlock<string> startingBlock, IDataflowBlock terminalBlock)
        {
            var timer = Stopwatch.StartNew();
            int dllCount = 0;
            try
            {
                foreach (var file in Directory.EnumerateFiles(path, "*.dll", SearchOption.AllDirectories))
                {
                    await startingBlock.SendAsync(file);
                    dllCount++;
                }

                startingBlock.Complete();
            }
            catch (Exception ex)
            {
                startingBlock.Fault(ex);
            }

            try
            {
                await terminalBlock.Completion;
                Console.WriteLine($"All done ({dllCount} assemblies scanned in {timer.Elapsed:g}, or {dllCount / timer.Elapsed.TotalSeconds:0,0} assemblies per second)!");
            }
            catch (OperationCanceledException) when (this.CancellationToken.IsCancellationRequested)
            {
                Console.Error.WriteLine("Canceled.");
                return 2;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Fault encountered during scan: ");
                Console.Error.WriteLine(ex);
                return 3;
            }

            return 0;
        }
    }
}
