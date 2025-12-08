
using MedRecPro.Data;
using MedRecPro.Helpers;
using MedRecPro.Models;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Diagnostics;
using static MedRecPro.Models.Label;
using Cached = MedRecPro.Helpers.PerformanceHelper;

namespace MedRecPro.DataAccess
{
    /// <summary>
    /// Provides helper methods for building Data Transfer Objects (DTOs) from SPL Label entities.
    /// Constructs complete hierarchical data structures representing medical product documents
    /// and their associated metadata, relationships, and compliance information.
    /// </summary>
    /// <seealso cref="Label"/>
    /// <seealso cref="DocumentDto"/>
    public static partial class DtoLabelAccess
    {
        #region Content Hierarchy Builders
        /**************************************************************/
        /// <summary>
        /// Builds a list of Section DTOs for the specified structured body with all 
        /// nested collections. Constructs comprehensive section data including 
        /// products, highlights, media, substances, and various specialized content.
        /// </summary>
        /// <param name="db">The database context.</param>
        /// <param name="structuredBodyId">The structured body ID to find sections for.</param>
        /// <param name="pkSecret">Secret used for ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <returns>List of Section DTOs with complete nested collections.</returns>
        /// <seealso cref="Label.Section"/>
        private static async Task<List<SectionDto>> buildSectionsAsync(ApplicationDbContext db, int? structuredBodyId, string pkSecret, ILogger logger)
        {
            #region implementation
            if (structuredBodyId == null) return new List<SectionDto>();

            // Get all sections for this structured body
            var sections = await db.Set<Label.Section>()
                .AsNoTracking()
                .Where(s => s.StructuredBodyID == structuredBodyId)
                .ToListAsync();

            var sectionDtos = new List<SectionDto>();

            // For each section, build all its nested collections
            foreach (var section in sections)
            {
                // Build all child collections for this section
                var billingUnitIndexes = await buildBillingUnitIndexesAsync(db, section.SectionID, pkSecret, logger);

                var children = await buildChildSectionHierarchyDtoAsync(db, section.SectionID, pkSecret, logger) ?? new List<SectionHierarchyDto>();

                var content = await buildSectionTextContentDtoAsync(db, section.SectionID, pkSecret, logger)
                    ?? new List<SectionTextContentDto>();

                var highlights = await buildSectionExcerptHighlightsAsync(db, section.SectionID, pkSecret, logger);

                var identifiedSubstances = await buildIdentifiedSubstancesAsync(db, section.SectionID, pkSecret, logger);

                var interactionIssues = await buildInteractionIssuesAsync(db, section.SectionID, pkSecret, logger);

                var media = await buildObservationMediaAsync(db, section.SectionID, pkSecret, logger);

                var NCTLinks = await buildNCTLinkDtoAsync(db, section.SectionID, pkSecret, logger);

                var parents = await buildParentSectionHierarchyDtoAsync(db, section.SectionID, pkSecret, logger) ?? new List<SectionHierarchyDto>();

                var productConcepts = await buildProductConceptsAsync(db, section.SectionID, pkSecret, logger);

                var products = await buildProductsAsync(db, section.SectionID, pkSecret, logger);

                var protocols = await buildProtocolsAsync(db, section.SectionID, pkSecret, logger);

                var remsMaterials = await buildREMSMaterialsAsync(db, section.SectionID, pkSecret, logger);

                var remsResources = await buildREMSElectronicResourcesAsync(db, section.SectionID, pkSecret, logger);

                var warningLetterDates = await buildWarningLetterDatesAsync(db, section.SectionID, pkSecret, logger);

                var warningLetterInfos = await buildWarningLetterProductInfosAsync(db, section.SectionID, pkSecret, logger);


#if DEBUG
                //logSectionTextContentDebugInfo(content);
                //logSectionObservationMediaDebugInfo(media);
#endif

                // Assemble complete section DTO with all nested data
                sectionDtos.Add(new SectionDto(pkSecret)
                {
                    BillingUnitIndexes = billingUnitIndexes,
                    ChildSectionHierarchies = children,
                    ExcerptHighlights = highlights,
                    IdentifiedSubstances = identifiedSubstances,
                    InteractionIssues = interactionIssues,
                    NCTLinks = NCTLinks,
                    ObservationMedia = media,
                    ParentSectionHierarchies = parents,
                    ProductConcepts = productConcepts,
                    Products = products,
                    Protocols = protocols,
                    REMSElectronicResources = remsResources,
                    REMSMaterials = remsMaterials,
                    Section = section.ToEntityWithEncryptedId(pkSecret, logger),
                    TextContents = content,
                    WarningLetterDates = warningLetterDates,
                    WarningLetterProductInfos = warningLetterInfos
                });
            }
            return sectionDtos;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Diagnostic logging for SectionTextContentDto and its RenderedMedias.
        /// </summary>
        /// <param name="content"></param>
        private static void logSectionTextContentDebugInfo(List<SectionTextContentDto> content)
        {
            #region implementation
            // DIAGNOSTIC: Log what was built
            if (content != null && content.Any())
            {
                Debug.WriteLine($"=== buildSectionTextContentDtoAsync Results ===");
                Debug.WriteLine($"Total TextContent records: {content.Count}");

                foreach (var tc in content)
                {
                    Debug.WriteLine($"\nTextContent ID={tc.SectionTextContentID}");
                    Debug.WriteLine($"  ContentType: {tc.ContentType}");
                    Debug.WriteLine($"  SequenceNumber: {tc.SequenceNumber}");
                    Debug.WriteLine($"  RenderedMedias count: {tc.RenderedMedias?.Count ?? 0}");

                    if (tc.RenderedMedias?.Any() == true)
                    {
                        foreach (var rm in tc.RenderedMedias)
                        {
                            Debug.WriteLine($"    RenderedMedia ID={rm.RenderedMediaID}");
                            Debug.WriteLine($"      ObservationMediaID={rm.ObservationMediaID}");
                            Debug.WriteLine($"      SequenceInContent={rm.SequenceInContent}");
                        }
                    }
                }
                Debug.WriteLine($"=== End buildSectionTextContentDtoAsync ===");
            }
            #endregion

        }

        /**************************************************************/
        /// <summary>
        /// Debug logging for ObservationMediaDto list.
        /// </summary>
        /// <param name="media"></param>
        private static void logSectionObservationMediaDebugInfo(List<ObservationMediaDto> media)
        {
            #region implementation
            // DIAGNOSTIC: Log what was built
            if (media != null && media.Any())
            {
                Debug.WriteLine($"=== buildObservationMediaAsync Results ===");
                Debug.WriteLine($"Total ObservationMedia records: {media.Count}");
                foreach (var om in media)
                {
                    Debug.WriteLine($"\nObservationMedia ID={om.ObservationMediaID}");
                    Debug.WriteLine($"  MediaID: {om.MediaID}");
                    Debug.WriteLine($"  FileName: {om.FileName}");
                }
                Debug.WriteLine($"=== End buildObservationMediaAsync ===");
            }
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds the NCTLink DTOs for the specified section.
        /// </summary>
        /// <param name="db">The database context.</param>
        /// <param name="sectionID">The section ID to find NCT links for.</param>
        /// <param name="pkSecret">Secret used for ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <returns>List of NCTLink DTOs for the section.</returns>
        /// <seealso cref="Label.NCTLink"/>
        /// <seealso cref="NCTLinkDto"/>
        /// <seealso cref="SectionDto"/>
        private static async Task<List<NCTLinkDto>> buildNCTLinkDtoAsync(ApplicationDbContext db, int? sectionID, string pkSecret, ILogger logger)
        {
            #region implementation
            // Return empty list if no section ID is provided
            if (sectionID == null)
                return new List<NCTLinkDto>();

            // Query NCT links for the specified section
            var entity = await db.Set<Label.NCTLink>()
                .AsNoTracking()
                .Where(e => e.SectionID == sectionID)
                .ToListAsync();

            // Return empty list if no entities found
            if (entity == null)
                return new List<NCTLinkDto>();

            // Transform entities to DTOs with encrypted IDs
            return entity.Select(entity => new NCTLinkDto
            {
                NCTLink = entity.ToEntityWithEncryptedId(pkSecret, logger)
            }).ToList() ?? new List<NCTLinkDto>();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds a list of section hierarchy DTOs where the specified section ID is the parent.
        /// SectionHierarchy contains ParentSectionID and ChildSectionID relationships.
        /// </summary>
        /// <param name="db">The database context for querying section hierarchy entities.</param>
        /// <param name="sectionID">The section identifier used as parent section ID to filter section hierarchies.</param>
        /// <param name="pkSecret">The secret key used for encrypting entity IDs.</param>
        /// <param name="logger">The logger instance for tracking operations.</param>
        /// <returns>A list of SectionHierarchyDto objects representing parent relationships, or null if no section ID provided or no entities found.</returns>
        /// <seealso cref="Label.SectionHierarchy"/>
        /// <seealso cref="SectionHierarchyDto"/>
        private static async Task<List<SectionHierarchyDto>?> buildParentSectionHierarchyDtoAsync(ApplicationDbContext db, int? sectionID, string pkSecret, ILogger logger)
        {
            #region implementation
            // Return null if no section ID is provided
            if (sectionID == null)
                return null;

            // Query section hierarchies where the specified section is the parent
            var entity = await db.Set<Label.SectionHierarchy>()
                .AsNoTracking()
                .Where(e => e.ParentSectionID == sectionID)
                .ToListAsync();

            // Return null if no entities found
            if (entity == null)
                return null;

            // Transform entities to DTOs with encrypted IDs
            return entity.Select(entity => new SectionHierarchyDto(pkSecret)
            {
                SectionHierarchy = entity.ToEntityWithEncryptedId(pkSecret, logger)
            }).ToList();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds a list of section hierarchy DTOs where the specified section ID is the child.
        /// SectionHierarchy contains ParentSectionID and ChildSectionID relationships.
        /// </summary>
        /// <param name="db">The database context for querying section hierarchy entities.</param>
        /// <param name="sectionID">The section identifier used as child section ID to filter section hierarchies.</param>
        /// <param name="pkSecret">The secret key used for encrypting entity IDs.</param>
        /// <param name="logger">The logger instance for tracking operations.</param>
        /// <returns>A list of SectionHierarchyDto objects representing child relationships, or null if no section ID provided or no entities found.</returns>
        /// <seealso cref="Label.SectionHierarchy"/>
        /// <seealso cref="SectionHierarchyDto"/>
        private static async Task<List<SectionHierarchyDto>?> buildChildSectionHierarchyDtoAsync(ApplicationDbContext db, int? sectionID, string pkSecret, ILogger logger)
        {
            #region implementation
            // Return null if no section ID is provided
            if (sectionID == null)
                return null;

            // Query section hierarchies where the specified section is the child
            var entity = await db.Set<Label.SectionHierarchy>()
                .AsNoTracking()
                .Where(e => e.ChildSectionID == sectionID)
                .ToListAsync();

            // Return null if no entities found
            if (entity == null)
                return null;

            // Transform entities to DTOs with encrypted IDs
            return entity.Select(entity => new SectionHierarchyDto
            {
                SectionHierarchy = entity.ToEntityWithEncryptedId(pkSecret, logger)
            }).ToList();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds a list of section text content DTOs for a specified section ID.
        /// SectionID is a foreign key to the Section table.
        /// </summary>
        /// <param name="db">The database context for querying section text content entities.</param>
        /// <param name="sectionID">The section identifier to filter section text content.</param>
        /// <param name="pkSecret">The secret key used for encrypting entity IDs.</param>
        /// <param name="logger">The logger instance for tracking operations.</param>
        /// <returns>A list of SectionTextContentDto objects, or null if no section ID provided or no entities found.</returns>
        /// <seealso cref="Label.SectionTextContent"/>
        /// <seealso cref="SectionTextContentDto"/>
        private static async Task<List<SectionTextContentDto>?> buildSectionTextContentDtoAsync(ApplicationDbContext db, int? sectionID, string pkSecret, ILogger logger)
        {
            #region implementation
            // Return null if no section ID is provided
            if (sectionID == null)
                return null;

            List<SectionTextContentDto> sectionTextContentDtos = new List<SectionTextContentDto>();

            // Query section text content for the specified section
            var entity = await db.Set<Label.SectionTextContent>()
                .AsNoTracking()
                .Where(e => e.SectionID == sectionID)
                .ToListAsync();

            // Return null if no entities found
            if (entity == null)
                return null;

            foreach (var e in entity)
            {
                // Build all child collections for this section text content
                var renderedMedias = await buildRenderedMediasAsync(db, e.SectionTextContentID, pkSecret, logger);

                var textTables = await buildTextTablesAsync(db, e.SectionTextContentID, pkSecret, logger);

                var textLists = await buildTextListsAsync(db, e.SectionTextContentID, pkSecret, logger);

                // Create SectionTextContentDto with encrypted ID and nested collections
                sectionTextContentDtos.Add(new SectionTextContentDto
                {
                    SectionTextContent = e.ToEntityWithEncryptedId(pkSecret, logger),
                    RenderedMedias = renderedMedias,
                    TextTables = textTables,
                    TextLists = textLists
                });
            }

            // Return DTOs with encrypted IDs
            return sectionTextContentDtos;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Represents the renderMultimedia tag, linking text content to an ObservationMedia entry.
        /// Builds rendered media DTOs for the specified section text content.
        /// </summary>
        /// <param name="db">The database context.</param>
        /// <param name="sectionTextContentID">The section text content ID to find rendered media for.</param>
        /// <param name="pkSecret">Secret used for ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <returns>List of rendered media DTOs for the section text content.</returns>
        /// <seealso cref="Label.RenderedMedia"/>
        /// <seealso cref="RenderedMediaDto"/>
        /// <seealso cref="SectionTextContentDto"/>
        private static async Task<List<RenderedMediaDto>> buildRenderedMediasAsync(ApplicationDbContext db, int? sectionTextContentID, string pkSecret, ILogger logger)
        {
            #region implementation
            // Return empty list if no section text content ID is provided
            if (sectionTextContentID == null)
                return new List<RenderedMediaDto>();

            // Query rendered media for the specified section text content
            var entity = await db.Set<Label.RenderedMedia>()
                .AsNoTracking()
                .Where(e => e.SectionTextContentID == sectionTextContentID)
                .ToListAsync();

            // Return empty list if no entities found
            if (entity == null)
                return new List<RenderedMediaDto>();

            // Transform entities to DTOs with encrypted IDs
            return entity.Select(e => new RenderedMediaDto
            {
                RenderedMedia = e.ToEntityWithEncryptedId(pkSecret, logger)
            }).ToList() ?? new List<RenderedMediaDto>();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Stores details specific to table elements.
        /// Builds text table DTOs with their associated columns and rows for the specified section text content.
        /// </summary>
        /// <param name="db">The database context.</param>
        /// <param name="sectionTextContentID">The section text content ID to find text tables for.</param>
        /// <param name="pkSecret">Secret used for ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <returns>List of text table DTOs with nested column and row data for the section text content.</returns>
        /// <seealso cref="Label.TextTable"/>
        /// <seealso cref="TextTableDto"/>
        /// <seealso cref="SectionTextContentDto"/>
        private static async Task<List<TextTableDto>> buildTextTablesAsync(ApplicationDbContext db, int? sectionTextContentID, string pkSecret, ILogger logger)
        {
            #region implementation
            // Return empty list if no section text content ID is provided
            if (sectionTextContentID == null)
                return new List<TextTableDto>();

            var dtos = new List<TextTableDto>();

            // Query text tables for the specified section text content
            var entity = await db.Set<Label.TextTable>()
                .AsNoTracking()
                .Where(e => e.SectionTextContentID == sectionTextContentID)
                .ToListAsync();

            // Return empty list if no entities found
            if (entity == null)
                return new List<TextTableDto>();

            // Build DTOs with nested column and row data for each text table
            foreach (var e in entity)
            {
                // Build all columns for this text table
                var columns = await buildTextTableColumnsAsync(db, e.TextTableID, pkSecret, logger);

                // Build all rows for this text table
                var rows = await buildTextTableRowsAsync(db, e.TextTableID, pkSecret, logger);

                dtos.Add(new TextTableDto
                {
                    TextTable = e.ToEntityWithEncryptedId(pkSecret, logger),
                    TextTableColumns = columns,
                    TextTableRows = rows
                });
            }

            // Return completed DTOs
            return dtos ?? new List<TextTableDto>();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Stores details specific to individual col elements.
        /// Builds text table column DTOs for the specified text table.
        /// </summary>
        /// <param name="db">The database context.</param>
        /// <param name="textTableID">The text table ID to find columns for.</param>
        /// <param name="pkSecret">Secret used for ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <returns>List of text table column DTOs for the text table.</returns>
        /// <seealso cref="Label.TextTableColumn"/>
        /// <seealso cref="TextTableColumnDto"/>
        /// <seealso cref="TextTableDto"/>
        private static async Task<List<TextTableColumnDto>> buildTextTableColumnsAsync(ApplicationDbContext db, int? textTableID, string pkSecret, ILogger logger)
        {
            #region implementation
            // Return empty list if no text table ID is provided
            if (textTableID == null)
                return new List<TextTableColumnDto>();

            // Query text table columns for the specified text table
            var entity = await db.Set<Label.TextTableColumn>()
                .AsNoTracking()
                .Where(e => e.TextTableID == textTableID)
                .ToListAsync();

            // Return empty list if no entities found
            if (entity == null)
                return new List<TextTableColumnDto>();

            // Transform entities to DTOs with encrypted IDs
            return entity.Select(e => new TextTableColumnDto
            {
                TextTableColumn = e.ToEntityWithEncryptedId(pkSecret, logger)
            }).ToList() ?? new List<TextTableColumnDto>();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Stores details specific to individual tr elements.
        /// Builds text table row DTOs with their associated cells for the specified text table.
        /// </summary>
        /// <param name="db">The database context.</param>
        /// <param name="textTableID">The text table ID to find rows for.</param>
        /// <param name="pkSecret">Secret used for ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <returns>List of text table row DTOs with nested cell data for the text table.</returns>
        /// <seealso cref="Label.TextTableRow"/>
        /// <seealso cref="TextTableRowDto"/>
        /// <seealso cref="TextTableDto"/>
        private static async Task<List<TextTableRowDto>> buildTextTableRowsAsync(ApplicationDbContext db, int? textTableID, string pkSecret, ILogger logger)
        {
            #region implementation
            // Return empty list if no text table ID is provided
            if (textTableID == null)
                return new List<TextTableRowDto>();

            var dtos = new List<TextTableRowDto>();

            // Query text table rows for the specified text table
            var entity = await db.Set<Label.TextTableRow>()
                .AsNoTracking()
                .Where(e => e.TextTableID == textTableID)
                .ToListAsync();

            // Return empty list if no entities found
            if (entity == null)
                return new List<TextTableRowDto>();

            // Build DTOs with nested cell data for each text table row
            foreach (var e in entity)
            {
                // Build all cells for this text table row
                var cells = await buildTextTableCellsAsync(db, e.TextTableRowID, pkSecret, logger);

                dtos.Add(new TextTableRowDto
                {
                    TextTableRow = e.ToEntityWithEncryptedId(pkSecret, logger),
                    TextTableCells = cells
                });
            }

            // Return completed DTOs
            return dtos ?? new List<TextTableRowDto>();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Stores details specific to individual td elements.
        /// Builds text table cell DTOs for the specified text table row.
        /// </summary>
        /// <param name="db">The database context.</param>
        /// <param name="textTableRowID">The text table row ID to find cells for.</param>
        /// <param name="pkSecret">Secret used for ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <returns>List of text table cell DTOs for the text table row.</returns>
        /// <seealso cref="Label.TextTableCell"/>
        /// <seealso cref="TextTableCellDto"/>
        /// <seealso cref="TextTableRowDto"/>
        private static async Task<List<TextTableCellDto>> buildTextTableCellsAsync(ApplicationDbContext db, int? textTableRowID, string pkSecret, ILogger logger)
        {
            #region implementation
            // Return empty list if no text table row ID is provided
            if (textTableRowID == null)
                return new List<TextTableCellDto>();

            // Query text table cells for the specified text table row
            var entity = await db.Set<Label.TextTableCell>()
                .AsNoTracking()
                .Where(e => e.TextTableRowID == textTableRowID)
                .ToListAsync();

            // Return empty list if no entities found
            if (entity == null)
                return new List<TextTableCellDto>();

            // Transform entities to DTOs with encrypted IDs
            return entity.Select(e => new TextTableCellDto
            {
                TextTableCell = e.ToEntityWithEncryptedId(pkSecret, logger)
            }).ToList() ?? new List<TextTableCellDto>();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Stores details specific to list elements.
        /// Builds text list DTOs with their associated items for the specified section text content.
        /// </summary>
        /// <param name="db">The database context.</param>
        /// <param name="sectionTextContentID">The section text content ID to find text lists for.</param>
        /// <param name="pkSecret">Secret used for ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <returns>List of text list DTOs with nested item data for the section text content.</returns>
        /// <seealso cref="Label.TextList"/>
        /// <seealso cref="TextListDto"/>
        /// <seealso cref="SectionTextContentDto"/>
        private static async Task<List<TextListDto>> buildTextListsAsync(ApplicationDbContext db, int? sectionTextContentID, string pkSecret, ILogger logger)
        {
            #region implementation
            // Return empty list if no section text content ID is provided
            if (sectionTextContentID == null)
                return new List<TextListDto>();

            var dtos = new List<TextListDto>();

            // Query text lists for the specified section text content
            var entity = await db.Set<Label.TextList>()
                .AsNoTracking()
                .Where(e => e.SectionTextContentID == sectionTextContentID)
                .ToListAsync();

            // Return empty list if no entities found
            if (entity == null)
                return new List<TextListDto>();

            // Build DTOs with nested item data for each text list
            foreach (var e in entity)
            {
                // Build all items for this text list
                var items = await buildTextListItemsAsync(db, e.TextListID, pkSecret, logger);

                dtos.Add(new TextListDto
                {
                    TextList = e.ToEntityWithEncryptedId(pkSecret, logger),
                    TextListItems = items
                });
            }

            // Return completed DTOs
            return dtos ?? new List<TextListDto>();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Stores details specific to list item elements.
        /// Builds text list item DTOs for the specified text list.
        /// </summary>
        /// <param name="db">The database context.</param>
        /// <param name="textListID">The text list ID to find items for.</param>
        /// <param name="pkSecret">Secret used for ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <returns>List of text list item DTOs for the text list.</returns>
        /// <seealso cref="Label.TextListItem"/>
        /// <seealso cref="TextListItemDto"/>
        /// <seealso cref="TextListDto"/>
        private static async Task<List<TextListItemDto>?> buildTextListItemsAsync(ApplicationDbContext db, int? textListID, string pkSecret, ILogger logger)
        {
            #region implementation
            // Return empty list if no text list ID is provided
            if (textListID == null)
                return new List<TextListItemDto>();

            // Query text list items for the specified text list
            var entity = await db.Set<Label.TextListItem>()
                .AsNoTracking()
                .Where(e => e.TextListID == textListID)
                .ToListAsync();

            // Return empty list if no entities found
            if (entity == null)
                return new List<TextListItemDto>();

            // Transform entities to DTOs with encrypted IDs
            return entity.Select(e => new TextListItemDto
            {
                TextListItem = e.ToEntityWithEncryptedId(pkSecret, logger)
            }).ToList() ?? new List<TextListItemDto>();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds a list of SectionExcerptHighlight DTOs for the specified section.
        /// Retrieves highlighted text content within excerpt sections such as Boxed Warnings and Indications.
        /// </summary>
        /// <param name="db">The database context.</param>
        /// <param name="sectionId">The section ID to find excerpt highlights for.</param>
        /// <param name="pkSecret">Secret used for ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <returns>List of SectionExcerptHighlight DTOs with encrypted IDs.</returns>
        /// <seealso cref="Label.SectionExcerptHighlight"/>
        private static async Task<List<SectionExcerptHighlightDto>> buildSectionExcerptHighlightsAsync(ApplicationDbContext db, int? sectionId, string pkSecret, ILogger logger)
        {
            #region implementation
            if (sectionId == null) return new List<SectionExcerptHighlightDto>();

            // Query excerpt highlights for the specified section
            var items = await db.Set<Label.SectionExcerptHighlight>().AsNoTracking().Where(e => e.SectionID == sectionId).ToListAsync();

            // Transform entities to DTOs with encrypted IDs
            return items.Select(item => new SectionExcerptHighlightDto { SectionExcerptHighlight = item.ToEntityWithEncryptedId(pkSecret, logger) }).ToList();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds a list of ObservationMedia DTOs for the specified section.
        /// Retrieves image and media metadata associated with section content.
        /// </summary>
        /// <param name="db">The database context.</param>
        /// <param name="sectionId">The section ID to find observation media for.</param>
        /// <param name="pkSecret">Secret used for ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <returns>List of ObservationMedia DTOs with encrypted IDs.</returns>
        /// <seealso cref="Label.ObservationMedia"/>
        private static async Task<List<ObservationMediaDto>> buildObservationMediaAsync(ApplicationDbContext db, int? sectionId, string pkSecret, ILogger logger)
        {
            #region implementation
            if (sectionId == null) return new List<ObservationMediaDto>();

            // Query observation media for the specified section
            var items = await db.Set<Label.ObservationMedia>()
                .AsNoTracking()
                .Where(e => e.SectionID == sectionId)
                .ToListAsync();

            // Transform entities to DTOs with encrypted IDs
            return items
                .Select(item => new ObservationMediaDto { ObservationMedia = item.ToEntityWithEncryptedId(pkSecret, logger) })
                .ToList();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds a list of ProductConcept DTOs for the specified section.
        /// Retrieves abstract or application-specific product/kit concept definitions.
        /// </summary>
        /// <param name="db">The database context.</param>
        /// <param name="sectionId">The section ID to find product concepts for.</param>
        /// <param name="pkSecret">Secret used for ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <returns>List of ProductConcept DTOs with encrypted IDs.</returns>
        /// <seealso cref="Label.ProductConcept"/>
        private static async Task<List<ProductConceptDto>> buildProductConceptsAsync(ApplicationDbContext db, int? sectionId, string pkSecret, ILogger logger)
        {
            #region implementation
            if (sectionId == null) return new List<ProductConceptDto>();

            var dtos = new List<ProductConceptDto>();

            // Query product concepts for the specified section
            var items = await db.Set<Label.ProductConcept>()
                .AsNoTracking()
                .Where(e => e.SectionID == sectionId)
                .ToListAsync();

            if (items == null || !items.Any())
                return new List<ProductConceptDto>();

            foreach (var item in items)
            {
                // Build all equivalences for this product concept
                var equivalences = await buildProductConceptEquivalencesAsync(db, item.ProductConceptID, pkSecret, logger);

                dtos.Add(new ProductConceptDto
                {
                    ProductConcept = item.ToEntityWithEncryptedId(pkSecret, logger),
                    ProductConceptEquivalences = equivalences
                });
            }

            // Transform entities to DTOs with encrypted IDs
            return dtos ?? new List<ProductConceptDto>();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Links an Application Product Concept to its corresponding 
        /// Abstract Product Concept. Retrieves product concept equivalences 
        /// for a specified product concept ID and transforms them into DTOs 
        /// with encrypted identifiers.
        /// </summary>
        /// <param name="db">The application database context for data access operations</param>
        /// <param name="productConceptID">The unique identifier of the product concept to find equivalences for. Returns empty list if null</param>
        /// <param name="pkSecret">The private key secret used for encrypting entity identifiers in the returned DTOs</param>
        /// <param name="logger">The logger instance for recording operations and potential errors during processing</param>
        /// <returns>A list of ProductConceptEquivalenceDto objects representing the equivalences, or an empty list if none found</returns>
        /// <remarks>
        /// This method follows the data flow: SectionDto > ProductConceptDto > ProductConceptEquivalenceDto
        /// The method uses AsNoTracking() for read-only operations to improve performance.
        /// All returned DTOs contain encrypted IDs for security purposes.
        /// </remarks>
        /// <example>
        /// <code>
        /// var equivalences = await buildProductConceptEquivalencesAsync(dbContext, 123, secretKey, logger);
        /// </code>
        /// </example>
        /// <seealso cref="Label.ProductConceptEquivalence"/>
        /// <seealso cref="ProductConceptEquivalenceDto"/>
        private static async Task<List<ProductConceptEquivalenceDto>> buildProductConceptEquivalencesAsync(ApplicationDbContext db, int? productConceptID, string pkSecret, ILogger logger)
        {
            #region implementation
            // Early return if no product concept ID provided
            if (productConceptID == null) return new List<ProductConceptEquivalenceDto>();

            // Query for equivalents of the specified product concept using read-only tracking
            var items = await db.Set<Label.ProductConceptEquivalence>()
                .AsNoTracking()
                .Where(e => e.ProductConceptEquivalenceID == productConceptID)
                .ToListAsync();

            // Return empty list if no equivalences found
            if (items == null || !items.Any())
                return new List<ProductConceptEquivalenceDto>();

            // Transform entities to DTOs with encrypted IDs for security
            return items
                .Select(item => new ProductConceptEquivalenceDto { ProductConcept = item.ToEntityWithEncryptedId(pkSecret, logger) })
                .ToList();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds a list of InteractionIssue DTOs for the specified section.
        /// Retrieves drug interaction issues and their associated details.
        /// </summary>
        /// <param name="db">The database context.</param>
        /// <param name="sectionId">The section ID to find interaction issues for.</param>
        /// <param name="pkSecret">Secret used for ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <returns>List of InteractionIssue DTOs with encrypted IDs.</returns>
        /// <seealso cref="Label.InteractionIssue"/>
        private static async Task<List<InteractionIssueDto>> buildInteractionIssuesAsync(ApplicationDbContext db, int? sectionId, string pkSecret, ILogger logger)
        {
            #region implementation
            if (sectionId == null) return new List<InteractionIssueDto>();

            var dtos = new List<InteractionIssueDto>();

            // Query interaction issues for the specified section
            var items = await db.Set<Label.InteractionIssue>()
                .AsNoTracking()
                .Where(e => e.SectionID == sectionId)
                .ToListAsync();

            if (items == null || !items.Any())
                return new List<InteractionIssueDto>();

            foreach (var item in items)
            {
                // Build all consequences for this interaction issue
                var consequences = await buildInteractionConsequencesAsync(db, item.InteractionIssueID, pkSecret, logger);

                dtos.Add(new InteractionIssueDto
                {
                    InteractionIssue = item.ToEntityWithEncryptedId(pkSecret, logger),
                    InteractionConsequences = consequences
                });
            }

            // return DTOs with encrypted IDs
            return dtos ?? new List<InteractionIssueDto>();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds the list of consequences from an interaction issue.
        /// Retrieves interaction consequences associated with a 
        /// specified interaction issue ID and transforms them into DTOs 
        /// with encrypted identifiers.
        /// </summary>
        /// <param name="db">The application database context for data access operations</param>
        /// <param name="interactionIssueId">The unique identifier of the interaction issue to find consequences for. Returns empty list if null</param>
        /// <param name="pkSecret">The private key secret used for encrypting entity identifiers in the returned DTOs</param>
        /// <param name="logger">The logger instance for recording operations and potential errors during processing</param>
        /// <returns>A list of InteractionConsequenceDto objects representing the consequences, or an empty list if none found</returns>
        /// <remarks>
        /// This method follows the data flow: SectionDto > InteractionIssueDto > InteractionConsequenceDto
        /// The method uses AsNoTracking() for read-only operations to improve performance.
        /// All returned DTOs contain encrypted IDs for security purposes.
        /// </remarks>
        /// <example>
        /// <code>
        /// var consequences = await buildInteractionConsequencesAsync(dbContext, 456, secretKey, logger);
        /// </code>
        /// </example>
        /// <seealso cref="Label.InteractionConsequence"/>
        /// <seealso cref="InteractionConsequenceDto"/>
        private static async Task<List<InteractionConsequenceDto>> buildInteractionConsequencesAsync(ApplicationDbContext db, int? interactionIssueId, string pkSecret, ILogger logger)
        {
            #region implementation
            // Early return if no interaction issue ID provided
            if (interactionIssueId == null) return new List<InteractionConsequenceDto>();

            // Query interaction consequences for the specified interaction issue using read-only tracking
            var items = await db.Set<Label.InteractionConsequence>()
                .AsNoTracking()
                .Where(e => e.InteractionIssueID == interactionIssueId)
                .ToListAsync();

            // Return empty list if no consequences found
            if (items == null || !items.Any())
                return new List<InteractionConsequenceDto>();

            // Transform entities to DTOs with encrypted IDs for security, ensuring non-null result
            return items
                .Select(item => new InteractionConsequenceDto
                {
                    InteractionConsequence = item.ToEntityWithEncryptedId(pkSecret, logger)
                }).ToList() ?? new List<InteractionConsequenceDto>();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds a list of BillingUnitIndex DTOs for the specified section.
        /// Retrieves links between NDC Package Codes and their NCPDP Billing Units.
        /// </summary>
        /// <param name="db">The database context.</param>
        /// <param name="sectionId">The section ID to find billing unit indexes for.</param>
        /// <param name="pkSecret">Secret used for ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <returns>List of BillingUnitIndex DTOs with encrypted IDs.</returns>
        /// <seealso cref="Label.BillingUnitIndex"/>
        private static async Task<List<BillingUnitIndexDto>> buildBillingUnitIndexesAsync(ApplicationDbContext db, int? sectionId, string pkSecret, ILogger logger)
        {
            #region implementation
            if (sectionId == null) return new List<BillingUnitIndexDto>();

            // Query billing unit indexes for the specified section
            var items = await db.Set<Label.BillingUnitIndex>()
                .AsNoTracking()
                .Where(e => e.SectionID == sectionId)
                .ToListAsync();

            // Transform entities to DTOs with encrypted IDs
            return items
                .Select(item => new BillingUnitIndexDto { BillingUnitIndex = item.ToEntityWithEncryptedId(pkSecret, logger) })
                .ToList();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds a list of WarningLetterProductInfo DTOs for the specified section.
        /// Retrieves key product identification details referenced in Warning Letter Alert Indexing documents.
        /// </summary>
        /// <param name="db">The database context.</param>
        /// <param name="sectionId">The section ID to find warning letter product info for.</param>
        /// <param name="pkSecret">Secret used for ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <returns>List of WarningLetterProductInfo DTOs with encrypted IDs.</returns>
        /// <seealso cref="Label.WarningLetterProductInfo"/>
        private static async Task<List<WarningLetterProductInfoDto>> buildWarningLetterProductInfosAsync(ApplicationDbContext db, int? sectionId, string pkSecret, ILogger logger)
        {
            #region implementation
            if (sectionId == null) return new List<WarningLetterProductInfoDto>();

            // Query warning letter product info for the specified section
            var items = await db.Set<Label.WarningLetterProductInfo>()
                .AsNoTracking()
                .Where(e => e.SectionID == sectionId)
                .ToListAsync();

            // Transform entities to DTOs with encrypted IDs
            return items
                .Select(item => new WarningLetterProductInfoDto { WarningLetterProductInfo = item.ToEntityWithEncryptedId(pkSecret, logger) })
                .ToList();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds a list of WarningLetterDate DTOs for the specified section.
        /// Retrieves issue dates and optional resolution dates for warning letter alerts.
        /// </summary>
        /// <param name="db">The database context.</param>
        /// <param name="sectionId">The section ID to find warning letter dates for.</param>
        /// <param name="pkSecret">Secret used for ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <returns>List of WarningLetterDate DTOs with encrypted IDs.</returns>
        /// <seealso cref="Label.WarningLetterDate"/>
        private static async Task<List<WarningLetterDateDto>> buildWarningLetterDatesAsync(ApplicationDbContext db, int? sectionId, string pkSecret, ILogger logger)
        {
            #region implementation
            if (sectionId == null) return new List<WarningLetterDateDto>();

            // Query warning letter dates for the specified section
            var items = await db.Set<Label.WarningLetterDate>()
                .AsNoTracking()
                .Where(e => e.SectionID == sectionId)
                .ToListAsync();

            // Transform entities to DTOs with encrypted IDs
            return items
                .Select(item => new WarningLetterDateDto { WarningLetterDate = item.ToEntityWithEncryptedId(pkSecret, logger) })
                .ToList();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds a list of Protocol DTOs for the specified section.
        /// Retrieves REMS protocols defined within sections.
        /// </summary>
        /// <param name="db">The database context.</param>
        /// <param name="sectionId">The section ID to find protocols for.</param>
        /// <param name="pkSecret">Secret used for ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <returns>List of Protocol DTOs with encrypted IDs.</returns>
        /// <seealso cref="Label.Protocol"/>
        private static async Task<List<ProtocolDto>> buildProtocolsAsync(ApplicationDbContext db, int? sectionId, string pkSecret, ILogger logger)
        {
            #region implementation
            if (sectionId == null) return new List<ProtocolDto>();

            var dtos = new List<ProtocolDto>();

            // Query protocols for the specified section
            var items = await db.Set<Label.Protocol>()
                .AsNoTracking()
                .Where(e => e.SectionID == sectionId)
                .ToListAsync();

            if (items == null || !items.Any())
                return new List<ProtocolDto>();

            foreach (var e in items)
            {
                // Build all REMS approvals for this protocol
                var remsApprovals = await buildREMSApprovalsAsync(db, e.ProtocolID, pkSecret, logger);

                // Build all requirements for this protocol
                var requirements = await buildRequirementsAsync(db, e.ProtocolID, pkSecret, logger);

                dtos.Add(new ProtocolDto
                {
                    Protocol = e.ToEntityWithEncryptedId(pkSecret, logger),
                    REMSApprovals = remsApprovals,
                    Requirements = requirements
                });
            }

            // Transform entities to DTOs with encrypted IDs
            return dtos ?? new List<ProtocolDto>();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Stores the REMS approval details associated with the first protocol mention.
        /// Retrieves REMS approval records for a specified protocol ID and 
        /// transforms them into DTOs with encrypted identifiers.
        /// </summary>
        /// <param name="db">The application database context for data access operations</param>
        /// <param name="protocolID">The unique identifier of the protocol to find REMS approvals for. Returns empty list if null</param>
        /// <param name="pkSecret">The private key secret used for encrypting entity identifiers in the returned DTOs</param>
        /// <param name="logger">The logger instance for recording operations and potential errors during processing</param>
        /// <returns>A list of REMSApprovalDto objects representing the REMS approvals, or an empty list if none found</returns>
        /// <remarks>
        /// This method follows the data flow: SectionDto > ProtocolDto > REMSApprovalDto
        /// The method uses AsNoTracking() for read-only operations to improve performance.
        /// All returned DTOs contain encrypted IDs for security purposes.
        /// </remarks>
        /// <example>
        /// <code>
        /// var remsApprovals = await buildREMSApprovalsAsync(dbContext, 789, secretKey, logger);
        /// </code>
        /// </example>
        /// <seealso cref="Label.REMSApproval"/>
        /// <seealso cref="REMSApprovalDto"/>
        private static async Task<List<REMSApprovalDto>> buildREMSApprovalsAsync(ApplicationDbContext db, int? protocolID, string pkSecret, ILogger logger)
        {
            #region implementation
            // Early return if no protocol ID provided
            if (protocolID == null) return new List<REMSApprovalDto>();

            // Query REMS approvals for the specified protocol using read-only tracking
            var items = await db.Set<Label.REMSApproval>()
                .AsNoTracking()
                .Where(e => e.ProtocolID == protocolID)
                .ToListAsync();

            // Return empty list if no REMS approvals found
            if (items == null || !items.Any())
                return new List<REMSApprovalDto>();

            // Transform entities to DTOs with encrypted IDs for security, ensuring non-null result
            return items
                .Select(item => new REMSApprovalDto { REMSApproval = item.ToEntityWithEncryptedId(pkSecret, logger) })
                .ToList() ?? new List<REMSApprovalDto>();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Represents a REMS requirement or monitoring observation within a protocol.
        /// Retrieves requirement records for a specified protocol ID along with their 
        /// associated stakeholders and transforms them into DTOs with encrypted identifiers.
        /// </summary>
        /// <param name="db">The application database context for data access operations</param>
        /// <param name="protocolID">The unique identifier of the protocol to find requirements for. Returns empty list if null</param>
        /// <param name="pkSecret">The private key secret used for encrypting entity identifiers in the returned DTOs</param>
        /// <param name="logger">The logger instance for recording operations and potential errors during processing</param>
        /// <returns>A list of RequirementDto objects representing the requirements with their stakeholders, or an empty list if none found</returns>
        /// <remarks>
        /// This method follows the data flow: SectionDto > ProtocolDto > RequirementDto
        /// Each requirement is enriched with its associated stakeholder information.
        /// The method uses AsNoTracking() for read-only operations to improve performance.
        /// All returned DTOs contain encrypted IDs for security purposes.
        /// </remarks>
        /// <example>
        /// <code>
        /// var requirements = await buildRequirementsAsync(dbContext, 789, secretKey, logger);
        /// </code>
        /// </example>
        /// <seealso cref="Label.Requirement"/>
        /// <seealso cref="RequirementDto"/>
        /// <seealso cref="buildStakeholdersAsync"/>
        private static async Task<List<RequirementDto>> buildRequirementsAsync(ApplicationDbContext db, int? protocolID, string pkSecret, ILogger logger)
        {
            #region implementation
            // Early return if no protocol ID provided
            if (protocolID == null) return new List<RequirementDto>();

            var dtos = new List<RequirementDto>();

            // Query requirements for the specified protocol using read-only tracking
            var items = await db.Set<Label.Requirement>()
                .AsNoTracking()
                .Where(e => e.ProtocolID == protocolID)
                .ToListAsync();

            // Return empty list if no requirements found
            if (items == null || !items.Any())
                return new List<RequirementDto>();

            // Process each requirement and build associated stakeholder data
            foreach (var item in items)
            {
                // Build all stakeholders for this requirement
                var stakeholders = await buildStakeholdersAsync(db, item.StakeholderID, pkSecret, logger);

                // Create requirement DTO with encrypted ID and associated stakeholders
                dtos.Add(new RequirementDto
                {
                    Requirement = item.ToEntityWithEncryptedId(pkSecret, logger),
                    Stakeholders = stakeholders
                });
            }

            // Return processed requirements with stakeholder data, ensuring non-null result
            return dtos ?? new List<RequirementDto>();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Lookup table for REMS stakeholder types. Retrieves stakeholder 
        /// records for a specified stakeholder ID and transforms them into 
        /// DTOs with encrypted identifiers.
        /// </summary>
        /// <param name="db">The application database context for data access operations</param>
        /// <param name="stakeholderID">The unique identifier of the stakeholder to find records for. Returns empty list if null</param>
        /// <param name="pkSecret">The private key secret used for encrypting entity identifiers in the returned DTOs</param>
        /// <param name="logger">The logger instance for recording operations and potential errors during processing</param>
        /// <returns>A list of StakeholderDto objects representing the stakeholders, or an empty list if none found</returns>
        /// <remarks>
        /// This method follows the data flow: SectionDto > ProtocolDto > RequirementDto > StakeholderDto
        /// The method uses AsNoTracking() for read-only operations to improve performance.
        /// All returned DTOs contain encrypted IDs for security purposes.
        /// Note: Currently queries REMSApproval table using stakeholderID as ProtocolID - this may need review.
        /// </remarks>
        /// <example>
        /// <code>
        /// var stakeholders = await buildStakeholdersAsync(dbContext, 101, secretKey, logger);
        /// </code>
        /// </example>
        /// <seealso cref="Label.REMSApproval"/>
        /// <seealso cref="StakeholderDto"/>
        private static async Task<List<StakeholderDto>> buildStakeholdersAsync(ApplicationDbContext db, int? stakeholderID, string pkSecret, ILogger logger)
        {
            #region implementation
            // Early return if no stakeholder ID provided
            if (stakeholderID == null) return new List<StakeholderDto>();

            // Query stakeholder data using REMSApproval table with stakeholderID as ProtocolID filter
            var items = await db.Set<Label.Stakeholder>()
                .AsNoTracking()
                .Where(e => e.StakeholderID == stakeholderID)
                .ToListAsync();

            // Return empty list if no stakeholder records found
            if (items == null || !items.Any())
                return new List<StakeholderDto>();

            // Transform entities to DTOs with encrypted IDs for security, ensuring non-null result
            return items
                .Select(item => new StakeholderDto { Stakeholder = item.ToEntityWithEncryptedId(pkSecret, logger) })
                .ToList() ?? new List<StakeholderDto>();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds a list of REMSMaterial DTOs for the specified section.
        /// Retrieves references to REMS materials with potential document attachments.
        /// </summary>
        /// <param name="db">The database context.</param>
        /// <param name="sectionId">The section ID to find REMS materials for.</param>
        /// <param name="pkSecret">Secret used for ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <returns>List of REMSMaterial DTOs with encrypted IDs.</returns>
        /// <seealso cref="Label.REMSMaterial"/>
        /// <seealso cref="Label.AttachedDocument"/>
        /// <seealso cref="REMSMaterialDto"/>
        /// <seealso cref="AttachedDocumentDto"/>
        /// <seealso cref="buildREMSAttachmentsAsync"/>
        private static async Task<List<REMSMaterialDto>> buildREMSMaterialsAsync(ApplicationDbContext db, int? sectionId, string pkSecret, ILogger logger)
        {
            #region implementation
            if (sectionId == null) return new List<REMSMaterialDto>();

            var dtos = new List<REMSMaterialDto>();

            // Query REMS materials for the specified section
            var items = await db.Set<Label.REMSMaterial>()
                .AsNoTracking()
                .Where(e => e.SectionID == sectionId)
                .ToListAsync();

            if (items == null || !items.Any())
                return new List<REMSMaterialDto>();

            foreach (var item in items)
            {
                if (item.REMSMaterialID == null) continue;

                // Build all attachments for this REMS material
                var attachments = await buildREMSAttachmentsAsync(db, item.REMSMaterialID, pkSecret, logger);

                dtos.Add(new REMSMaterialDto
                {
                    REMSMaterial = item.ToEntityWithEncryptedId(pkSecret, logger),
                    AttachedDocuments = attachments
                });
            }

            // Transform entities to DTOs with encrypted IDs
            return dtos ?? new List<REMSMaterialDto>();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds REMS attachments for the specified parent entity. Retrieves 
        /// attached document records for a specified parent entity ID and 
        /// transforms them into DTOs with encrypted identifiers.
        /// </summary>
        /// <param name="db">The application database context for data access operations</param>
        /// <param name="parentEntityID">The unique identifier of the parent entity to find attached documents for. Returns empty list if null</param>
        /// <param name="pkSecret">The private key secret used for encrypting entity identifiers in the returned DTOs</param>
        /// <param name="logger">The logger instance for recording operations and potential errors during processing</param>
        /// <returns>A list of AttachedDocumentDto objects representing the REMS attachments, or an empty list if none found</returns>
        /// <remarks>
        /// REMS (Risk Evaluation and Mitigation Strategies) attachments are regulatory documents associated with parent entities.
        /// The method uses AsNoTracking() for read-only operations to improve performance.
        /// All returned DTOs contain encrypted IDs for security purposes.
        /// </remarks>
        /// <example>
        /// <code>
        /// var remsAttachments = await buildREMSAttachmentsAsync(dbContext, 127, secretKey, logger);
        /// </code>
        /// </example>
        /// <seealso cref="Label.AttachedDocument"/>
        /// <seealso cref="AttachedDocumentDto"/>
        private static async Task<List<AttachedDocumentDto>> buildREMSAttachmentsAsync(ApplicationDbContext db, int? parentEntityID, string pkSecret, ILogger logger)
        {
            #region implementation
            // Early return if no parent entity ID provided
            if (parentEntityID == null)
                return new List<AttachedDocumentDto>();

            // Query attached documents for the specified parent entity using read-only tracking
            var items = await db.Set<Label.AttachedDocument>()
                .AsNoTracking()
                .Where(e => e.ParentEntityID == parentEntityID)
                .ToListAsync();

            // Return empty list if no attached documents found
            if (items == null || !items.Any())
                return new List<AttachedDocumentDto>();

            // Transform entities to DTOs with encrypted IDs for security, ensuring non-null result
            return items
                .Select(item => new AttachedDocumentDto { AttachedDocument = item.ToEntityWithEncryptedId(pkSecret, logger) })
                .ToList() ?? new List<AttachedDocumentDto>();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds a list of REMSElectronicResource DTOs for the specified section.
        /// Retrieves references to REMS electronic resources including URLs and URNs.
        /// </summary>
        /// <param name="db">The database context.</param>
        /// <param name="sectionId">The section ID to find REMS electronic resources for.</param>
        /// <param name="pkSecret">Secret used for ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <returns>List of REMSElectronicResource DTOs with encrypted IDs.</returns>
        /// <seealso cref="Label.REMSElectronicResource"/>
        private static async Task<List<REMSElectronicResourceDto>> buildREMSElectronicResourcesAsync(ApplicationDbContext db, int? sectionId, string pkSecret, ILogger logger)
        {
            #region implementation
            if (sectionId == null) return new List<REMSElectronicResourceDto>();

            // Query REMS electronic resources for the specified section
            var items = await db.Set<Label.REMSElectronicResource>()
                .AsNoTracking()
                .Where(e => e.SectionID == sectionId)
                .ToListAsync();

            // Transform entities to DTOs with encrypted IDs
            return items
                .Select(item => new REMSElectronicResourceDto { REMSElectronicResource = item.ToEntityWithEncryptedId(pkSecret, logger) })
                .ToList();
            #endregion
        }
        #endregion
    }
}