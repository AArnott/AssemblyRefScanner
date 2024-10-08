// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;

namespace AssemblyRefScanner;

internal class Program
{
    private static async Task<int> Main(string[] args)
    {
        Parser parser = BuildCommandLine();
        return await parser.InvokeAsync(args);
    }

    private static Parser BuildCommandLine()
    {
        var searchDirOption = new Option<string>("--path", () => Directory.GetCurrentDirectory(), "The path of the directory to search. This should be a full install of VS (i.e. all workloads) to produce complete results. If not specified, the current directory will be searched.").LegalFilePathsOnly();

        Argument<string> simpleAssemblyName = new("simpleAssemblyName", "The simple assembly name (e.g. \"StreamJsonRpc\") to search for in referenced assembly lists.");
        Command versions = new("assembly", "Searches for references to the assembly with the specified simple name.")
        {
            searchDirOption,
            simpleAssemblyName,
        };
        versions.SetHandler(
            async context => context.ExitCode = await new AssemblyReferenceScanner
            {
                Path = context.ParseResult.GetValueForOption(searchDirOption)!,
                SimpleAssemblyName = context.ParseResult.GetValueForArgument(simpleAssemblyName),
            }.Execute(context.GetCancellationToken()));

        Command multiVersions = new("multiversions", "All assemblies that reference multiple versions of *any* assembly will be printed.")
        {
            searchDirOption,
        };
        multiVersions.SetHandler(
            async context => context.ExitCode = await new MultiVersionOfOneAssemblyNameScanner
            {
                Path = context.ParseResult.GetValueForArgument(simpleAssemblyName),
            }.Execute(context.GetCancellationToken()));

        Argument<IList<string>> embeddableAssemblies = new("embeddableAssemblies")
        {
            Description = "The path to an embeddable assembly.",
            Arity = ArgumentArity.OneOrMore,
        };
        Command embeddedSearch = new("embeddedTypes", "Searches for assemblies that have embedded types.")
        {
            searchDirOption,
            embeddableAssemblies,
        };
        embeddedSearch.SetHandler(
            async context => context.ExitCode = await new EmbeddedTypeScanner
            {
                Path = context.ParseResult.GetValueForOption(searchDirOption)!,
                EmbeddableAssemblies = context.ParseResult.GetValueForArgument(embeddableAssemblies),
            }.Execute(context.GetCancellationToken()));

        Option<string> declaringAssembly = new(new string[] { "--declaringAssembly", "-a" }, "The simple name of the assembly that declares the API whose references are to be found.");
        Option<string> namespaceArg = new(new string[] { "--namespace", "-n" }, "The namespace of the type to find references to.");
        Argument<string> typeName = new("typeName", "The simple name of the type to find references to.") { Arity = ArgumentArity.ExactlyOne };
        Command typeRefSearch = new("type", "Searches for references to a given type.")
        {
            searchDirOption,
            declaringAssembly,
            namespaceArg,
            typeName,
        };
        typeRefSearch.SetHandler(
            async context => context.ExitCode = await new TypeRefScanner
            {
                Path = context.ParseResult.GetValueForOption(searchDirOption)!,
                DeclaringAssembly = context.ParseResult.GetValueForOption(declaringAssembly),
                Namespace = context.ParseResult.GetValueForOption(namespaceArg),
                TypeName = context.ParseResult.GetValueForArgument(typeName),
            }.Execute(context.GetCancellationToken()));

        Argument<string[]> docId = new("docID", "The DocID that identifies the API member to search for references to. A DocID for a given API may be obtained by compiling a C# program with GenerateDocumentationFile=true that references the API using <see cref=\"the-api\" /> and then inspecting the compiler-generated .xml file for that reference.") { Arity = ArgumentArity.OneOrMore };
        Command apiRefSearch = new("api", "Searches for references to a given type or member.")
        {
            searchDirOption,
            declaringAssembly,
            docId,
        };
        apiRefSearch.SetHandler(
            async context => context.ExitCode = await new ApiRefScanner
            {
                Path = context.ParseResult.GetValueForOption(searchDirOption)!,
                DeclaringAssembly = context.ParseResult.GetValueForOption(declaringAssembly),
                DocIds = context.ParseResult.GetValueForArgument(docId),
            }.Execute(context.GetCancellationToken()));

        Option<string> json = new("--json", "The path to a .json file that will contain the raw output of all assemblies scanned.");
        Option<string> dgml = new("--dgml", "The path to a .dgml file to be generated with all assemblies graphed with their dependencies and identified by TargetFramework.");
        Option<bool> includeRuntimeAssemblies = new("--include-runtime", "Includes runtime assemblies in the output.");
        Command targetFramework = new("targetFramework", "Groups all assemblies by TargetFramework.")
        {
            searchDirOption,
            dgml,
            json,
            includeRuntimeAssemblies,
        };
        targetFramework.SetHandler(
            async context => context.ExitCode = await new TargetFrameworkScanner
            {
                Path = context.ParseResult.GetValueForOption(searchDirOption)!,
                Dgml = context.ParseResult.GetValueForOption(dgml),
                Json = context.ParseResult.GetValueForOption(json),
                IncludeRuntimeAssemblies = context.ParseResult.GetValueForOption(includeRuntimeAssemblies),
            }.Execute(context.GetCancellationToken()));

        Argument<string> assemblyPath = new("assemblyPath", "The path to the assembly to search for assembly references.");
        Option<bool> transitive = new("--transitive", "Resolves transitive assembly references  a = new(in addition to the default direct references).");
        Option<string> config = new("--config", "The path to an .exe.config or .dll.config file to use to resolve references.");
        Option<string> baseDir = new("--base-dir", "The path to the directory to consider the app base directory for resolving assemblies and relative paths in the .config file. If not specified, the default is the directory that contains the .config file if specified, or the directory containing the entry assembly.");
        Option<string[]> runtimeDir = new("--runtime-dir", "The path to a .NET runtime directory where assemblies may also be resolved from. May be used more than once.");
        Option<bool> excludeRuntime = new("--exclude-runtime", "Omits reporting assembly paths that are found in any of the specified runtime directories.");
        Command resolveAssemblyReferences = new("resolveReferences", "Lists paths to assemblies referenced by a given assembly.")
        {
            assemblyPath,
            transitive,
            config,
            baseDir,
            runtimeDir,
            excludeRuntime,
        };
        resolveAssemblyReferences.SetHandler(
            context => new ResolveAssemblyReferences
            {
                AssemblyPath = context.ParseResult.GetValueForArgument(assemblyPath),
                Transitive = context.ParseResult.GetValueForOption(transitive),
                Config = context.ParseResult.GetValueForOption(config),
                BaseDir = context.ParseResult.GetValueForOption(baseDir),
                RuntimeDir = context.ParseResult.GetValueForOption(runtimeDir) ?? [],
                ExcludeRuntime = context.ParseResult.GetValueForOption(excludeRuntime),
            }.Execute(context.GetCancellationToken()));

        var root = new RootCommand($"{ThisAssembly.AssemblyTitle} v{ThisAssembly.AssemblyInformationalVersion}")
        {
            versions,
            multiVersions,
            embeddedSearch,
            apiRefSearch,
            typeRefSearch,
            targetFramework,
            resolveAssemblyReferences,
        };
        root.Name = "refscanner";
        return new CommandLineBuilder(root)
            .UseDefaults()
            .Build();
    }
}
