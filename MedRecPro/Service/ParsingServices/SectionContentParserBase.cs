using System.Xml.Linq;
using MedRecPro.DataAccess;
using MedRecPro.Models;
using MedRecPro.Data;
using MedRecPro.Helpers;
using Microsoft.EntityFrameworkCore;
using static MedRecPro.Models.Label;
#pragma warning disable CS8981 // The type name only contains lower-cased ascii characters. Such names may become reserved for the language.
using sc = MedRecPro.Models.SplConstants;
#pragma warning restore CS8981 // The type name only contains lower-cased ascii characters. Such names may become reserved for the language.
#pragma warning disable CS8981 // The type name only contains lower-cased ascii characters. Such names may become reserved for the language.
using c = MedRecPro.Models.Constant;
#pragma warning restore CS8981 // The type name only contains lower-cased ascii characters. Such names may become reserved for the language.

namespace MedRecPro.Service.ParsingServices
{
    /**************************************************************/
    /// <summary>
    /// Base class for section content parsing providing shared functionality
    /// used by both bulk and single-call parsing strategies. Contains only
    /// methods that are genuinely common to both implementation approaches.
    /// </summary>
    /// <remarks>
    /// This abstract base class encapsulates the minimal set of shared methods
    /// between bulk operations and single-call operations patterns. Strategy-specific
    /// methods, DTOs, and helper classes remain in the derived SectionContentParser class.
    /// </remarks>
    /// <seealso cref="SectionContentParser"/>
    /// <seealso cref="SectionTextContent"/>
    /// <seealso cref="TextList"/>
    /// <seealso cref="SplParseContext"/>
    public abstract class SectionContentParserBase
    {
        #region Protected Fields

        /// <summary>
        /// The XML namespace used for element parsing, derived from the constant configuration.
        /// </summary>
        /// <seealso cref="MedRecPro.Models.Constant"/>
        protected static readonly XNamespace ns = c.XML_NAMESPACE;

        /// <summary>
        /// Media parser for handling multimedia content within text blocks.
        /// </summary>
        /// <seealso cref="SectionMediaParser"/>
        protected readonly SectionMediaParser _mediaParser;

        #endregion

        #region Constructor

        /**************************************************************/
        /// <summary>
        /// Initializes a new instance of the SectionContentParserBase with required dependencies.
        /// </summary>
        /// <param name="mediaParser">Parser for handling multimedia content within text blocks.</param>
        /// <seealso cref="SectionMediaParser"/>
        protected SectionContentParserBase(SectionMediaParser? mediaParser = null)
        {
            _mediaParser = mediaParser ?? new SectionMediaParser();
        }

        #endregion

        #region Shared Validation Methods

        /**************************************************************/
        /// <summary>
        /// Validates the input parameters for text list processing.
        /// Used by both single-call and bulk-call list processing methods.
        /// </summary>
        /// <param name="listEl">The XElement representing the list.</param>
        /// <param name="sectionTextContentId">The section text content ID.</param>
        /// <param name="context">The parsing context.</param>
        /// <returns>True if all inputs are valid, false otherwise.</returns>
        /// <seealso cref="XElement"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="Label"/>
        protected static bool validateTextListInputs(XElement listEl, int sectionTextContentId, SplParseContext context)
        {
            #region implementation
            // Check for null or invalid parameters
            return listEl != null &&
                   sectionTextContentId > 0 &&
                   context?.ServiceProvider != null;
            #endregion
        }

        #endregion

        #region Shared Entity Creation Methods

        /**************************************************************/
        /// <summary>
        /// Creates a new TextList entity from the provided list element and section content ID.
        /// Used by both single-call (via getOrCreateTextListAsync) and bulk-call implementations.
        /// </summary>
        /// <param name="listEl">The XElement representing the list.</param>
        /// <param name="sectionTextContentId">The section text content ID.</param>
        /// <returns>A new TextList entity with populated attributes.</returns>
        /// <seealso cref="TextList"/>
        /// <seealso cref="XElement"/>
        /// <seealso cref="XElementExtensions"/>
        /// <seealso cref="Label"/>
        protected static TextList createTextListEntity(XElement listEl, int sectionTextContentId)
        {
            #region implementation
            // Extract list attributes and create new entity
            return new TextList
            {
                SectionTextContentID = sectionTextContentId,
                ListType = listEl.Attribute(sc.A.ListType)?.Value,
                StyleCode = listEl.Attribute(sc.A.StyleCode)?.Value
            };
            #endregion
        }

        #endregion

        #region Shared Highlight Processing Methods

        /**************************************************************/
        /// <summary>
        /// Finds or creates SectionExcerptHighlight records for all highlight text nodes
        /// within excerpt elements of a section, capturing the complete inner XML content
        /// including tables, lists, and paragraphs for database storage.
        /// Used by both single-call and bulk-call implementations via processSpecializedContentAsync.
        /// </summary>
        /// <param name="excerptEl">The XElement to search for excerpt/highlight/text patterns.</param>
        /// <param name="sectionId">The SectionID owning this highlight content.</param>
        /// <param name="context">Parsing context for repository and database access.</param>
        /// <returns>List of SectionExcerptHighlight objects (created or found).</returns>
        /// <seealso cref="SectionExcerptHighlight"/>
        /// <seealso cref="Section"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="XElementExtensions"/>
        /// <seealso cref="ApplicationDbContext"/>
        /// <seealso cref="Label"/>
        /// <remarks>
        /// This method must capture the complete inner XML of the text element,
        /// including complex nested structures like tables. The content is stored as raw XML
        /// in the database for later rendering.
        /// </remarks>
        protected async Task<List<SectionExcerptHighlight>> getOrCreateSectionExcerptHighlightsAsync(
            XElement excerptEl,
            int sectionId,
            SplParseContext context)
        {
            #region implementation
            var highlights = new List<SectionExcerptHighlight>();

            // Validate required input parameters
            if (excerptEl == null || sectionId <= 0)
                return highlights;

            // Validate required context dependencies
            if (context == null || context.Logger == null || context.ServiceProvider == null)
                return highlights;

            // Get database context and repository for section excerpt highlight operations
            var dbContext = context.GetDbContext();
            var repo = context.GetRepository<SectionExcerptHighlight>();
            var dbSet = dbContext.Set<SectionExcerptHighlight>();

            // Find all highlight elements directly under this excerpt
            // Use Elements (not Descendants) to avoid finding nested highlights in child structures
            var highlightElements = excerptEl.Elements(ns + sc.E.Highlight);

            foreach (var highlightEl in highlightElements)
            {
                // Get the text element within this specific highlight
                var textEl = highlightEl.Element(ns + sc.E.Text);

                if (textEl == null)
                {
                    // No text element - this shouldn't happen in valid SPL but handle gracefully
                    context.Logger?.LogWarning($"Highlight element without text child in SectionID {sectionId}");
                    continue;
                }

                // Extract the complete inner XML from the text element
                // This preserves tables, lists, paragraphs, and all nested structures
                string? txt = null;

                try
                {
                    // Get all child nodes of the text element and serialize them
                    var innerNodes = textEl.Nodes();

                    if (innerNodes != null && innerNodes.Any())
                    {
                        // Concatenate all inner XML preserving structure
                        txt = string
                            .Concat(innerNodes.Select(n => n.ToString()))
                            ?.NormalizeXmlWhitespace();
                    }
                    else
                    {
                        // Text element exists but has no children - check for direct text content
                        txt = textEl.Value?.Trim();
                    }
                }
                catch (Exception ex)
                {
                    context.Logger?.LogError(ex, $"Error extracting highlight XML for SectionID {sectionId}");
                    continue;
                }

                // Skip if no actual content was extracted
                if (string.IsNullOrWhiteSpace(txt))
                {
                    context.Logger?.LogWarning($"Empty highlight text extracted for SectionID {sectionId}");
                    continue;
                }

                // Dedupe: SectionID + HighlightText
                var existing = await dbSet
                    .Where(eh => eh.SectionID == sectionId && eh.HighlightText == txt)
                    .FirstOrDefaultAsync();

                if (existing != null)
                {
                    highlights.Add(existing);
                    continue;
                }

                // Create new section excerpt highlight with the complete XML content
                var newHighlight = new SectionExcerptHighlight
                {
                    SectionID = sectionId,
                    HighlightText = txt
                };

                await repo.CreateAsync(newHighlight);
                highlights.Add(newHighlight);

                context.Logger?.LogInformation($"Created SectionExcerptHighlight for SectionID {sectionId} with {txt.Length} characters");
            }

            return highlights;
            #endregion
        }

        #endregion

        #region Shared Static Helper Methods

        /**************************************************************/
        /// <summary>
        /// Parses and saves a Section entity from an XElement, extracting metadata and establishing
        /// relationships with the structured body context. Performs deduplication based on section GUID.
        /// Used by both single-call and bulk-call implementations as a delegate parameter.
        /// </summary>
        /// <param name="sectionEl">The XElement representing the section to parse.</param>
        /// <param name="context">Parsing context containing structured body and repository access.</param>
        /// <returns>The Section entity (created or existing) with populated metadata.</returns>
        /// <exception cref="ArgumentNullException">Thrown when sectionEl or context is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when structured body context is invalid or section GUID is missing.</exception>
        /// <seealso cref="Section"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="XElementExtensions"/>
        /// <seealso cref="ApplicationDbContext"/>
        /// <seealso cref="Label"/>
        protected static async Task<Section> parseAndSaveSectionAsync(XElement sectionEl, SplParseContext context)
        {
            #region implementation
            // Validate required input parameters
            if (sectionEl == null)
                throw new ArgumentNullException(nameof(sectionEl));
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            if (context.StructuredBody?.StructuredBodyID == null)
                throw new InvalidOperationException("No valid structured body context.");

            int? documentId = context.StructuredBody.DocumentID;

            // Get section GUID
            // Extract unique identifier for section from XML element
            var sectionGuidStr = sectionEl.GetSplElementAttrVal(sc.E.Id, sc.A.Root);
            if (!Guid.TryParse(sectionGuidStr, out var sectionGuid) || sectionGuid == Guid.Empty)
                throw new InvalidOperationException("Section <id root> is missing or not a valid GUID.");

            // Get repo/db
            // Get database context and repository for section operations
            var dbContext = context!.ServiceProvider!.GetRequiredService<ApplicationDbContext>();
            var sectionRepo = context.GetRepository<Section>();
            var sectionDbSet = dbContext.Set<Section>();

            // Deduplicate by GUID and StructuredBodyID (or just by GUID if global)
            // Search for existing section with matching GUID and structured body context
            var existing = await sectionDbSet
                .FirstOrDefaultAsync(s => s.SectionGUID == sectionGuid && s.StructuredBodyID == context.StructuredBody.StructuredBodyID.Value);

            // Return existing section if found to avoid duplicates
            if (existing != null)
                return existing;

            // Extract additional metadata
            // Parse section code information and metadata from XML attributes
            var sectionCode = sectionEl.GetSplElementAttrVal(sc.E.Code, sc.A.CodeValue);
            var sectionLinkGuid = sectionEl.GetAttrVal(sc.A.ID);
            var sectionCodeSystem = sectionEl.GetSplElementAttrVal(sc.E.Code, sc.A.CodeSystem);
            var sectionCodeSystemName = sectionEl.GetSplElementAttrVal(sc.E.Code, sc.A.CodeSystemName);
            var sectionDisplayName = sectionEl.GetSplElementAttrVal(sc.E.Code, sc.A.DisplayName) ?? string.Empty;
            var sectionTitle = sectionEl.GetSplElementVal(sc.E.Title)?.Trim();

            // Parse effective time with fallback to minimum value if not present
            var sectionEffectiveTime = Util.ParseNullableDateTime(sectionEl.GetSplElementAttrVal(sc.E.EffectiveTime, sc.A.Value) ?? string.Empty) ?? DateTime.MinValue;

            // Create new Section entity
            // Build new section entity with all extracted metadata and context relationships
            var newSection = new Section
            {
                DocumentID = documentId,
                StructuredBodyID = context.StructuredBody.StructuredBodyID.Value,
                SectionGUID = sectionGuid,
                SectionCode = sectionCode,
                SectionCodeSystem = sectionCodeSystem,
                SectionCodeSystemName = sectionCodeSystemName,
                SectionDisplayName = sectionDisplayName,
                Title = sectionTitle,
                EffectiveTime = sectionEffectiveTime
            };

            // Persist new section to database and return
            await sectionRepo.CreateAsync(newSection);
            return newSection;
            #endregion
        }

        #endregion
    }
}