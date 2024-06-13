// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft;

namespace AssemblyRefScanner;

/// <summary>
/// Parses a DocId into its component parts and a matcher.
/// </summary>
public class DocId
{
    /// <summary>
    /// Classifies the type of an API.
    /// </summary>
    public enum ApiKind
    {
        /// <summary>
        /// The API is a type.
        /// </summary>
        Type,

        /// <summary>
        /// The API is a method.
        /// </summary>
        Method,

        /// <summary>
        /// The API is a property.
        /// </summary>
        Property,

        /// <summary>
        /// The API is a field.
        /// </summary>
        Field,

        /// <summary>
        /// The API is an event.
        /// </summary>
        Event,
    }

    /// <summary>
    /// Parses a doc ID string into an object that can help find definitions of or references to the identified API.
    /// </summary>
    /// <param name="docId">The DocID that identifies the API to find references to.</param>
    /// <returns>An object that can identify the API.</returns>
    public static Descriptor Parse(string docId)
    {
        Requires.NotNullOrEmpty(docId);
        Requires.Argument(docId.Length > 2, nameof(docId), "DocId must be at least 3 characters long.");
        Requires.Argument(docId[1] == ':', nameof(docId), "Not a valid DocId.");

        return new Descriptor(docId);
    }

    /// <summary>
    /// Describes an API that can be identified by a DocID.
    /// </summary>
    /// <param name="docId">The DocID that identifies an API.</param>
    public class Descriptor(string docId)
    {
        /// <summary>
        /// Gets the kind of API that this DocID represents.
        /// </summary>
        public ApiKind Kind => docId[0] switch
        {
            'T' => ApiKind.Type,
            'M' => ApiKind.Method,
            'P' => ApiKind.Property,
            'F' => ApiKind.Field,
            'E' => ApiKind.Event,
            _ => throw new ArgumentException("Invalid DocId."),
        };

        /// <summary>
        /// Gets the DocID that this instance represents.
        /// </summary>
        protected string DocId => docId;

        /// <summary>
        /// Tests whether a given handle is to an API that defines or references the API identified by this DocID.
        /// </summary>
        /// <param name="handle">The handle to an API definition or reference.</param>
        /// <param name="reader">The metadata reader behind the <paramref name="handle"/>.</param>
        /// <returns>A value indicating whether it is a match.</returns>
        public virtual bool IsMatch(EntityHandle handle, MetadataReader reader)
        {
            // In this virtual method, we do the simple thing of just constructing a DocID for the candidate API
            // to see if it equals the DocID we are looking for.
            // But in an override, a more efficient parsing of the DocID could be done that compares the result
            // with the referenced entity to see if they are equal, with fewer or no allocations.
            DocIdBuilder builder = new(reader);
            string actualDocId = builder.GetDocumentationCommentId(handle);
            return actualDocId == docId;
        }
    }
}
