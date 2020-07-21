# AssemblyRefScanner

This tool will very quickly scan an entire directory tree for all managed assemblies that reference either of:

1. A particular simple assembly name of interest.
1. Multiple references to the same assembly, but different versions, within the same assembly. For example if A.dll references B.dll v1.0.0.0 and B.dll v2.0.0.0, A.dll would be found.

## Usage

Clone the repo and use `dotnet run -- ` within the `AssemblyRefScanner` directory.

```
usage: AssemblyRefScanner [-r <arg>] [--] <path>

    -r, --findReferences <arg>    Searches for references to the assembly with the specified simple name.
                                  Without this switch, all assemblies that reference multiple versions of
                                  *any* assembly will be printed.
    <path>                        The path to the directory to search for assembly references.
```

### Sample search for all references to StreamJsonRpc

```
> dotnet run -- 'C:\Program Files (x86)\Microsoft Visual Studio\2019\master\Common7\IDE\' -r streamjsonrpc

1.2.0.0
        C:\Program Files (x86)\Microsoft Visual Studio\2019\master\Common7\IDE\Extensions\Microsoft\LiveShare\Microsoft.VisualStudio.LanguageServer.Client.LiveShare.dll

1.3.0.0
        C:\Program Files (x86)\Microsoft Visual Studio\2019\master\Common7\IDE\CommonExtensions\Microsoft\ModelBuilder\Microsoft.ML.ModelBuilder.dll
        C:\Program Files (x86)\Microsoft Visual Studio\2019\master\Common7\IDE\Extensions\Microsoft\LiveShare\Microsoft.VisualStudio.LanguageServices.LanguageExtension.15.8.dll
        C:\Program Files (x86)\Microsoft Visual Studio\2019\master\Common7\IDE\Extensions\Microsoft\LiveShare\Microsoft.VisualStudio.LanguageServices.LanguageExtension.16.0.dll
        C:\Program Files (x86)\Microsoft Visual Studio\2019\master\Common7\IDE\Extensions\Microsoft\LiveShare\Microsoft.VisualStudio.LanguageServices.LanguageExtension.dll
        C:\Program Files (x86)\Microsoft Visual Studio\2019\master\Common7\IDE\Extensions\Microsoft\LiveShare\Microsoft.VisualStudio.LiveShare.Core.dll
        C:\Program Files (x86)\Microsoft Visual Studio\2019\master\Common7\IDE\Extensions\Microsoft\LiveShare\Microsoft.VisualStudio.LiveShare.Rpc.Json.dll
        C:\Program Files (x86)\Microsoft Visual Studio\2019\master\Common7\IDE\CommonExtensions\Microsoft\ModelBuilder\AutoMLService\Microsoft.ML.ModelBuilder.AutoMLService.dll
        C:\Program Files (x86)\Microsoft Visual Studio\2019\master\Common7\IDE\CommonExtensions\Microsoft\ModelBuilder\AzCopyService\Microsoft.ML.ModelBuilder.AzCopyService.dll
```

Above we see all the assemblies listed that reference StreamJsonRpc.dll, grouped by the version of StreamJsonRpc.dll that they reference.

### Sample search for multi-version references

```
> dotnet run -- 'C:\Program Files (x86)\Microsoft Visual Studio\2019\master\Common7\IDE\'

C:\Program Files (x86)\Microsoft Visual Studio\2019\master\Common7\IDE\CommonExtensions\Microsoft\LanguageServer\Microsoft.VisualStudio.LanguageServer.Client.dll
        StreamJsonRpc, Version=1.5.0.0, PublicKeyToken=b03f5f7f11d50a3a
        StreamJsonRpc, Version=2.4.0.0, PublicKeyToken=b03f5f7f11d50a3a

C:\Program Files (x86)\Microsoft Visual Studio\2019\master\Common7\IDE\CommonExtensions\Microsoft\LanguageServer\Microsoft.VisualStudio.LanguageServer.Client.Implementation.dll
        StreamJsonRpc, Version=2.4.0.0, PublicKeyToken=b03f5f7f11d50a3a
        StreamJsonRpc, Version=1.5.0.0, PublicKeyToken=b03f5f7f11d50a3a
```

Above we see that two assemblies each reference *two* versions of StreamJsonRpc simultaneously.
