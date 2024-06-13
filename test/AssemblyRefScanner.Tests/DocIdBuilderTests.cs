// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using AssemblyRefScanner;

public class DocIdBuilderTests : IDisposable
{
    private readonly ITestOutputHelper logger;
    private readonly FileStream assemblyStream;
    private readonly PEReader peReader;
    private readonly MetadataReader reader;
    private readonly DocIdBuilder docIdBuilder;

    public DocIdBuilderTests(ITestOutputHelper logger)
    {
        this.logger = logger;

        try
        {
            this.assemblyStream = File.OpenRead(Assembly.GetExecutingAssembly().Location);
            this.peReader = new(this.assemblyStream);
            this.reader = this.peReader.GetMetadataReader();
            this.docIdBuilder = new(this.reader);
        }
        catch
        {
            this.assemblyStream?.Dispose();
            this.peReader?.Dispose();
            throw;
        }
    }

    public void Dispose()
    {
        this.peReader.Dispose();
        this.assemblyStream.Dispose();
    }

    [Fact]
    public void Types()
    {
        string[] expected = [
            "T:DocIdSamples.ColorA",
            "T:DocIdSamples.IProcess",
            "T:DocIdSamples.ValueType",
            "T:DocIdSamples.Widget",
            "T:DocIdSamples.MyList`1",
            "T:DocIdSamples.UseList",
            "T:DocIdSamples.Widget.NestedClass",
            "T:DocIdSamples.Widget.IMenuItem",
            "T:DocIdSamples.Widget.Del",
            "T:DocIdSamples.Widget.Direction",
            "T:DocIdSamples.MyList`1.Helper`2",
        ];
        this.AssertMatchingDocIds(expected, this.reader.TypeDefinitions.Select(h => (EntityHandle)h));
    }

    [Fact]
    public void Fields()
    {
        string[] expected = [
            "F:DocIdSamples.ColorA.value__",
            "F:DocIdSamples.ColorA.Red",
            "F:DocIdSamples.ColorA.Blue",
            "F:DocIdSamples.ColorA.Green",
            "F:DocIdSamples.ValueType.total",
            "F:DocIdSamples.Widget.AnEvent",
            "F:DocIdSamples.Widget.message",
            "F:DocIdSamples.Widget.defaultColor",
            "F:DocIdSamples.Widget.PI",
            "F:DocIdSamples.Widget.monthlyAverage",
            "F:DocIdSamples.Widget.array1",
            "F:DocIdSamples.Widget.array2",
            "F:DocIdSamples.Widget.pCount",
            "F:DocIdSamples.Widget.ppValues",
            "F:DocIdSamples.Widget.Direction.value__",
            "F:DocIdSamples.Widget.Direction.North",
            "F:DocIdSamples.Widget.Direction.South",
            "F:DocIdSamples.Widget.Direction.East",
            "F:DocIdSamples.Widget.Direction.West",
        ];
        this.AssertMatchingDocIds(expected, this.reader.FieldDefinitions.Select(h => (EntityHandle)h));
    }

    [Fact]
    public void Methods()
    {
        string[] expected = [
            "M:DocIdSamples.ValueType.M(System.Int32)",
            "M:DocIdSamples.ValueType.P_AnEvent(System.Int32)",
            "M:DocIdSamples.Widget.#cctor",
            "M:DocIdSamples.Widget.#ctor",
            "M:DocIdSamples.Widget.#ctor(System.String)",
            "M:DocIdSamples.Widget.Finalize",
            "M:DocIdSamples.Widget.op_Addition(DocIdSamples.Widget,DocIdSamples.Widget)",
            "M:DocIdSamples.Widget.op_Explicit(DocIdSamples.Widget)",
            "M:DocIdSamples.Widget.op_Implicit(DocIdSamples.Widget)",
            "M:DocIdSamples.Widget.add_AnEvent(DocIdSamples.Widget.Del)",
            "M:DocIdSamples.Widget.remove_AnEvent(DocIdSamples.Widget.Del)",
            "M:DocIdSamples.Widget.M0",
            "M:DocIdSamples.Widget.M1(System.Char,System.Single@,DocIdSamples.ValueType@,System.Int32@)",
            "M:DocIdSamples.Widget.M2(System.Int16[],System.Int32[0:,0:],System.Int64[][])",
            "M:DocIdSamples.Widget.M3(System.Int64[][],DocIdSamples.Widget[0:,0:,0:][])",
            "M:DocIdSamples.Widget.M4(System.Char*,System.Drawing.Color**)",
            "M:DocIdSamples.Widget.M5(System.Void*,System.Double*[0:,0:][])",
            "M:DocIdSamples.Widget.M6(System.Int32,System.Object[])",
            "M:DocIdSamples.Widget.get_Width",
            "M:DocIdSamples.Widget.set_Width(System.Int32)",
            "M:DocIdSamples.Widget.get_Item(System.Int32)",
            "M:DocIdSamples.Widget.set_Item(System.Int32,System.Int32)",
            "M:DocIdSamples.Widget.get_Item(System.String,System.Int32)",
            "M:DocIdSamples.Widget.set_Item(System.String,System.Int32,System.Int32)",
            "M:DocIdSamples.MyList`1.Test(`0)",
            "M:DocIdSamples.MyList`1.#ctor",
            "M:DocIdSamples.UseList.Process(DocIdSamples.MyList`1{System.Int32})",
            "M:DocIdSamples.UseList.GetValues``1(``0)",
            "M:DocIdSamples.UseList.#ctor",
            "M:DocIdSamples.Widget.NestedClass.M(System.Int32)",
            "M:DocIdSamples.Widget.NestedClass.#ctor",
            "M:DocIdSamples.Widget.Del.#ctor(System.Object,System.IntPtr)",
            "M:DocIdSamples.Widget.Del.Invoke(System.Int32)",
            "M:DocIdSamples.Widget.Del.BeginInvoke(System.Int32,System.AsyncCallback,System.Object)",
            "M:DocIdSamples.Widget.Del.EndInvoke(System.IAsyncResult)",
            "M:DocIdSamples.MyList`1.Helper`2.#ctor",
        ];
        this.AssertMatchingDocIds(expected, this.reader.MethodDefinitions.Select(h => (EntityHandle)h));
    }

    [Fact]
    public void Events()
    {
        string[] expected = [
            "E:DocIdSamples.Widget.Del.AnEvent",
        ];
        this.AssertMatchingDocIds(expected, this.reader.EventDefinitions.Select(e => (EntityHandle)e));
    }

    [Fact]
    public void Properties()
    {
        string[] expected = [
            "P:DocIdSamples.Widget.Width",
            "P:DocIdSamples.Widget.Item(System.Int32)",
            "P:DocIdSamples.Widget.Item(System.String,System.Int32)",
        ];
        this.AssertMatchingDocIds(expected, this.reader.PropertyDefinitions.Select(h => (EntityHandle)h));
    }

    [Fact]
    public void NoNamespace()
    {
        TypeDefinitionHandle selfHandle = this.reader.TypeDefinitions.Single(h => this.reader.StringComparer.Equals(this.reader.GetTypeDefinition(h).Name, nameof(DocIdBuilderTests)));
        Assert.Equal("T:DocIdBuilderTests", this.docIdBuilder.GetDocumentationCommentId(selfHandle));
    }

    private void AssertMatchingDocIds(string[] expectedDocIds, IEnumerable<EntityHandle> apis)
    {
        List<string> actualDocIds = new();
        foreach (EntityHandle handle in apis)
        {
            string? docId = this.docIdBuilder.GetDocumentationCommentId(handle);
            if (docId?.Contains("DocIdSamples") is true)
            {
                actualDocIds.Add(docId);
                this.logger.WriteLine(docId);
            }
        }

        Array.Sort(expectedDocIds, StringComparer.Ordinal);
        actualDocIds.Sort(StringComparer.Ordinal);

        Assert.Equal(expectedDocIds, actualDocIds);
    }
}
