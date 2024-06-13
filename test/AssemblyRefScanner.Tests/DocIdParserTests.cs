// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

public class DocIdParserTests : IDisposable
{
    private readonly ITestOutputHelper logger;
    private readonly FileStream assemblyStream;
    private readonly PEReader peReader;
    private readonly MetadataReader reader;
    private readonly DocIdBuilder docIdBuilder;

    public DocIdParserTests(ITestOutputHelper logger)
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
    public void Parse_IsMatch_TypeDefinitions()
    {
        Dictionary<TypeDefinitionHandle, string> dict = this.reader.TypeDefinitions.ToDictionary(
            h => h,
            h => this.docIdBuilder.GetDocumentationCommentId(h));
        foreach ((TypeDefinitionHandle h, string docId) in dict)
        {
            this.logger.WriteLine(docId);
            DocId.Descriptor match = DocId.Parse(docId);
            Assert.Equal(DocId.ApiKind.Type, match.Kind);
            foreach ((TypeDefinitionHandle candidateHandle, string candidateDocId) in dict)
            {
                Assert.Equal(candidateHandle.Equals(h), match.IsMatch(candidateHandle, this.reader));
            }
        }
    }

    [Fact]
    public void Parse_IsMatch_MethodDefinitions()
    {
        Dictionary<MethodDefinitionHandle, string> dict = this.reader.MethodDefinitions.ToDictionary(
            h => h,
            h => this.docIdBuilder.GetDocumentationCommentId(h));
        foreach ((MethodDefinitionHandle h, string docId) in dict)
        {
            this.logger.WriteLine(docId);
            DocId.Descriptor match = DocId.Parse(docId);
            Assert.Equal(DocId.ApiKind.Method, match.Kind);
            foreach ((MethodDefinitionHandle candidateHandle, string candidateDocId) in dict)
            {
                Assert.Equal(candidateHandle.Equals(h), match.IsMatch(candidateHandle, this.reader));
            }
        }
    }

    [Fact]
    public void Parse_IsMatch_PropertyDefinitions()
    {
        Dictionary<PropertyDefinitionHandle, string> dict = this.reader.PropertyDefinitions.ToDictionary(
            h => h,
            h => this.docIdBuilder.GetDocumentationCommentId(h));
        foreach ((PropertyDefinitionHandle h, string docId) in dict)
        {
            this.logger.WriteLine(docId);
            DocId.Descriptor match = DocId.Parse(docId);
            Assert.Equal(DocId.ApiKind.Property, match.Kind);
            foreach ((PropertyDefinitionHandle candidateHandle, string candidateDocId) in dict)
            {
                Assert.Equal(candidateHandle.Equals(h), match.IsMatch(candidateHandle, this.reader));
            }
        }
    }

    [Fact]
    public void Parse_IsMatch_EventDefinitions()
    {
        Dictionary<EventDefinitionHandle, string> dict = this.reader.EventDefinitions.ToDictionary(
            h => h,
            h => this.docIdBuilder.GetDocumentationCommentId(h));
        foreach ((EventDefinitionHandle h, string docId) in dict)
        {
            this.logger.WriteLine(docId);
            DocId.Descriptor match = DocId.Parse(docId);
            Assert.Equal(DocId.ApiKind.Event, match.Kind);
            foreach ((EventDefinitionHandle candidateHandle, string candidateDocId) in dict)
            {
                Assert.Equal(candidateHandle.Equals(h), match.IsMatch(candidateHandle, this.reader));
            }
        }
    }

    [Fact]
    public void Parse_IsMatch_FieldDefinitions()
    {
        Dictionary<FieldDefinitionHandle, string> dict = this.reader.FieldDefinitions.ToDictionary(
            h => h,
            h => this.docIdBuilder.GetDocumentationCommentId(h));
        foreach ((FieldDefinitionHandle h, string docId) in dict)
        {
            this.logger.WriteLine(docId);
            DocId.Descriptor match = DocId.Parse(docId);
            Assert.Equal(DocId.ApiKind.Field, match.Kind);
            foreach ((FieldDefinitionHandle candidateHandle, string candidateDocId) in dict)
            {
                Assert.Equal(candidateHandle.Equals(h), match.IsMatch(candidateHandle, this.reader));
            }
        }
    }

    [Fact]
    public void Parse_IsMatch_TypeReferences()
    {
        Dictionary<TypeReferenceHandle, string> dict = this.reader.TypeReferences.ToDictionary(
            h => h,
            h => this.docIdBuilder.GetDocumentationCommentId(h));
        foreach ((TypeReferenceHandle h, string docId) in dict)
        {
            this.logger.WriteLine(docId);
            DocId.Descriptor match = DocId.Parse(docId);
            Assert.Equal(DocId.ApiKind.Type, match.Kind);
            foreach ((TypeReferenceHandle candidateHandle, string candidateDocId) in dict)
            {
                bool expectedMatch = this.docIdBuilder.GetDocumentationCommentId(candidateHandle) == docId;
                Assert.Equal(expectedMatch, match.IsMatch(candidateHandle, this.reader));
            }
        }
    }

    [Fact]
    public void Parse_IsMatch_MemberReferences()
    {
        Dictionary<MemberReferenceHandle, string> dict = this.reader.MemberReferences.ToDictionary(
            h => h,
            h => this.docIdBuilder.GetDocumentationCommentId(h));
        foreach ((MemberReferenceHandle h, string docId) in dict)
        {
            this.logger.WriteLine(docId);
            DocId.Descriptor match = DocId.Parse(docId);
            Assert.NotEqual(DocId.ApiKind.Type, match.Kind);
            foreach ((MemberReferenceHandle candidateHandle, string candidateDocId) in dict)
            {
                bool expectedMatch = this.docIdBuilder.GetDocumentationCommentId(candidateHandle) == docId;
                Assert.Equal(expectedMatch, match.IsMatch(candidateHandle, this.reader));
            }
        }
    }
}
