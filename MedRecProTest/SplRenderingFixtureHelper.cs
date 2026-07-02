using MedRecPro.Data;
using MedRecPro.Helpers;
using MedRecPro.Models;
using MedRecPro.Service.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Globalization;
using System.Text.Json;
using System.Xml.Linq;

namespace MedRecPro.Service.Test
{
    /**************************************************************/
    /// <summary>
    /// Loads the repo-local SPL rendering fixture and maps stable JSON fields into DTOs.
    /// </summary>
    /// <remarks>
    /// The fixture JSON is API-shaped camelCase, while DTO computed properties expect
    /// PascalCase dictionary keys. This helper creates deterministic test DTOs without
    /// depending on user-specific attachment paths or production secrets.
    /// </remarks>
    /// <seealso cref="DocumentDto"/>
    /// <seealso cref="Util"/>
    internal static class SplRenderingFixtureHelper
    {
        #region implementation

        /**************************************************************/
        /// <summary>
        /// Document GUID for the amlodipine/benazepril fixture.
        /// </summary>
        /// <seealso cref="LoadFixtureDocument"/>
        public static readonly Guid FixtureDocumentGuid = Guid.Parse("53566d4f-ff40-4815-b922-3416cde56fb1");

        /**************************************************************/
        /// <summary>
        /// Set GUID for the amlodipine/benazepril fixture.
        /// </summary>
        /// <seealso cref="LoadFixtureDocument"/>
        public static readonly Guid FixtureSetGuid = Guid.Parse("968c611b-43a7-40f0-9ed3-aa67ee88364b");

        /**************************************************************/
        /// <summary>
        /// File name for the fixture JSON copied into the test output folder.
        /// </summary>
        /// <seealso cref="GetFixturePath"/>
        public const string FixtureJsonFileName = "53566d4f-ff40-4815-b922-3416cde56fb1.json";

        /**************************************************************/
        /// <summary>
        /// File name for the fixture XML copied into the test output folder.
        /// </summary>
        /// <seealso cref="LoadFixtureXml"/>
        public const string FixtureXmlFileName = "53566d4f-ff40-4815-b922-3416cde56fb1.xml";

        private const string TestSecret = "SplRenderingFixture-Fixed-Secret";

        #endregion

        /**************************************************************/
        /// <summary>
        /// Initializes static helpers that decrypt DTO identifier dictionaries.
        /// </summary>
        /// <seealso cref="Util.Initialize"/>
        public static void InitializeUtil()
        {
            #region implementation
            Util.Initialize(
                new HttpContextAccessor { HttpContext = new DefaultHttpContext() },
                new EncryptionService(CreateConfiguration()),
                new DictionaryUtilityService());
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates deterministic in-memory configuration for fixture tests.
        /// </summary>
        /// <returns>Configuration containing a fixed encryption secret and disabled debug flag.</returns>
        /// <seealso cref="IConfiguration"/>
        public static IConfiguration CreateConfiguration()
        {
            #region implementation
            return new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Security:DB:PKSecret"] = TestSecret,
                    ["FeatureFlags:UseEnhancedDebugging"] = "false"
                })
                .Build();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates an in-memory rendering service for text content tests.
        /// </summary>
        /// <returns>A text content rendering service with an isolated EF Core context.</returns>
        /// <seealso cref="TextContentRenderingService"/>
        public static TextContentRenderingService CreateTextContentRenderingService()
        {
            #region implementation
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase($"spl_rendering_{Guid.NewGuid():N}")
                .Options;

            return new TextContentRenderingService(new ApplicationDbContext(options), CreateConfiguration());
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Resolves a copied fixture file path from the test output directory.
        /// </summary>
        /// <param name="fileName">Fixture file name under TestData/SPL.</param>
        /// <returns>Absolute path to the copied fixture file.</returns>
        /// <seealso cref="FixtureJsonFileName"/>
        public static string GetFixturePath(string fileName)
        {
            #region implementation
            return Path.Combine(AppContext.BaseDirectory, "TestData", "SPL", fileName);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Loads the copied fixture XML document.
        /// </summary>
        /// <returns>The SPL fixture XML document.</returns>
        /// <seealso cref="FixtureXmlFileName"/>
        public static XDocument LoadFixtureXml()
        {
            #region implementation
            return XDocument.Load(GetFixturePath(FixtureXmlFileName), LoadOptions.PreserveWhitespace);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Loads the copied fixture JSON into a rendering-ready document DTO.
        /// </summary>
        /// <returns>Document DTO containing authors, structured body, sections, and products.</returns>
        /// <seealso cref="DocumentDto"/>
        public static DocumentDto LoadFixtureDocument()
        {
            #region implementation
            InitializeUtil();

            using var json = JsonDocument.Parse(File.ReadAllText(GetFixturePath(FixtureJsonFileName)));
            var root = json.RootElement;
            var documentElement = root.GetProperty("document");

            var document = new DocumentDto
            {
                Document = new Dictionary<string, object?>
                {
                    [nameof(DocumentDto.DocumentGUID)] = getGuid(documentElement, "documentGUID") ?? FixtureDocumentGuid,
                    [nameof(DocumentDto.DocumentCode)] = getString(documentElement, "documentCode"),
                    [nameof(DocumentDto.DocumentCodeSystem)] = getString(documentElement, "documentCodeSystem"),
                    [nameof(DocumentDto.DocumentDisplayName)] = getString(documentElement, "documentDisplayName"),
                    [nameof(DocumentDto.Title)] = getString(documentElement, "title"),
                    [nameof(DocumentDto.EffectiveTime)] = getDate(documentElement, "effectiveTime"),
                    [nameof(DocumentDto.SetGUID)] = getGuid(documentElement, "setGUID") ?? FixtureSetGuid,
                    [nameof(DocumentDto.VersionNumber)] = getInt(documentElement, "versionNumber"),
                    [nameof(DocumentDto.SubmissionFileName)] = getString(documentElement, "submissionFileName")
                }
            };

            document.DocumentAuthors.Add(loadAuthor(root.GetProperty("documentAuthors").EnumerateArray().First(), document));

            var structuredBodyElement = root.GetProperty("structuredBodies").EnumerateArray().First();
            var structuredBody = loadStructuredBody(structuredBodyElement, document);
            document.StructuredBodies.Add(structuredBody);

            return document;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets the first product from the fixture document.
        /// </summary>
        /// <returns>A representative product DTO from the supplied label fixture.</returns>
        /// <seealso cref="ProductDto"/>
        public static ProductDto LoadFirstProduct()
        {
            #region implementation
            return LoadFixtureDocument().StructuredBodies.Single().Sections
                .SelectMany(section => section.Products)
                .First(product => product.ProductName != null);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets the first section from the fixture that contains text content.
        /// </summary>
        /// <returns>A section DTO with text content populated from the fixture.</returns>
        /// <seealso cref="SectionDto"/>
        public static SectionDto LoadFirstTextSection()
        {
            #region implementation
            return LoadFixtureDocument().StructuredBodies.Single().Sections
                .First(section => section.TextContents.Any());
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets the first section from the fixture that contains observation media.
        /// </summary>
        /// <returns>A section DTO with observation media populated from the fixture.</returns>
        /// <seealso cref="ObservationMediaDto"/>
        public static SectionDto LoadFirstMediaSection()
        {
            #region implementation
            return LoadFixtureDocument().StructuredBodies.Single().Sections
                .First(section => section.ObservationMedia.Any());
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates a deterministic encrypted identifier for DTO dictionary keys.
        /// </summary>
        /// <param name="id">Plain integer identifier.</param>
        /// <returns>Encrypted identifier text.</returns>
        /// <seealso cref="StringCipher"/>
        public static string EncryptedId(int id)
        {
            #region implementation
            return StringCipher.Encrypt(id.ToString(CultureInfo.InvariantCulture), TestSecret, StringCipher.EncryptionStrength.Fast);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates deterministic encrypted text for DTO dictionary keys.
        /// </summary>
        /// <param name="value">Plain text value.</param>
        /// <returns>Encrypted text.</returns>
        /// <seealso cref="StringCipher"/>
        public static string EncryptedText(string value)
        {
            #region implementation
            return StringCipher.Encrypt(value, TestSecret, StringCipher.EncryptionStrength.Fast);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates a product-level or package-level characteristic.
        /// </summary>
        /// <param name="id">Characteristic identifier.</param>
        /// <param name="packagingLevelId">Optional package identifier for package-specific characteristics.</param>
        /// <returns>Characteristic DTO with deterministic value data.</returns>
        /// <seealso cref="CharacteristicDto"/>
        public static CharacteristicDto CreateCharacteristic(int id, int? packagingLevelId = null)
        {
            #region implementation
            return new CharacteristicDto
            {
                Characteristic = new Dictionary<string, object?>
                {
                    ["EncryptedCharacteristicID"] = EncryptedId(id),
                    ["EncryptedPackagingLevelID"] = packagingLevelId.HasValue ? EncryptedId(packagingLevelId.Value) : null,
                    [nameof(CharacteristicDto.ValueType)] = "PQ",
                    [nameof(CharacteristicDto.ValuePQ_Value)] = 12.500m,
                    [nameof(CharacteristicDto.ValuePQ_Unit)] = "mg",
                    [nameof(CharacteristicDto.ValueCV_Code)] = "C42998",
                    [nameof(CharacteristicDto.ValueCV_DisplayName)] = "CAPSULE",
                    [nameof(CharacteristicDto.OriginalText)] = string.Empty
                }
            };
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates a text content item with a resolved rendered media reference.
        /// </summary>
        /// <param name="textContentId">Text content identifier.</param>
        /// <param name="observationMediaId">Observation media identifier.</param>
        /// <returns>A multimedia text content DTO.</returns>
        /// <seealso cref="TextContentRenderingService"/>
        public static SectionTextContentDto CreateMultimediaTextContent(int textContentId, int observationMediaId)
        {
            #region implementation
            return new SectionTextContentDto
            {
                SectionTextContent = new Dictionary<string, object?>
                {
                    ["EncryptedSectionTextContentID"] = EncryptedId(textContentId),
                    [nameof(SectionTextContentDto.ContentType)] = "RenderMultiMedia",
                    [nameof(SectionTextContentDto.SequenceNumber)] = 2,
                    [nameof(SectionTextContentDto.ContentText)] = "<renderMultiMedia referencedObject=\"IMGID3621\" />"
                },
                RenderedMedias = new List<RenderedMediaDto>
                {
                    new()
                    {
                        RenderedMedia = new Dictionary<string, object?>
                        {
                            ["EncryptedRenderedMediaID"] = EncryptedId(7001),
                            ["EncryptedSectionTextContentID"] = EncryptedId(textContentId),
                            ["EncryptedObservationMediaID"] = EncryptedId(observationMediaId),
                            [nameof(RenderedMediaDto.SequenceInContent)] = 1,
                            [nameof(RenderedMediaDto.IsInline)] = false
                        }
                    }
                }
            };
            #endregion
        }

        private static DocumentAuthorDto loadAuthor(JsonElement authorElement, DocumentDto document)
        {
            #region implementation
            var documentAuthorElement = authorElement.GetProperty("documentAuthor");
            var organizationElement = authorElement.GetProperty("organization").GetProperty("organization");
            var identifierElement = authorElement.GetProperty("organization").GetProperty("identifiers").EnumerateArray().First().GetProperty("organizationIdentifier");

            var organization = new OrganizationDto
            {
                Organization = new Dictionary<string, object?>
                {
                    ["EncryptedOrganizationID"] = EncryptedId(100),
                    [nameof(OrganizationDto.OrganizationName)] = getString(organizationElement, "organizationName"),
                    [nameof(OrganizationDto.IsConfidential)] = getBool(organizationElement, "isConfidential")
                },
                Identifiers = new List<OrganizationIdentifierDto>
                {
                    new()
                    {
                        OrganizationIdentifier = new Dictionary<string, object?>
                        {
                            ["EncryptedOrganizationIdentifierID"] = EncryptedId(101),
                            ["EncryptedOrganizationID"] = EncryptedId(100),
                            [nameof(OrganizationIdentifierDto.IdentifierValue)] = getString(identifierElement, "identifierValue"),
                            [nameof(OrganizationIdentifierDto.IdentifierSystemOID)] = getString(identifierElement, "identifierSystemOID"),
                            [nameof(OrganizationIdentifierDto.IdentifierType)] = getString(identifierElement, "identifierType")
                        }
                    }
                }
            };

            return new DocumentAuthorDto
            {
                Document = document,
                Organization = organization,
                DocumentAuthor = new Dictionary<string, object?>
                {
                    ["EncryptedDocumentAuthorID"] = EncryptedId(10),
                    ["EncryptedDocumentID"] = EncryptedId(1),
                    ["EncryptedOrganizationID"] = EncryptedId(100),
                    [nameof(DocumentAuthorDto.AuthorType)] = getString(documentAuthorElement, "authorType")
                }
            };
            #endregion
        }

        private static StructuredBodyDto loadStructuredBody(JsonElement structuredBodyElement, DocumentDto document)
        {
            #region implementation
            var structuredBody = new StructuredBodyDto
            {
                Document = document,
                StructuredBody = new Dictionary<string, object?>
                {
                    ["EncryptedStructuredBodyID"] = EncryptedId(20),
                    ["EncryptedDocumentID"] = EncryptedId(1)
                }
            };

            var sectionId = 1;
            foreach (var sectionElement in structuredBodyElement.GetProperty("sections").EnumerateArray())
            {
                structuredBody.Sections.Add(loadSection(sectionElement, structuredBody, sectionId++));
            }

            if (structuredBody.Sections.Count >= 3)
            {
                structuredBody.SectionHierarchies.Add(new SectionHierarchyDto
                {
                    SectionHierarchy = new Dictionary<string, object?>
                    {
                        ["EncryptedSectionHierarchyID"] = EncryptedId(301),
                        ["EncryptedParentSectionID"] = EncryptedId(structuredBody.Sections[0].SectionID!.Value),
                        ["EncryptedChildSectionID"] = EncryptedId(structuredBody.Sections[1].SectionID!.Value),
                        [nameof(SectionHierarchyDto.SequenceNumber)] = 2
                    }
                });
                structuredBody.SectionHierarchies.Add(new SectionHierarchyDto
                {
                    SectionHierarchy = new Dictionary<string, object?>
                    {
                        ["EncryptedSectionHierarchyID"] = EncryptedId(302),
                        ["EncryptedParentSectionID"] = EncryptedId(structuredBody.Sections[0].SectionID!.Value),
                        ["EncryptedChildSectionID"] = EncryptedId(structuredBody.Sections[2].SectionID!.Value),
                        [nameof(SectionHierarchyDto.SequenceNumber)] = 1
                    }
                });
            }

            return structuredBody;
            #endregion
        }

        private static SectionDto loadSection(JsonElement sectionElement, StructuredBodyDto structuredBody, int sectionId)
        {
            #region implementation
            var rawSection = sectionElement.GetProperty("section");
            var section = new SectionDto
            {
                StructuredBody = structuredBody,
                Section = new Dictionary<string, object?>
                {
                    ["EncryptedSectionID"] = EncryptedId(sectionId),
                    ["EncryptedStructuredBodyID"] = EncryptedId(20),
                    ["EncryptedDocumentID"] = EncryptedId(1),
                    [nameof(SectionDto.SectionLinkGUID)] = $"ID{sectionId:000}",
                    [nameof(SectionDto.SectionGUID)] = getGuid(rawSection, "sectionGUID") ?? Guid.NewGuid(),
                    [nameof(SectionDto.SectionCode)] = getString(rawSection, "sectionCode"),
                    [nameof(SectionDto.SectionCodeSystem)] = getString(rawSection, "sectionCodeSystem"),
                    [nameof(SectionDto.SectionCodeSystemName)] = "LOINC",
                    [nameof(SectionDto.SectionDisplayName)] = getString(rawSection, "sectionDisplayName"),
                    [nameof(SectionDto.Title)] = getString(rawSection, "title") ?? getString(rawSection, "sectionDisplayName"),
                    [nameof(SectionDto.EffectiveTime)] = getDate(rawSection, "effectiveTime")
                }
            };

            var textId = sectionId * 100;
            if (sectionElement.TryGetProperty("textContents", out var textContents))
            {
                foreach (var textElement in textContents.EnumerateArray())
                {
                    section.TextContents.Add(loadTextContent(textElement, section, textId++));
                }
            }

            var mediaId = sectionId * 1000;
            if (sectionElement.TryGetProperty("observationMedia", out var observationMedia))
            {
                foreach (var mediaElement in observationMedia.EnumerateArray())
                {
                    section.ObservationMedia.Add(loadObservationMedia(mediaElement, section, mediaId++));
                }
            }

            if (section.TextContents.Any())
            {
                section.ExcerptHighlights.Add(new SectionExcerptHighlightDto
                {
                    SectionExcerptHighlight = new Dictionary<string, object?>
                    {
                        ["EncryptedSectionExcerptHighlightID"] = EncryptedId(sectionId * 2000),
                        [nameof(SectionExcerptHighlightDto.HighlightText)] = "<paragraph xmlns=\"urn:hl7-org:v3\">Fixture warning highlight.</paragraph>"
                    }
                });
            }

            var productId = sectionId * 10000;
            if (sectionElement.TryGetProperty("products", out var products))
            {
                foreach (var productElement in products.EnumerateArray())
                {
                    section.Products.Add(loadProduct(productElement, section, productId++));
                }
            }

            return section;
            #endregion
        }

        private static SectionTextContentDto loadTextContent(JsonElement textElement, SectionDto section, int textId)
        {
            #region implementation
            var rawText = textElement.GetProperty("sectionTextContent");
            return new SectionTextContentDto
            {
                Section = section,
                SectionTextContent = new Dictionary<string, object?>
                {
                    ["EncryptedSectionTextContentID"] = EncryptedId(textId),
                    ["EncryptedSectionID"] = EncryptedId(section.SectionID!.Value),
                    [nameof(SectionTextContentDto.ContentType)] = getString(rawText, "contentType"),
                    [nameof(SectionTextContentDto.SequenceNumber)] = getInt(rawText, "sequenceNumber"),
                    [nameof(SectionTextContentDto.ContentText)] = getString(rawText, "contentText")
                }
            };
            #endregion
        }

        private static ObservationMediaDto loadObservationMedia(JsonElement mediaElement, SectionDto section, int mediaId)
        {
            #region implementation
            var rawMedia = mediaElement.GetProperty("observationMedia");
            var sourceMediaId = getString(rawMedia, "encryptedMediaID") ?? $"IMGID{mediaId}";
            return new ObservationMediaDto
            {
                Section = section,
                ObservationMedia = new Dictionary<string, object?>
                {
                    ["EncryptedObservationMediaID"] = EncryptedId(mediaId),
                    ["EncryptedSectionID"] = EncryptedId(section.SectionID!.Value),
                    ["EncryptedMediaID"] = sourceMediaId.StartsWith("F-", StringComparison.Ordinal)
                        ? sourceMediaId
                        : EncryptedText(sourceMediaId),
                    [nameof(ObservationMediaDto.DescriptionText)] = getString(rawMedia, "descriptionText"),
                    [nameof(ObservationMediaDto.MediaType)] = getString(rawMedia, "mediaType"),
                    [nameof(ObservationMediaDto.XsiType)] = getString(rawMedia, "xsiType"),
                    [nameof(ObservationMediaDto.FileName)] = getString(rawMedia, "fileName")
                }
            };
            #endregion
        }

        private static ProductDto loadProduct(JsonElement productElement, SectionDto section, int productId)
        {
            #region implementation
            var rawProduct = productElement.GetProperty("product");
            var product = new ProductDto
            {
                Section = section,
                Product = new Dictionary<string, object?>
                {
                    ["EncryptedProductID"] = EncryptedId(productId),
                    ["EncryptedSectionID"] = EncryptedId(section.SectionID!.Value),
                    [nameof(ProductDto.ProductName)] = getString(rawProduct, "productName"),
                    [nameof(ProductDto.FormCode)] = getString(rawProduct, "formCode"),
                    [nameof(ProductDto.FormCodeSystem)] = getString(rawProduct, "formCodeSystem"),
                    [nameof(ProductDto.FormDisplayName)] = getString(rawProduct, "formDisplayName")
                }
            };

            var ingredientId = productId * 10;
            if (productElement.TryGetProperty("ingredients", out var ingredients))
            {
                foreach (var ingredientElement in ingredients.EnumerateArray())
                {
                    product.Ingredients.Add(loadIngredient(ingredientElement, product, ingredientId++));
                }
            }

            var packageId = productId * 20;
            if (productElement.TryGetProperty("packagingLevels", out var packagingLevels))
            {
                foreach (var packagingElement in packagingLevels.EnumerateArray())
                {
                    var package = loadPackagingLevel(packagingElement, product, packageId++);
                    product.PackagingLevels.Add(package);
                    product.PackageIdentifiers.AddRange(package.PackageIdentifiers);
                }
            }

            if (product.PackagingLevels.Any())
            {
                var firstPackageId = product.PackagingLevels.First().PackagingLevelID!.Value;
                product.MarketingStatuses.Add(new MarketingStatusDto
                {
                    MarketingStatus = new Dictionary<string, object?>
                    {
                        ["EncryptedMarketingStatusID"] = EncryptedId(productId * 30),
                        ["EncryptedProductID"] = EncryptedId(productId),
                        ["EncryptedPackagingLevelID"] = EncryptedId(firstPackageId),
                        [nameof(MarketingStatusDto.StatusCode)] = "active",
                        [nameof(MarketingStatusDto.MarketingActCode)] = "C53292"
                    }
                });

                product.Characteristics.Add(CreateCharacteristic(productId * 40, firstPackageId));
            }

            product.Characteristics.Add(CreateCharacteristic(productId * 40 + 1));

            return product;
            #endregion
        }

        private static IngredientDto loadIngredient(JsonElement ingredientElement, ProductDto product, int ingredientId)
        {
            #region implementation
            var rawIngredient = ingredientElement.GetProperty("ingredient");
            var ingredient = new IngredientDto
            {
                Ingredient = new Dictionary<string, object?>
                {
                    ["EncryptedIngredientID"] = EncryptedId(ingredientId),
                    ["EncryptedProductID"] = EncryptedId(product.ProductID!.Value),
                    ["EncryptedIngredientSubstanceID"] = EncryptedId(ingredientId + 1000),
                    [nameof(IngredientDto.ClassCode)] = getString(rawIngredient, "classCode"),
                    [nameof(IngredientDto.QuantityNumerator)] = getDecimal(rawIngredient, "quantityNumerator"),
                    [nameof(IngredientDto.QuantityNumeratorUnit)] = getString(rawIngredient, "quantityNumeratorUnit"),
                    [nameof(IngredientDto.NumeratorTranslationCode)] = getString(rawIngredient, "numeratorTranslationCode"),
                    [nameof(IngredientDto.NumeratorCodeSystem)] = getString(rawIngredient, "numeratorCodeSystem"),
                    [nameof(IngredientDto.NumeratorDisplayName)] = getString(rawIngredient, "numeratorDisplayName"),
                    [nameof(IngredientDto.QuantityDenominator)] = getDecimal(rawIngredient, "quantityDenominator"),
                    [nameof(IngredientDto.DenominatorTranslationCode)] = getString(rawIngredient, "denominatorTranslationCode"),
                    [nameof(IngredientDto.DenominatorCodeSystem)] = getString(rawIngredient, "denominatorCodeSystem"),
                    [nameof(IngredientDto.DenominatorDisplayName)] = getString(rawIngredient, "denominatorDisplayName"),
                    [nameof(IngredientDto.QuantityDenominatorUnit)] = getString(rawIngredient, "quantityDenominatorUnit"),
                    [nameof(IngredientDto.SequenceNumber)] = getInt(rawIngredient, "sequenceNumber"),
                    [nameof(IngredientDto.OriginatingElement)] = getString(rawIngredient, "originatingElement")
                }
            };

            if (ingredientElement.TryGetProperty("ingredientSubstance", out var substanceWrapper)
                && substanceWrapper.TryGetProperty("ingredientSubstance", out var rawSubstance))
            {
                ingredient.IngredientSubstance = new IngredientSubstanceDto
                {
                    IngredientSubstance = new Dictionary<string, object?>
                    {
                        ["EncryptedIngredientSubstanceID"] = EncryptedId(ingredientId + 1000),
                        [nameof(IngredientSubstanceDto.UNII)] = getString(rawSubstance, "unii"),
                        [nameof(IngredientSubstanceDto.SubstanceName)] = getString(rawSubstance, "substanceName"),
                        [nameof(IngredientSubstanceDto.OriginatingElement)] = getString(rawSubstance, "originatingElement")
                    }
                };

                ingredient.IngredientSubstance.ActiveMoieties.Add(new ActiveMoietyDto
                {
                    ActiveMoiety = new Dictionary<string, object?>
                    {
                        ["EncryptedActiveMoietyID"] = EncryptedId(ingredientId + 2000),
                        ["EncryptedIngredientSubstanceID"] = EncryptedId(ingredientId + 1000),
                        [nameof(ActiveMoietyDto.MoietyUNII)] = "864V2Q084H",
                        [nameof(ActiveMoietyDto.MoietyName)] = "AMLODIPINE"
                    }
                });
            }

            ingredient.SpecifiedSubstances.Add(new SpecifiedSubstanceDto
            {
                SpecifiedSubstance = new Dictionary<string, object?>
                {
                    ["EncryptedSpecifiedSubstanceID"] = EncryptedId(ingredientId + 3000),
                    [nameof(SpecifiedSubstanceDto.SubstanceCode)] = "864V2Q084H",
                    [nameof(SpecifiedSubstanceDto.SubstanceCodeSystem)] = Constant.FDA_UNII_CODE_SYSTEM
                }
            });

            ingredient.ReferenceSubstances.Add(new ReferenceSubstanceDto
            {
                ReferenceSubstance = new Dictionary<string, object?>
                {
                    ["EncryptedReferenceSubstanceID"] = EncryptedId(ingredientId + 4000),
                    [nameof(ReferenceSubstanceDto.RefSubstanceUNII)] = "864V2Q084H",
                    [nameof(ReferenceSubstanceDto.RefSubstanceName)] = "AMLODIPINE"
                }
            });

            return ingredient;
            #endregion
        }

        private static PackagingLevelDto loadPackagingLevel(JsonElement packagingElement, ProductDto product, int packageId)
        {
            #region implementation
            var rawPackage = packagingElement.GetProperty("packagingLevel");
            var identifiers = new List<PackageIdentifierDto>();
            if (packagingElement.TryGetProperty("packageIdentifiers", out var packageIdentifiers))
            {
                var identifierId = packageId * 10;
                foreach (var identifierElement in packageIdentifiers.EnumerateArray())
                {
                    var rawIdentifier = identifierElement.GetProperty("packageIdentifier");
                    identifiers.Add(new PackageIdentifierDto
                    {
                        PackageIdentifier = new Dictionary<string, object?>
                        {
                            ["EncryptedPackageIdentifierID"] = EncryptedId(identifierId++),
                            ["EncryptedPackagingLevelID"] = EncryptedId(packageId),
                            [nameof(PackageIdentifierDto.IdentifierValue)] = getString(rawIdentifier, "identifierValue"),
                            [nameof(PackageIdentifierDto.IdentifierSystemOID)] = getString(rawIdentifier, "identifierSystemOID"),
                            [nameof(PackageIdentifierDto.IdentifierType)] = getString(rawIdentifier, "identifierType")
                        }
                    });
                }
            }

            var firstIdentifier = identifiers.FirstOrDefault();
            return new PackagingLevelDto
            {
                PackagingLevel = new Dictionary<string, object?>
                {
                    ["EncryptedPackagingLevelID"] = EncryptedId(packageId),
                    ["EncryptedProductID"] = EncryptedId(product.ProductID!.Value),
                    [nameof(PackagingLevelDto.QuantityNumerator)] = getDecimal(rawPackage, "quantityNumerator"),
                    [nameof(PackagingLevelDto.QuantityDenominator)] = getDecimal(rawPackage, "quantityDenominator"),
                    [nameof(PackagingLevelDto.QuantityNumeratorUnit)] = getString(rawPackage, "quantityNumeratorUnit"),
                    [nameof(PackagingLevelDto.PackageCode)] = firstIdentifier?.IdentifierValue,
                    [nameof(PackagingLevelDto.PackageCodeSystem)] = firstIdentifier?.IdentifierSystemOID,
                    [nameof(PackagingLevelDto.PackageFormCode)] = getString(rawPackage, "packageFormCode"),
                    [nameof(PackagingLevelDto.PackageFormCodeSystem)] = getString(rawPackage, "packageFormCodeSystem"),
                    [nameof(PackagingLevelDto.PackageFormDisplayName)] = getString(rawPackage, "packageFormDisplayName")
                },
                PackageIdentifiers = identifiers
            };
            #endregion
        }

        private static string? getString(JsonElement element, string propertyName)
        {
            #region implementation
            if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            {
                return null;
            }

            return property.ValueKind == JsonValueKind.String
                ? property.GetString()
                : property.ToString();
            #endregion
        }

        private static bool? getBool(JsonElement element, string propertyName)
        {
            #region implementation
            if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            {
                return null;
            }

            return property.ValueKind == JsonValueKind.True
                ? true
                : property.ValueKind == JsonValueKind.False
                    ? false
                    : null;
            #endregion
        }

        private static int? getInt(JsonElement element, string propertyName)
        {
            #region implementation
            if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            {
                return null;
            }

            if (property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out var value))
            {
                return value;
            }

            return int.TryParse(property.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : null;
            #endregion
        }

        private static decimal? getDecimal(JsonElement element, string propertyName)
        {
            #region implementation
            if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            {
                return null;
            }

            if (property.ValueKind == JsonValueKind.Number && property.TryGetDecimal(out var value))
            {
                return value;
            }

            return decimal.TryParse(property.ToString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : null;
            #endregion
        }

        private static Guid? getGuid(JsonElement element, string propertyName)
        {
            #region implementation
            var value = getString(element, propertyName);
            return Guid.TryParse(value, out var guid) ? guid : null;
            #endregion
        }

        private static DateTime? getDate(JsonElement element, string propertyName)
        {
            #region implementation
            var value = getString(element, propertyName);
            return DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var date)
                ? date
                : null;
            #endregion
        }
    }
}
