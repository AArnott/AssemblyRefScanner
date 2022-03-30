// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace AssemblyRefScanner;

using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;

internal class Program
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

    private static async Task<int> Main(string[] args)
    {
        var parser = BuildCommandLine();
        return await parser.InvokeAsync(args);
    }

    private static Parser BuildCommandLine()
    {
        var searchDirOption = new Option<string>("--path", () => Directory.GetCurrentDirectory(), "The path of the directory to search. This should be a full install of VS (i.e. all workloads) to produce complete results. If not specified, the current directory will be searched.").LegalFilePathsOnly();

        var versions = new Command("assembly", "Searches for references to the assembly with the specified simple name.")
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

        var embeddedSearch = new Command("embeddedTypes", "Searches for assemblies that have embedded types.")
        {
            searchDirOption,
            new Argument("embeddableAssemblies")
            {
                Description = "The path to an embeddable assembly.",
                Arity = ArgumentArity.OneOrMore,
            },
        };
        embeddedSearch.Handler = CommandHandler.Create<string, IList<string>>(new EmbeddedTypeScanner(CtrlCToken).Execute);

        var typeRefSearch = new Command("type", "Searches for references to a given type.")
        {
            searchDirOption,
            new Option<string>(new string[] { "--declaringAssembly", "-a" }, "The simple name of the assembly that declares the type whose references are to be found."),
            new Option<string>(new string[] { "--namespace", "-n" }, "The namespace of the type to find references to."),
            new Argument<string>("typeName", "The simple name of the type to find references to.") { Arity = ArgumentArity.ExactlyOne },
        };
        typeRefSearch.Handler = CommandHandler.Create<string, string, string, string>(new TypeRefScanner(CtrlCToken).Execute);

        Command targetFramework = new("targetFramework", "Groups all assemblies by TargetFramework.")
        {
            searchDirOption,
            new Option<string>("--dgml", "The path to a .dgml file to be generated with all assemblies graphed with their dependencies and identified by TargetFramework."),
        };
        targetFramework.Handler = CommandHandler.Create<string, string>(new TargetFrameworkScanner(CtrlCToken).Execute);

        var root = new RootCommand($"{ThisAssembly.AssemblyTitle} v{ThisAssembly.AssemblyInformationalVersion}")
        {
            versions,
            multiVersions,
            embeddedSearch,
            typeRefSearch,
            targetFramework,
        };
        return new CommandLineBuilder(root)
            .UseDefaults()
            .Build();
    }
}
