# AssemblyRefScanner

[![NuGet package](https://img.shields.io/nuget/v/AssemblyRefScanner)](https://www.nuget.org/packages/assemblyrefscanner)
[![Build Status](https://dev.azure.com/andrewarnott/OSS/_apis/build/status/AssemblyRefScanner/AArnott.AssemblyRefScanner?branchName=main)](https://dev.azure.com/andrewarnott/OSS/_build/latest?definitionId=63&branchName=main)

This tool will very quickly scan an entire directory tree for all managed assemblies that reference interesting things, including:

1. A particular simple assembly name of interest.
1. Multiple references to the same assembly, but different versions, within the same assembly. For example if A.dll references B.dll v1.0.0.0 and B.dll v2.0.0.0, A.dll would be found.
1. A particular *type* (useful when making a breaking change).

## Usage

Install or update the CLI tool with:

```
dotnet tool update -g AssemblyRefScanner
```

Then refer to the tool by its CLI name: `refscanner`:

```
PS> refscanner -h
Description:
  AssemblyRefScanner v1.0.41-beta+8f8b7e9c74

Usage:
  refscanner [command] [options]

Options:
  --version       Show version information
  -?, -h, --help  Show help and usage information

Commands:
  assembly <simpleAssemblyName>         Searches for references to the assembly with the specified simple name.
  multiversions                         All assemblies that reference multiple versions of *any* assembly will be printed.
  embeddedTypes <embeddableAssemblies>  Searches for assemblies that have embedded types.
  type <typeName>                       Searches for references to a given type.
  targetFramework                       Groups all assemblies by TargetFramework.
  resolveReferences <assemblyPath>      Lists paths to assemblies referenced by a given assembly.
```

You can then get usage help for a particular command:

```
PS> refscanner assembly -h
assembly:
  Searches for references to the assembly with the specified simple name.

Usage:
  AssemblyRefScanner assembly [options] <simpleAssemblyName>

Arguments:
  <simpleAssemblyName>    The simple assembly name (e.g. "StreamJsonRpc") to search for in referenced assembly lists.

Options:
  --path <path>     The path of the directory to search. This should be a full install of VS (i.e. all workloads) to produce complete results. If not specified, the current directory will be searched. [default:
                    C:\git\Helix]
  -?, -h, --help    Show help and usage information
  ```

**Tip:** Enjoy completion of commands and switches at the command line by using PowerShell and taking [these steps](https://github.com/dotnet/command-line-api/blob/main/docs/dotnet-suggest.md).

### Samples

#### Search for all references to StreamJsonRpc

```
PS> refscanner assembly streamjsonrpc --path "C:\Program Files (x86)\Microsoft Visual Studio\2019\master\Common7\IDE\"

1.2.0.0
        Extensions\Microsoft\LiveShare\Microsoft.VisualStudio.LanguageServer.Client.LiveShare.dll

1.3.0.0
        CommonExtensions\Microsoft\ModelBuilder\Microsoft.ML.ModelBuilder.dll
        Extensions\Microsoft\LiveShare\Microsoft.VisualStudio.LanguageServices.LanguageExtension.15.8.dll
        Extensions\Microsoft\LiveShare\Microsoft.VisualStudio.LanguageServices.LanguageExtension.16.0.dll
        Extensions\Microsoft\LiveShare\Microsoft.VisualStudio.LanguageServices.LanguageExtension.dll
        Extensions\Microsoft\LiveShare\Microsoft.VisualStudio.LiveShare.Core.dll
        Extensions\Microsoft\LiveShare\Microsoft.VisualStudio.LiveShare.Rpc.Json.dll
        CommonExtensions\Microsoft\ModelBuilder\AutoMLService\Microsoft.ML.ModelBuilder.AutoMLService.dll
        CommonExtensions\Microsoft\ModelBuilder\AzCopyService\Microsoft.ML.ModelBuilder.AzCopyService.dll
```

Above we see all the assemblies listed that reference StreamJsonRpc.dll, grouped by the version of StreamJsonRpc.dll that they reference.

#### Search for multi-version references

```
PS> refscanner multiversions --path "C:\Program Files (x86)\Microsoft Visual Studio\2019\master\Common7\IDE\"

CommonExtensions\Microsoft\LanguageServer\Microsoft.VisualStudio.LanguageServer.Client.dll
        StreamJsonRpc, Version=1.5.0.0, PublicKeyToken=b03f5f7f11d50a3a
        StreamJsonRpc, Version=2.4.0.0, PublicKeyToken=b03f5f7f11d50a3a

CommonExtensions\Microsoft\LanguageServer\Microsoft.VisualStudio.LanguageServer.Client.Implementation.dll
        StreamJsonRpc, Version=2.4.0.0, PublicKeyToken=b03f5f7f11d50a3a
        StreamJsonRpc, Version=1.5.0.0, PublicKeyToken=b03f5f7f11d50a3a
```

Above we see that two assemblies each reference *two* versions of StreamJsonRpc simultaneously.
