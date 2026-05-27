// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.CommandLine;

namespace AssemblyRefScanner;

internal class Program
{
    private static async Task<int> Main(string[] args)
    {
        RootCommand rootCommand = BuildCommandLine();
        return await rootCommand.Parse(args).InvokeAsync();
    }

    private static RootCommand BuildCommandLine()
    {
        var searchDirOption = new Option<string>("--path")
        {
            DefaultValueFactory = _ => Directory.GetCurrentDirectory(),
            Description = "The path of the directory to search. This should be a full install of VS (i.e. all workloads) to produce complete results. If not specified, the current directory will be searched.",
        };

        Argument<string> simpleAssemblyName = new("simpleAssemblyName")
        {
            Description = "The simple assembly name (e.g. \"StreamJsonRpc\") to search for in referenced assembly lists.",
        };
        Command versions = new("assembly", "Searches for references to the assembly with the specified simple name.")
        {
            searchDirOption,
            simpleAssemblyName,
        };
        versions.SetAction(
            async (parseResult, cancellationToken) => await new AssemblyReferenceScanner
            {
                Path = parseResult.GetValue(searchDirOption)!,
                SimpleAssemblyName = parseResult.GetValue(simpleAssemblyName)!,
            }.Execute(cancellationToken));

        Command multiVersions = new("multiversions", "All assemblies that reference multiple versions of *any* assembly will be printed.")
        {
            searchDirOption,
        };
        multiVersions.SetAction(
            async (parseResult, cancellationToken) => await new MultiVersionOfOneAssemblyNameScanner
            {
                Path = parseResult.GetValue(searchDirOption)!,
            }.Execute(cancellationToken));

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
        embeddedSearch.SetAction(
            async (parseResult, cancellationToken) => await new EmbeddedTypeScanner
            {
                Path = parseResult.GetValue(searchDirOption)!,
                EmbeddableAssemblies = parseResult.GetValue(embeddableAssemblies)!,
            }.Execute(cancellationToken));

        Option<string> declaringAssembly = new("--declaringAssembly", "-a")
        {
            Description = "The simple name of the assembly that declares the API whose references are to be found.",
        };
        Option<string> namespaceArg = new("--namespace", "-n")
        {
            Description = "The namespace of the type to find references to.",
        };
        Argument<string> typeName = new("typeName")
        {
            Description = "The simple name of the type to find references to.",
            Arity = ArgumentArity.ExactlyOne,
        };
        Command typeRefSearch = new("type", "Searches for references to a given type.")
        {
            searchDirOption,
            declaringAssembly,
            namespaceArg,
            typeName,
        };
        typeRefSearch.SetAction(
            async (parseResult, cancellationToken) => await new TypeRefScanner
            {
                Path = parseResult.GetValue(searchDirOption)!,
                DeclaringAssembly = parseResult.GetValue(declaringAssembly),
                Namespace = parseResult.GetValue(namespaceArg),
                TypeName = parseResult.GetValue(typeName)!,
            }.Execute(cancellationToken));

        Argument<string[]> docId = new("docID")
        {
            Description = "The DocID that identifies the API member to search for references to. A DocID for a given API may be obtained by compiling a C# program with GenerateDocumentationFile=true that references the API using <see cref=\"the-api\" /> and then inspecting the compiler-generated .xml file for that reference.",
            Arity = ArgumentArity.OneOrMore,
        };
        Command apiRefSearch = new("api", "Searches for references to a given type or member.")
        {
            searchDirOption,
            declaringAssembly,
            docId,
        };
        apiRefSearch.SetAction(
            async (parseResult, cancellationToken) => await new ApiRefScanner
            {
                Path = parseResult.GetValue(searchDirOption)!,
                DeclaringAssembly = parseResult.GetValue(declaringAssembly),
                DocIds = parseResult.GetValue(docId)!,
            }.Execute(cancellationToken));

        Option<string> json = new("--json")
        {
            Description = "The path to a .json file that will contain the raw output of all assemblies scanned.",
        };
        Option<string> dgml = new("--dgml")
        {
            Description = "The path to a .dgml file to be generated with all assemblies graphed with their dependencies and identified by TargetFramework.",
        };
        Option<bool> includeRuntimeAssemblies = new("--include-runtime")
        {
            Description = "Includes runtime assemblies in the output.",
        };
        Command targetFramework = new("targetFramework", "Groups all assemblies by TargetFramework.")
        {
            searchDirOption,
            dgml,
            json,
            includeRuntimeAssemblies,
        };
        targetFramework.SetAction(
            async (parseResult, cancellationToken) => await new TargetFrameworkScanner
            {
                Path = parseResult.GetValue(searchDirOption)!,
                Dgml = parseResult.GetValue(dgml),
                Json = parseResult.GetValue(json),
                IncludeRuntimeAssemblies = parseResult.GetValue(includeRuntimeAssemblies),
            }.Execute(cancellationToken));

        Argument<string> assemblyPath = new("assemblyPath")
        {
            Description = "The path to the assembly to search for assembly references.",
        };
        Option<bool> transitive = new("--transitive")
        {
            Description = "Resolves transitive assembly references in addition to the default direct references.",
        };
        Option<string> config = new("--config")
        {
            Description = "The path to an .exe.config or .dll.config file to use to resolve references.",
        };
        Option<string> baseDir = new("--base-dir")
        {
            Description = "The path to the directory to consider the app base directory for resolving assemblies and relative paths in the .config file. If not specified, the default is the directory that contains the .config file if specified, or the directory containing the entry assembly.",
        };
        Option<string[]> runtimeDir = new("--runtime-dir")
        {
            Description = "The path to a .NET runtime directory where assemblies may also be resolved from. May be used more than once.",
        };
        Option<bool> excludeRuntime = new("--exclude-runtime")
        {
            Description = "Omits reporting assembly paths that are found in any of the specified runtime directories.",
        };
        Command resolveAssemblyReferences = new("resolveReferences", "Lists paths to assemblies referenced by a given assembly.")
        {
            assemblyPath,
            transitive,
            config,
            baseDir,
            runtimeDir,
            excludeRuntime,
        };
        resolveAssemblyReferences.SetAction(
            parseResult => new ResolveAssemblyReferences
            {
                AssemblyPath = parseResult.GetValue(assemblyPath)!,
                Transitive = parseResult.GetValue(transitive),
                Config = parseResult.GetValue(config),
                BaseDir = parseResult.GetValue(baseDir),
                RuntimeDir = parseResult.GetValue(runtimeDir) ?? [],
                ExcludeRuntime = parseResult.GetValue(excludeRuntime),
            }.Execute(CancellationToken.None));

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
        return root;
    }
}
