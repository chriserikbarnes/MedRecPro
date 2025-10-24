using MedRecPro.Helpers;
using MedRecPro.Models;
using MedRecPro.Service.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestTools.UnitTesting.Logging;
using Moq;
using static MedRecPro.Models.Label;


namespace MedRecPro.Service.Test
{
    /**************************************************************/
    /// <summary>
    /// Unit tests for product rendering service with proper dictionary-based DTO support.
    /// </summary>
    /// <seealso cref="IProductRenderingService"/>
    /// <seealso cref="Label.Product"/>
    [TestClass]
    public class ProductRenderingServiceTests
    {
        #region configuration

        /**************************************************************/
        /// <summary>
        /// Gets a null logger for testing scenarios where logging is not validated.
        /// NullLogger discards all log messages without side effects.
        /// </summary>
        private static ILogger<PackagingLevel> Logger => NullLogger<PackagingLevel>.Instance;



        private static IConfiguration? _configuration;
        private IEncryptionService? _encryptionService;
        private IDictionaryUtilityService? _dictionaryUtilityService;
         
        /**************************************************************/
        /// <summary>
        /// Sets up test environment before each test method.
        /// </summary>
        [TestInitialize]
        public void TestInitialize()
        {

            // Create real service instances
            _encryptionService = new EncryptionService(Configuration);
            _dictionaryUtilityService = new DictionaryUtilityService();

            // Initialize Util with real services
            var mockHttpContextAccessor = new Mock<IHttpContextAccessor>();
            Util.Initialize(
                mockHttpContextAccessor.Object,
                _encryptionService,
                _dictionaryUtilityService);
        }

        /**************************************************************/
        /// <summary>
        /// Gets the configuration with user secrets for testing.
        /// Lazy-loads configuration on first access to avoid overhead.
        /// </summary>
        private static IConfiguration Configuration
        {
            get
            {
                if (_configuration == null)
                {
                    _configuration = new ConfigurationBuilder()
                        .AddUserSecrets<ProductRenderingServiceTests>()
                        .Build();
                }
                return _configuration;
            }
        }

        #endregion

        #region test helper methods

        /**************************************************************/
        /// <summary>
        /// Creates a test ProductDto with properly populated dictionaries.
        /// Ensures all nested DTOs use dictionary-based property access patterns.
        /// </summary>
        /// <returns>ProductDto with realistic test data for service validation</returns>
        /// <seealso cref="ProductDto"/>
        /// <seealso cref="Label.Product"/>
        private ProductDto createTestProductDto()
        {
            #region implementation

            return new ProductDto
            {
                // Populate the underlying dictionary - all properties are computed from this
                Product = new Dictionary<string, object?>
                {
                    { nameof(ProductDto.ProductID), 123 },
                    { nameof(ProductDto.ProductName), "Test Product" },
                    { nameof(ProductDto.ProductSuffix), "XR" },
                    { nameof(ProductDto.FormCode), "TAB" },
                    { nameof(ProductDto.FormCodeSystem), "2.16.840.1.113883.3.26.1.1" },
                    { nameof(ProductDto.FormDisplayName), "Tablet" },
                    { nameof(ProductDto.DescriptionText), "Test description" }
                },
                ProductIdentifiers = new List<ProductIdentifierDto>
                {
                    new ProductIdentifierDto
                    {
                        ProductIdentifier = new Dictionary<string, object?>
                        {
                            { nameof(ProductIdentifierDto.IdentifierType), "NDC" },
                            { nameof(ProductIdentifierDto.IdentifierValue), "12345-678-90" },
                            { nameof(ProductIdentifierDto.IdentifierSystemOID), "2.16.840.1.113883.6.69" }
                        }
                    }
                },
                Ingredients = new List<IngredientDto>
                {
                    new IngredientDto
                    {
                        Ingredient = new Dictionary<string, object?>
                        {
                            { nameof(IngredientDto.OriginatingElement), "activeIngredient" },
                            { nameof(IngredientDto.SequenceNumber), 1 }
                        }
                    },
                    new IngredientDto
                    {
                        Ingredient = new Dictionary<string, object?>
                        {
                            { nameof(IngredientDto.OriginatingElement), "inactiveIngredient" },
                            { nameof(IngredientDto.SequenceNumber), 1 }
                        }
                    }
                },
                Characteristics = new List<CharacteristicDto>
                {
                    new CharacteristicDto
                    {
                        Characteristic = new Dictionary<string, object?>
                        {
                            { nameof(CharacteristicDto.CharacteristicID), 1 },
                            { nameof(CharacteristicDto.PackagingLevelID), null }
                        }
                    },
                    new CharacteristicDto
                    {
                        Characteristic = new Dictionary<string, object?>
                        {
                            { nameof(CharacteristicDto.CharacteristicID), 2 },
                            { nameof(CharacteristicDto.PackagingLevelID), null }
                        }
                    }
                },
                GenericMedicines = new List<GenericMedicineDto>
                {
                    new GenericMedicineDto
                    {
                        GenericMedicine = new Dictionary<string, object?>
                        {
                            { nameof(GenericMedicineDto.GenericName), "Generic Medicine 1" }
                        }
                    }
                }
            };

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates a test PackagingLevelDto with properly populated dictionary.
        /// Configures packaging hierarchy based on the specified parameters.
        /// </summary>
        /// <param name="id">Packaging level identifier for unique identification</param>
        /// <param name="childPackaging">Optional child packaging to create hierarchy relationship</param>
        /// <param name="packageCode">Optional package code for business duplicate testing</param>
        /// <param name="packageCodeSystem">Optional package code system</param>
        /// <param name="formCode">Optional form code</param>
        /// <param name="formCodeSystem">Optional form code system</param>
        /// <param name="formDisplayName">Optional form display name</param>
        /// <param name="quantityNumerator">Optional quantity numerator</param>
        /// <param name="quantityDenominator">Optional quantity denominator</param>
        /// <returns>PackagingLevelDto with test data configured according to parameters</returns>
        /// <seealso cref="PackagingLevelDto"/>
        /// <seealso cref="Label.PackagingLevel"/>
        private PackagingLevelDto createTestPackagingLevel(
          int id,
          PackagingLevelDto? childPackaging = null,
          string? packageCode = null,
          string? packageCodeSystem = null,
          string? formCode = null,
          string? formCodeSystem = null,
          string? formDisplayName = null,
          decimal? quantityNumerator = null,
          decimal? quantityDenominator = null)
        {
            #region implementation

            var secret = Configuration["Security:DB:PKSecret"];

            var packagingLevel = new PackagingLevel
            {
                PackagingLevelID = id,
                PackageCode = packageCode ?? $"PKG{id}",
                PackageCodeSystem = packageCodeSystem,
                PackageFormCode = formCode ?? "BOT",
                PackageFormCodeSystem = formCodeSystem,
                PackageFormDisplayName = formDisplayName ?? "Bottle",
                QuantityNumerator = quantityNumerator,
                QuantityDenominator = quantityDenominator
            };

            // Convert entity to dictionary with encrypted IDs
            var packagingLevelDict = packagingLevel.ToEntityWithEncryptedId(secret!, Logger);

            // Create the DTO with the encrypted dictionary
            var packagingLevelDto = new PackagingLevelDto
            {
                PackagingLevel = packagingLevelDict
            };

            // If a child packaging is provided, create a hierarchy relationship
            if (childPackaging != null)
            {
                packagingLevelDto.PackagingHierarchy = new List<PackagingHierarchyDto>
                {
                    new PackagingHierarchyDto
                    {
                        PackagingHierarchy = new Dictionary<string, object?>(),
                        ChildPackagingLevel = childPackaging
                    }
                };
            }

            return packagingLevelDto;

            #endregion
        }

        #endregion

        #region core functionality tests

        /**************************************************************/
        /// <summary>
        /// Tests that PrepareForRendering with valid ProductDto returns enhanced context.
        /// Verifies all pre-computed flags and collections are correctly populated.
        /// UPDATED: Fixed parameter order to match current method signature.
        /// </summary>
        /// <seealso cref="IProductRenderingService.PrepareForRendering"/>
        /// <seealso cref="Label.Product"/>
        [TestMethod]
        public void PrepareForRendering_WithValidProductDto_ReturnsEnhancedContext()
        {
            #region implementation

            // Arrange - Create test product with dictionary-based properties
            var testProductDto = createTestProductDto();
            var service = new ProductRenderingService();
            var packageService = new PackageRenderingService();

            // Act - Process the product through rendering service
            // FIXED: Pass packageService as the 4th parameter (packageRenderingService), not 2nd
            var result = service.PrepareForRendering(
                product: testProductDto,
                additionalParams: null,
                ingredientRenderingService: null,
                packageRenderingService: packageService,
                characteristicRenderingService: null);

            // Assert - Verify the service created a valid result with proper context
            Assert.IsNotNull(result);
            Assert.AreEqual(testProductDto, result.ProductDto);

            // Verify pre-computed boolean flags are correctly calculated
            Assert.IsTrue(result.HasValidData);
            Assert.IsTrue(result.HasNdcIdentifier);
            Assert.IsTrue(result.HasActiveIngredients);
            Assert.IsTrue(result.HasInactiveIngredients);
            Assert.IsTrue(result.HasCharacteristics);
            Assert.IsTrue(result.HasGenericMedicines);

            // Verify pre-computed collections contain expected data
            Assert.IsNotNull(result.NdcProductIdentifier);
            Assert.AreEqual("12345-678-90", result.NdcProductIdentifier.IdentifierValue);
            Assert.AreEqual(1, result.OrderedActiveIngredients?.Count);
            Assert.AreEqual(1, result.OrderedInactiveIngredients?.Count);
            Assert.AreEqual(2, result.OrderedCharacteristics?.Count);

            // Verify that computed properties work correctly from the dictionary
            Assert.AreEqual("Test Product", result.ProductDto.ProductName);
            Assert.AreEqual("XR", result.ProductDto.ProductSuffix);
            Assert.AreEqual("TAB", result.ProductDto.FormCode);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Tests PrepareForRendering with all optional rendering services provided.
        /// Verifies that the service properly integrates all enhancement services.
        /// </summary>
        /// <seealso cref="IProductRenderingService.PrepareForRendering"/>
        /// <seealso cref="Label.Product"/>
        [TestMethod]
        public void PrepareForRendering_WithAllServices_ReturnsFullyEnhancedContext()
        {
            #region implementation

            // Arrange - Create test product and all rendering services
            var testProductDto = createTestProductDto();
            var service = new ProductRenderingService();
            var packageService = new PackageRenderingService();
            var characteristicService = new CharacteristicRenderingService();

            // Act - Process with all services provided
            var result = service.PrepareForRendering(
                product: testProductDto,
                additionalParams: null,
                ingredientRenderingService: null,
                packageRenderingService: packageService,
                characteristicRenderingService: characteristicService);

            // Assert - Verify enhanced context
            Assert.IsNotNull(result);
            Assert.IsTrue(result.HasValidData);
            Assert.IsTrue(result.HasCharacteristics);
            Assert.AreEqual(2, result.OrderedCharacteristics?.Count);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Tests PrepareForRendering behavior with minimal product data.
        /// Verifies service handles products with only required fields gracefully.
        /// </summary>
        /// <seealso cref="IProductRenderingService.PrepareForRendering"/>
        /// <seealso cref="Label.Product"/>
        [TestMethod]
        public void PrepareForRendering_WithMinimalProductData_ReturnsValidContext()
        {
            #region implementation

            // Arrange - Create minimal product with just basic info
            var minimalProduct = new ProductDto
            {
                Product = new Dictionary<string, object?>
                {
                    { nameof(ProductDto.ProductID), 1 },
                    { nameof(ProductDto.ProductName), "Minimal Product" }
                }
            };

            var service = new ProductRenderingService();

            // Act - Process minimal product
            var result = service.PrepareForRendering(product: minimalProduct);

            // Assert - Should handle gracefully with false flags for missing data
            Assert.IsNotNull(result);
            Assert.IsTrue(result.HasValidData); // Has name so should be valid
            Assert.IsFalse(result.HasNdcIdentifier);
            Assert.IsFalse(result.HasActiveIngredients);
            Assert.IsFalse(result.HasInactiveIngredients);
            Assert.IsFalse(result.HasCharacteristics);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Tests GetOrderedActiveIngredients with multiple active ingredients.
        /// Verifies proper filtering and ordering based on sequence numbers.
        /// </summary>
        /// <seealso cref="IProductRenderingService.GetOrderedActiveIngredients"/>
        /// <seealso cref="Label.Ingredient"/>
        [TestMethod]
        public void GetOrderedActiveIngredients_WithMultipleIngredients_ReturnsOrderedActive()
        {
            #region implementation

            // Arrange - Product with multiple active and inactive ingredients
            var productDto = new ProductDto
            {
                Product = new Dictionary<string, object?>(),
                Ingredients = new List<IngredientDto>
                {
                    new IngredientDto
                    {
                        Ingredient = new Dictionary<string, object?>
                        {
                            { nameof(IngredientDto.OriginatingElement), "activeIngredient" },
                            { nameof(IngredientDto.SequenceNumber), 2 }
                        }
                    },
                    new IngredientDto
                    {
                        Ingredient = new Dictionary<string, object?>
                        {
                            { nameof(IngredientDto.OriginatingElement), "inactiveIngredient" },
                            { nameof(IngredientDto.SequenceNumber), 1 }
                        }
                    },
                    new IngredientDto
                    {
                        Ingredient = new Dictionary<string, object?>
                        {
                            { nameof(IngredientDto.OriginatingElement), "activeIngredient" },
                            { nameof(IngredientDto.SequenceNumber), 1 }
                        }
                    }
                }
            };

            var service = new ProductRenderingService();

            // Act - Filter and order active ingredients
            var result = service.GetOrderedActiveIngredients(productDto);

            // Assert - Should return only active ingredients in proper sequence order
            Assert.IsNotNull(result);
            Assert.AreEqual(2, result.Count);
            Assert.AreEqual(1, result[0].SequenceNumber);
            Assert.AreEqual(2, result[1].SequenceNumber);

            // Verify all are active ingredients
            foreach (var ingredient in result)
            {
                Assert.AreEqual("activeIngredient", ingredient.OriginatingElement);
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Tests GetOrderedInactiveIngredients with multiple inactive ingredients.
        /// Verifies proper filtering and ordering based on sequence numbers.
        /// </summary>
        /// <seealso cref="IProductRenderingService.GetOrderedInactiveIngredients"/>
        /// <seealso cref="Label.Ingredient"/>
        [TestMethod]
        public void GetOrderedInactiveIngredients_WithMultipleIngredients_ReturnsOrderedInactive()
        {
            #region implementation

            // Arrange - Product with multiple inactive ingredients
            var productDto = new ProductDto
            {
                Product = new Dictionary<string, object?>(),
                Ingredients = new List<IngredientDto>
                {
                    new IngredientDto
                    {
                        Ingredient = new Dictionary<string, object?>
                        {
                            { nameof(IngredientDto.OriginatingElement), "inactiveIngredient" },
                            { nameof(IngredientDto.SequenceNumber), 2 }
                        }
                    },
                    new IngredientDto
                    {
                        Ingredient = new Dictionary<string, object?>
                        {
                            { nameof(IngredientDto.OriginatingElement), "activeIngredient" },
                            { nameof(IngredientDto.SequenceNumber), 1 }
                        }
                    },
                    new IngredientDto
                    {
                        Ingredient = new Dictionary<string, object?>
                        {
                            { nameof(IngredientDto.OriginatingElement), "inactiveIngredient" },
                            { nameof(IngredientDto.SequenceNumber), 1 }
                        }
                    }
                }
            };

            var service = new ProductRenderingService();

            // Act - Filter and order inactive ingredients
            var result = service.GetOrderedInactiveIngredients(productDto);

            // Assert - Should return only inactive ingredients in proper order
            Assert.IsNotNull(result);
            Assert.AreEqual(2, result.Count);
            Assert.AreEqual(1, result[0].SequenceNumber);
            Assert.AreEqual(2, result[1].SequenceNumber);

            // Verify all are inactive ingredients
            foreach (var ingredient in result)
            {
                Assert.AreEqual("inactiveIngredient", ingredient.OriginatingElement);
            }

            #endregion
        }

        #endregion

        #region packaging tests

        /**************************************************************/
        /// <summary>
        /// Tests that GetOrderedTopLevelPackaging filters correctly based on hierarchy relationships.
        /// Verifies identification of top-level packages by checking which packages appear as children.
        /// UPDATED: Test now reflects the current two-stage filtering logic (hierarchy + business duplicates).
        /// </summary>
        /// <seealso cref="IProductRenderingService.GetOrderedTopLevelPackaging"/>
        /// <seealso cref="Label.PackagingLevel"/>
        [TestMethod]
        public void GetOrderedTopLevelPackaging_WithHierarchicalPackaging_ReturnsOnlyTopLevel()
        {
            #region implementation

            // Arrange - Create packaging hierarchy where:
            // Package 2 is referenced as a child of Package 1
            // Package 3 is standalone with no hierarchy relationships
            var package2 = createTestPackagingLevel(2);
            var package3 = createTestPackagingLevel(3);
            var package1 = createTestPackagingLevel(1, childPackaging: package2);

            var productDto = new ProductDto
            {
                Product = new Dictionary<string, object?>(),
                PackagingLevels = new List<PackagingLevelDto>
                {
                    package1,  // Parent with child - IS top level
                    package2,  // Referenced as child - NOT top level  
                    package3   // Standalone - IS top level
                }
            };

            var service = new ProductRenderingService();

            // Act - Filter for top-level packaging only
            var result = service.GetOrderedTopLevelPackaging(productDto);

            // Assert - Should return only packages 1 and 3 (packages that don't appear as children)
            Assert.IsNotNull(result);
            Assert.AreEqual(2, result.Count);
            Assert.AreEqual(1, result[0].PackagingLevelID);
            Assert.AreEqual(3, result[1].PackagingLevelID);

            // Verify computed properties work correctly from dictionaries
            Assert.AreEqual("PKG1", result[0].PackageCode);
            Assert.AreEqual("PKG3", result[1].PackageCode);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Tests GetOrderedTopLevelPackaging with nested multi-level hierarchy.
        /// Verifies recursive traversal correctly identifies all child packages at any depth.
        /// </summary>
        /// <seealso cref="IProductRenderingService.GetOrderedTopLevelPackaging"/>
        /// <seealso cref="Label.PackagingLevel"/>
        [TestMethod]
        public void GetOrderedTopLevelPackaging_WithNestedHierarchy_ExcludesAllChildLevels()
        {
            #region implementation

            // Arrange - Create multi-level hierarchy:
            // Package 1 -> Package 2 -> Package 3 (nested)
            // Package 4 (standalone)
            var package3 = createTestPackagingLevel(3);
            var package2 = createTestPackagingLevel(2, childPackaging: package3);
            var package1 = createTestPackagingLevel(1, childPackaging: package2);
            var package4 = createTestPackagingLevel(4);

            var productDto = new ProductDto
            {
                Product = new Dictionary<string, object?>(),
                PackagingLevels = new List<PackagingLevelDto>
                {
                    package1,
                    package2,
                    package3,
                    package4
                }
            };

            var service = new ProductRenderingService();

            // Act - Filter for top-level packaging
            var result = service.GetOrderedTopLevelPackaging(productDto);

            // Assert - Should return only packages 1 and 4 (2 and 3 are children at various levels)
            Assert.IsNotNull(result);
            Assert.AreEqual(2, result.Count);
            Assert.AreEqual(1, result[0].PackagingLevelID);
            Assert.AreEqual(4, result[1].PackagingLevelID);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Tests GetOrderedTopLevelPackaging with business duplicate filtering.
        /// Verifies that packages with identical business attributes to children are excluded.
        /// </summary>
        /// <seealso cref="IProductRenderingService.GetOrderedTopLevelPackaging"/>
        /// <seealso cref="Label.PackagingLevel"/>
        [TestMethod]
        public void GetOrderedTopLevelPackaging_WithBusinessDuplicates_ExcludesDuplicates()
        {
            #region implementation

            // Arrange - Create packages where:
            // Package 1 is parent with child Package 2
            // Package 3 has same business attributes as Package 2 (business duplicate)

            // Create Package 2 with specific business attributes
            var package2 = createTestPackagingLevel(
                id: 2,
                packageCode: "SHARED-CODE",
                packageCodeSystem: "2.16.840.1.113883.6.69",
                formCode: "BOT",
                formCodeSystem: "2.16.840.1.113883.3.26.1.1",
                formDisplayName: "Bottle",
                quantityNumerator: 100m,
                quantityDenominator: 1m);

            // Create Package 1 as parent of Package 2
            var package1 = createTestPackagingLevel(
                id: 1,
                childPackaging: package2,
                formCode: "CASE",
                formDisplayName: "Case");

            // Create Package 3 as business duplicate of Package 2
            var package3 = createTestPackagingLevel(
                id: 3,
                packageCode: "SHARED-CODE",
                packageCodeSystem: "2.16.840.1.113883.6.69",
                formCode: "BOT",
                formCodeSystem: "2.16.840.1.113883.3.26.1.1",
                formDisplayName: "Bottle",
                quantityNumerator: 100m,
                quantityDenominator: 1m);

            var productDto = new ProductDto
            {
                Product = new Dictionary<string, object?>(),
                PackagingLevels = new List<PackagingLevelDto> { package1, package2, package3 }
            };

            var service = new ProductRenderingService();

            // Act - Filter for top-level packaging
            var result = service.GetOrderedTopLevelPackaging(productDto);

            // Assert - Should return only Package 1 (Package 2 is a child, Package 3 is a business duplicate)
            Assert.IsNotNull(result);
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(1, result[0].PackagingLevelID);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Tests GetOrderedTopLevelPackaging with no packaging levels.
        /// Verifies graceful handling of null or empty packaging collections.
        /// </summary>
        /// <seealso cref="IProductRenderingService.GetOrderedTopLevelPackaging"/>
        [TestMethod]
        public void GetOrderedTopLevelPackaging_WithNoPackaging_ReturnsNull()
        {
            #region implementation

            // Arrange - Product with null packaging levels
            var productDto = new ProductDto
            {
                Product = new Dictionary<string, object?>(),
                PackagingLevels = null
            };

            var service = new ProductRenderingService();

            // Act - Attempt to get top-level packaging
            var result = service.GetOrderedTopLevelPackaging(productDto);

            // Assert - Should return null gracefully
            Assert.IsNull(result);

            #endregion
        }

        #endregion

        #region validation tests

        /**************************************************************/
        /// <summary>
        /// Tests that HasValidData returns true for products with minimal required data.
        /// Verifies validation logic accepts products with basic required information.
        /// </summary>
        /// <seealso cref="IProductRenderingService.HasValidData"/>
        /// <seealso cref="Label.Product"/>
        [TestMethod]
        public void HasValidData_WithProductName_ReturnsTrue()
        {
            #region implementation

            // Arrange - Product with just a name in the dictionary
            var productDto = new ProductDto
            {
                Product = new Dictionary<string, object?>
                {
                    { nameof(ProductDto.ProductName), "Test Product" }
                }
            };

            var service = new ProductRenderingService();

            // Act - Validate product data completeness
            var result = service.HasValidData(productDto);

            // Assert - Should consider product with name as valid
            Assert.IsTrue(result);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Tests that HasValidData returns false for empty products.
        /// Ensures validation properly identifies products lacking required data.
        /// </summary>
        /// <seealso cref="IProductRenderingService.HasValidData"/>
        [TestMethod]
        public void HasValidData_WithEmptyProduct_ReturnsFalse()
        {
            #region implementation

            // Arrange - Empty product with no data in dictionary
            var productDto = new ProductDto
            {
                Product = new Dictionary<string, object?>()
            };

            var service = new ProductRenderingService();

            // Act - Validate empty product data
            var result = service.HasValidData(productDto);

            // Assert - Should consider empty product as invalid
            Assert.IsFalse(result);

            #endregion
        }

        #endregion

        #region edge case tests

        /**************************************************************/
        /// <summary>
        /// Tests service behavior with null collections.
        /// Ensures graceful handling of null collection properties without errors.
        /// </summary>
        /// <seealso cref="IProductRenderingService"/>
        [TestMethod]
        public void ServiceMethods_WithNullCollections_HandleGracefully()
        {
            #region implementation

            // Arrange - Product with null collections to test error handling
            var productDto = new ProductDto
            {
                Product = new Dictionary<string, object?>()
            };

            var service = new ProductRenderingService();

            // Act & Assert - All methods should handle null collections gracefully
            Assert.IsNull(service.GetNdcProductIdentifier(productDto));
            Assert.IsNull(service.GetOrderedActiveIngredients(productDto));
            Assert.IsNull(service.GetOrderedInactiveIngredients(productDto));
            Assert.IsNull(service.GetOrderedCharacteristics(productDto));
            Assert.IsNull(service.GetOrderedTopLevelPackaging(productDto));
            Assert.IsNull(service.GetOrderedRoutes(productDto));

            // Prepare for rendering should still work with proper flag settings
            var result = service.PrepareForRendering(productDto);
            Assert.IsNotNull(result);
            Assert.IsFalse(result.HasNdcIdentifier);
            Assert.IsFalse(result.HasActiveIngredients);
            Assert.IsFalse(result.HasInactiveIngredients);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Tests PrepareForRendering with null product argument.
        /// Verifies proper exception handling for invalid input.
        /// </summary>
        /// <seealso cref="IProductRenderingService.PrepareForRendering"/>
        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void PrepareForRendering_WithNullProduct_ThrowsArgumentNullException()
        {
            #region implementation

            // Arrange
            var service = new ProductRenderingService();

            // Act - Should throw ArgumentNullException
            service.PrepareForRendering(product: null!);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Tests GetOrderedCharacteristics with multiple characteristics.
        /// Verifies proper ordering by CharacteristicID.
        /// NOTE: GetOrderedCharacteristics filters for product-level characteristics only (PackagingLevelID == null).
        /// </summary>
        /// <seealso cref="IProductRenderingService.GetOrderedCharacteristics"/>
        /// <seealso cref="Label.Characteristic"/>
        [TestMethod]
        public void GetOrderedCharacteristics_WithMultipleCharacteristics_ReturnsOrdered()
        {
            #region implementation

            // Arrange - Product with multiple characteristics in non-sequential order
            // IMPORTANT: Must set PackagingLevelID = null for product-level characteristics
            // Arrange - Product with multiple characteristics in non-sequential order
            var secret = Configuration["Security:DB:PKSecret"];

            // Create the product entity and convert to encrypted entity
            var product = new Product
            {
                // Add any product properties here
            };

            var productDict = product.ToEntityWithEncryptedId(secret!, Logger);

            var productDto = new ProductDto
            {
                Product = productDict,
                Characteristics = new List<CharacteristicDto>
                {
                    // Characteristic 3
                    new CharacteristicDto
                    {
                        Characteristic = new Characteristic
                        {
                            CharacteristicID = 3,
                            PackagingLevelID = null
                            // Add other characteristic properties as needed
                        }.ToEntityWithEncryptedId(secret!, Logger)
                    },
                    // Characteristic 1
                    new CharacteristicDto
                    {
                        Characteristic = new Characteristic
                        {
                            CharacteristicID = 1,
                            PackagingLevelID = null
                        }.ToEntityWithEncryptedId(secret!, Logger)
                    },
                    // Characteristic 2
                    new CharacteristicDto
                    {
                        Characteristic = new Characteristic
                        {
                            CharacteristicID = 2,
                            PackagingLevelID = null
                        }.ToEntityWithEncryptedId(secret!, Logger)
                    }
                }
            };

            var service = new ProductRenderingService();

            // Act - Get ordered characteristics
            var result = service.GetOrderedCharacteristics(productDto);

            // Assert - Should be ordered by CharacteristicID
            Assert.IsNotNull(result);
            Assert.AreEqual(3, result.Count);
            Assert.AreEqual(1, result[0].CharacteristicID);
            Assert.AreEqual(2, result[1].CharacteristicID);
            Assert.AreEqual(3, result[2].CharacteristicID);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Tests GetNdcProductIdentifier with multiple identifiers.
        /// Verifies correct identification and extraction of NDC identifier.
        /// </summary>
        /// <seealso cref="IProductRenderingService.GetNdcProductIdentifier"/>
        /// <seealso cref="Label.ProductIdentifier"/>
        [TestMethod]
        public void GetNdcProductIdentifier_WithMultipleIdentifiers_ReturnsNdcOnly()
        {
            #region implementation

            // Arrange - Product with multiple identifier types
            var productDto = new ProductDto
            {
                Product = new Dictionary<string, object?>(),
                ProductIdentifiers = new List<ProductIdentifierDto>
                {
                    new ProductIdentifierDto
                    {
                        ProductIdentifier = new Dictionary<string, object?>
                        {
                            { nameof(ProductIdentifierDto.IdentifierType), "UPC" },
                            { nameof(ProductIdentifierDto.IdentifierValue), "123456789012" }
                        }
                    },
                    new ProductIdentifierDto
                    {
                        ProductIdentifier = new Dictionary<string, object?>
                        {
                            { nameof(ProductIdentifierDto.IdentifierType), "NDC" },
                            { nameof(ProductIdentifierDto.IdentifierValue), "12345-678-90" }
                        }
                    }
                }
            };

            var service = new ProductRenderingService();

            // Act - Get NDC identifier
            var result = service.GetNdcProductIdentifier(productDto);

            // Assert - Should return only the NDC identifier
            Assert.IsNotNull(result);
            Assert.AreEqual("NDC", result.IdentifierType);
            Assert.AreEqual("12345-678-90", result.IdentifierValue);

            #endregion
        }

        #endregion

        #region marketing status tests

        /**************************************************************/
        /// <summary>
        /// Tests that marketing status properties are correctly computed in PrepareForRendering.
        /// Verifies HasMarketingAct flag and OrderedMarketingStatuses collection.
        /// </summary>
        /// <seealso cref="IProductRenderingService.PrepareForRendering"/>
        /// <seealso cref="Label.MarketingStatus"/>
        [TestMethod]
        public void PrepareForRendering_WithMarketingStatuses_ComputesMarketingProperties()
        {
            #region implementation

            // Arrange - Product with marketing statuses
            var productDto = new ProductDto
            {
                Product = new Dictionary<string, object?>
                {
                    { nameof(ProductDto.ProductName), "Test Product" }
                },
                MarketingStatuses = new List<MarketingStatusDto>
                {
                    new MarketingStatusDto
                    {
                        MarketingStatus = new Dictionary<string, object?>
                        {
                            { nameof(MarketingStatusDto.MarketingStatusID), 1 },
                            { nameof(MarketingStatusDto.StatusCode), "active" }
                        }
                    }
                }
            };

            var service = new ProductRenderingService();

            // Act - Prepare for rendering
            var result = service.PrepareForRendering(product: productDto);

            // Assert - Marketing status properties should be computed
            Assert.IsNotNull(result);
            Assert.IsTrue(result.HasMarketingAct);
            Assert.IsNotNull(result.OrderedMarketingStatuses);
            Assert.AreEqual(1, result.OrderedMarketingStatuses.Count);

            #endregion
        }

        #endregion
    }
}