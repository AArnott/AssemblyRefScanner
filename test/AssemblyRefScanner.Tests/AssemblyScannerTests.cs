// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Reflection;

public class AssemblyScannerTests : IDisposable
{
    private readonly string tempDirectory = global::System.IO.Path.Combine(global::System.IO.Path.GetTempPath(), $"OwnAssemblyScannerTests.{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(this.tempDirectory))
        {
            Directory.Delete(this.tempDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task AssemblyCommand_FindsMatchingDllAndPrintsMetadata()
    {
        string toolPath = typeof(DocId).Assembly.Location;
        string searchPath = global::System.IO.Path.GetDirectoryName(toolPath)!;
        Assembly toolAssembly = typeof(DocId).Assembly;

        CommandResult result = await InvokeToolAsync(toolPath, $"assembly AssemblyRefScanner --path \"{searchPath}\"");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Assemblies named AssemblyRefScanner found as follows:", result.StandardOutput);
        Assert.Contains(toolAssembly.GetName().Version?.ToString() ?? string.Empty, result.StandardOutput);
        Assert.Contains(toolAssembly.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version ?? string.Empty, result.StandardOutput);
        Assert.Contains(toolAssembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? string.Empty, result.StandardOutput);
        Assert.Contains("Assembly version:", result.StandardOutput);
        Assert.Contains("Informational version:", result.StandardOutput);
        Assert.Contains("\t.", result.StandardOutput);
    }

    [Fact]
    public async Task AssemblyCommand_FindsMatchingExeFiles()
    {
        string toolPath = typeof(DocId).Assembly.Location;
        Directory.CreateDirectory(this.tempDirectory);

        string copiedAssemblyPath = global::System.IO.Path.Combine(this.tempDirectory, "AssemblyRefScanner.exe");
        File.Copy(toolPath, copiedAssemblyPath);

        CommandResult result = await InvokeToolAsync(toolPath, $"assembly AssemblyRefScanner --path \"{this.tempDirectory}\"");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("\t.", result.StandardOutput);
    }

    [Fact]
    public async Task AssemblyCommand_PrintsPathInGroupedOutput()
    {
        string toolPath = typeof(DocId).Assembly.Location;
        Directory.CreateDirectory(this.tempDirectory);

        string longDirectory = global::System.IO.Path.Combine(this.tempDirectory, "very", "long", "path", "that", "should", "not", "be", "truncated", "in", "the", "path", "column");
        Directory.CreateDirectory(longDirectory);

        string copiedAssemblyPath = global::System.IO.Path.Combine(longDirectory, "AssemblyRefScanner.dll");
        File.Copy(toolPath, copiedAssemblyPath);

        CommandResult result = await InvokeToolAsync(toolPath, $"assembly AssemblyRefScanner --path \"{this.tempDirectory}\"");
        string expectedDirectory = string.Join(global::System.IO.Path.DirectorySeparatorChar, ["very", "long", "path", "that", "should", "not", "be", "truncated", "in", "the", "path", "column"]);
        string unexpectedPath = expectedDirectory + global::System.IO.Path.DirectorySeparatorChar + "AssemblyRefScanner.dll";

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("File version:", result.StandardOutput);
        Assert.Contains("Assembly version:", result.StandardOutput);
        Assert.Contains("Informational version:", result.StandardOutput);
        Assert.Contains(expectedDirectory, result.StandardOutput);
        Assert.DoesNotContain(unexpectedPath, result.StandardOutput);
    }

    private static async Task<CommandResult> InvokeToolAsync(string toolPath, string arguments)
    {
        ProcessStartInfo startInfo = new("dotnet", $"\"{toolPath}\" {arguments}")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        using Process process = Process.Start(startInfo)!;
        string standardOutput = await process.StandardOutput.ReadToEndAsync();
        string standardError = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        return new(process.ExitCode, standardOutput, standardError);
    }

    private readonly record struct CommandResult(int ExitCode, string StandardOutput, string StandardError);
}
