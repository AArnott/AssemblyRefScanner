using System;
using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace AssemblyRefScanner
{
    class Program
    {
        internal static readonly CancellationToken CtrlCToken;

        static Program()
        {
            var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (s, e) =>
            {
                Console.WriteLine("Canceling...");
                cts.Cancel();
                e.Cancel = true;
            };
            CtrlCToken = cts.Token;
        }

        static async Task<int> Main(string[] args)
        {
            var parser = BuildCommandLine();
            return await parser.InvokeAsync(args);
        }

        private static Parser BuildCommandLine()
        {
            var searchDirOption = new Option<string>("--path", () => Directory.GetCurrentDirectory(), "The path of the directory to search. This should be a full install of VS (i.e. all workloads) to produce complete results. If not specified, the current directory will be searched.").LegalFilePathsOnly();

            var versions = new Command("assemblyVersions", "Searches for references to the assembly with the specified simple name.")
            {
                searchDirOption,
                new Argument<string>("simpleAssemblyName", "The simple assembly name (e.g. \"StreamJsonRpc\") to search for in referenced assembly lists."),
            };
            versions.Handler = CommandHandler.Create<string, string>(new AssemblyReferenceScanner(CtrlCToken).Execute);

            var multiVersions = new Command("multiversions", "All assemblies that reference multiple versions of *any* assembly will be printed.")
            {
                searchDirOption,
            };
            multiVersions.Handler = CommandHandler.Create<string>(new MultiVersionOfOneAssemblyNameScanner(CtrlCToken).Execute);

            var root = new RootCommand($"{ThisAssembly.AssemblyTitle} v{ThisAssembly.AssemblyInformationalVersion}")
            {
                versions,
                multiVersions,
            };
            return new CommandLineBuilder(root)
                .UseDefaults()
                .Build();
        }
    }
}
