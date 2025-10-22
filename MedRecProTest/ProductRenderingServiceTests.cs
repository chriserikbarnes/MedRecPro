using MedRecPro.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

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
                            { nameof(CharacteristicDto.CharacteristicID), 1 }
                        }
                    },
                    new CharacteristicDto
                    {
                        Characteristic = new Dictionary<string, object?>
                        {
                            { nameof(CharacteristicDto.CharacteristicID), 2 }
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
        /// <param name="hasHierarchy">Whether to include packaging hierarchy relationships</param>
        /// <returns>PackagingLevelDto with test data configured according to parameters</returns>
        /// <seealso cref="PackagingLevelDto"/>
        /// <seealso cref="Label.PackagingLevel"/>
        private PackagingLevelDto createTestPackagingLevel(int id, bool hasHierarchy = false)
        {
            #region implementation

            var packagingLevel = new PackagingLevelDto
            {
                PackagingLevel = new Dictionary<string, object?>
                {
                    { nameof(PackagingLevelDto.PackagingLevelID), id },
                    { nameof(PackagingLevelDto.PackageCode), $"PKG{id}" },
                    { nameof(PackagingLevelDto.PackageFormCode), "BOT" },
                    { nameof(PackagingLevelDto.PackageFormDisplayName), "Bottle" }
                }
            };

            // Configure hierarchy based on test requirements
            if (hasHierarchy)
            {
                packagingLevel.PackagingHierarchy = new List<PackagingHierarchyDto>
                {
                    new PackagingHierarchyDto
                    {
                        PackagingHierarchy = new Dictionary<string, object?>()
                    }
                };
            }

            return packagingLevel;

            #endregion
        }

        #endregion

        #region core functionality tests

        /**************************************************************/
        /// <summary>
        /// Tests that PrepareForRendering with valid ProductDto returns enhanced context.
        /// Verifies all pre-computed flags and collections are correctly populated.
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
            var result = service.PrepareForRendering(product: testProductDto, packageService);

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
            Assert.AreEqual(123, testProductDto.ProductID);
            Assert.AreEqual("Test Product", testProductDto.ProductName);
            Assert.AreEqual("TAB", testProductDto.FormCode);
            Assert.AreEqual("XR", testProductDto.ProductSuffix);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Tests that PrepareForRendering with null ProductDto throws ArgumentNullException.
        /// Ensures proper parameter validation and error handling.
        /// </summary>
        /// <seealso cref="IProductRenderingService.PrepareForRendering"/>
        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void PrepareForRendering_WithNullProduct_ThrowsArgumentNullException()
        {
            #region implementation

            // Arrange - Create service instance for testing
            var service = new ProductRenderingService();

            // Act & Assert - Should throw ArgumentNullException for null input
            service.PrepareForRendering(product: null);

            #endregion
        }

        #endregion

        #region ndc identifier tests

        /**************************************************************/
        /// <summary>
        /// Tests that GetNdcProductIdentifier returns correct NDC identifier from multiple types.
        /// Verifies proper filtering and selection of NDC-specific identifiers.
        /// </summary>
        /// <seealso cref="IProductRenderingService.GetNdcProductIdentifier"/>
        /// <seealso cref="Label.ProductIdentifier"/>
        [TestMethod]
        public void GetNdcProductIdentifier_WithNdcIdentifier_ReturnsCorrectIdentifier()
        {
            #region implementation

            // Arrange - Create product with multiple identifier types including NDC
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
                            { nameof(ProductIdentifierDto.IdentifierValue), "12345-678-90" },
                            { nameof(ProductIdentifierDto.IdentifierSystemOID), "2.16.840.1.113883.6.69" }
                        }
                    },
                    new ProductIdentifierDto
                    {
                        ProductIdentifier = new Dictionary<string, object?>
                        {
                            { nameof(ProductIdentifierDto.IdentifierType), "OTHER" },
                            { nameof(ProductIdentifierDto.IdentifierValue), "OTHER-123" }
                        }
                    }
                }
            };

            var service = new ProductRenderingService();

            // Act - Extract NDC identifier from mixed collection
            var result = service.GetNdcProductIdentifier(productDto);

            // Assert - Should find the NDC identifier specifically among all types
            Assert.IsNotNull(result);
            Assert.AreEqual("NDC", result.IdentifierType);
            Assert.AreEqual("12345-678-90", result.IdentifierValue);
            Assert.AreEqual("2.16.840.1.113883.6.69", result.IdentifierSystemOID);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Tests that GetNdcProductIdentifier handles NDCProduct type as well.
        /// Verifies support for alternative NDC identifier type naming conventions.
        /// </summary>
        /// <seealso cref="IProductRenderingService.GetNdcProductIdentifier"/>
        /// <seealso cref="Label.ProductIdentifier"/>
        [TestMethod]
        public void GetNdcProductIdentifier_WithNdcProductType_ReturnsCorrectIdentifier()
        {
            #region implementation

            // Arrange - Test NDCProduct type specifically for alternative naming
            var productDto = new ProductDto
            {
                Product = new Dictionary<string, object?>(),
                ProductIdentifiers = new List<ProductIdentifierDto>
                {
                    new ProductIdentifierDto
                    {
                        ProductIdentifier = new Dictionary<string, object?>
                        {
                            { nameof(ProductIdentifierDto.IdentifierType), "NDCProduct" },
                            { nameof(ProductIdentifierDto.IdentifierValue), "98765-432-10" },
                            { nameof(ProductIdentifierDto.IdentifierSystemOID), "2.16.840.1.113883.6.69" }
                        }
                    }
                }
            };

            var service = new ProductRenderingService();

            // Act - Process NDCProduct type identifier
            var result = service.GetNdcProductIdentifier(productDto);

            // Assert - Should find NDCProduct type correctly
            Assert.IsNotNull(result);
            Assert.AreEqual("NDCProduct", result.IdentifierType);
            Assert.AreEqual("98765-432-10", result.IdentifierValue);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Tests that GetNdcProductIdentifier returns null when no NDC identifier exists.
        /// Ensures proper handling when NDC identifiers are absent from the collection.
        /// </summary>
        /// <seealso cref="IProductRenderingService.GetNdcProductIdentifier"/>
        [TestMethod]
        public void GetNdcProductIdentifier_WithoutNdcIdentifier_ReturnsNull()
        {
            #region implementation

            // Arrange - Product with no NDC identifiers, only other types
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
                            { nameof(ProductIdentifierDto.IdentifierValue), "123" }
                        }
                    },
                    new ProductIdentifierDto
                    {
                        ProductIdentifier = new Dictionary<string, object?>
                        {
                            { nameof(ProductIdentifierDto.IdentifierType), "GTIN" },
                            { nameof(ProductIdentifierDto.IdentifierValue), "456" }
                        }
                    }
                }
            };

            var service = new ProductRenderingService();

            // Act - Attempt to find NDC identifier in non-NDC collection
            var result = service.GetNdcProductIdentifier(productDto);

            // Assert - Should return null when no NDC identifiers present
            Assert.IsNull(result);

            #endregion
        }

        #endregion

        #region ingredient filtering tests

        /**************************************************************/
        /// <summary>
        /// Tests that GetOrderedActiveIngredients returns correctly filtered and ordered results.
        /// Verifies filtering by originating element and ordering by sequence number.
        /// </summary>
        /// <seealso cref="IProductRenderingService.GetOrderedActiveIngredients"/>
        /// <seealso cref="Label.Ingredient"/>
        [TestMethod]
        public void GetOrderedActiveIngredients_WithMixedIngredients_ReturnsOnlyActiveOrdered()
        {
            #region implementation

            // Arrange - Create ingredients with mixed types and out-of-order sequences
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
                            { nameof(IngredientDto.SequenceNumber), 3 } // Out of order
                        }
                    },
                    new IngredientDto
                    {
                        Ingredient = new Dictionary<string, object?>
                        {
                            { nameof(IngredientDto.OriginatingElement), "inactiveIngredient" },
                            { nameof(IngredientDto.SequenceNumber), 1 } // Should be filtered out
                        }
                    },
                    new IngredientDto
                    {
                        Ingredient = new Dictionary<string, object?>
                        {
                            { nameof(IngredientDto.OriginatingElement), "activeIngredient" },
                            { nameof(IngredientDto.SequenceNumber), 1 } // Should be first
                        }
                    },
                    new IngredientDto
                    {
                        Ingredient = new Dictionary<string, object?>
                        {
                            { nameof(IngredientDto.OriginatingElement), "activeIngredient" },
                            { nameof(IngredientDto.SequenceNumber), 2 } // Should be second
                        }
                    }
                }
            };

            var service = new ProductRenderingService();

            // Act - Filter and order active ingredients
            var result = service.GetOrderedActiveIngredients(productDto);

            // Assert - Should return only active ingredients in sequence order
            Assert.IsNotNull(result);
            Assert.AreEqual(3, result.Count);

            // Verify ordering by SequenceNumber (1, 2, 3)
            Assert.AreEqual(1, result[0].SequenceNumber);
            Assert.AreEqual(2, result[1].SequenceNumber);
            Assert.AreEqual(3, result[2].SequenceNumber);

            // Verify all are active ingredients
            foreach (var ingredient in result)
            {
                Assert.AreEqual("activeIngredient", ingredient.OriginatingElement);
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Tests that GetOrderedInactiveIngredients returns correctly filtered results.
        /// Ensures proper filtering for inactive ingredients and sequence ordering.
        /// </summary>
        /// <seealso cref="IProductRenderingService.GetOrderedInactiveIngredients"/>
        /// <seealso cref="Label.Ingredient"/>
        [TestMethod]
        public void GetOrderedInactiveIngredients_WithMixedIngredients_ReturnsOnlyInactiveOrdered()
        {
            #region implementation

            // Arrange - Mixed ingredient types with varying sequence numbers
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
                            { nameof(IngredientDto.SequenceNumber), 1 } // Should be filtered out
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
        /// Tests that GetOrderedTopLevelPackaging filters correctly for top-level only.
        /// Verifies identification of top-level packages based on hierarchy presence.
        /// </summary>
        /// <seealso cref="IProductRenderingService.GetOrderedTopLevelPackaging"/>
        /// <seealso cref="Label.PackagingLevel"/>
        [TestMethod]
        public void GetOrderedTopLevelPackaging_WithHierarchicalPackaging_ReturnsOnlyTopLevel()
        {
            #region implementation

            // Arrange - Create packaging levels with different hierarchy configurations
            var productDto = new ProductDto
            {
                Product = new Dictionary<string, object?>(),
                PackagingLevels = new List<PackagingLevelDto>
                {
                    createTestPackagingLevel(1, hasHierarchy: false), // Top level - null hierarchy
                    createTestPackagingLevel(2, hasHierarchy: false), // Top level - empty hierarchy  
                    createTestPackagingLevel(3, hasHierarchy: true)   // Not top level - has hierarchy
                }
            };

            // Set up specific hierarchy conditions for testing
            productDto.PackagingLevels[0].PackagingHierarchy = null; // Explicitly null
            productDto.PackagingLevels[1].PackagingHierarchy = new List<PackagingHierarchyDto>(); // Empty list

            var service = new ProductRenderingService();

            // Act - Filter for top-level packaging only
            var result = service.GetOrderedTopLevelPackaging(productDto);

            // Assert - Should return only the first two (top-level) packaging items
            Assert.IsNotNull(result);
            Assert.AreEqual(2, result.Count);
            Assert.AreEqual(1, result[0].PackagingLevelID);
            Assert.AreEqual(2, result[1].PackagingLevelID);

            // Verify computed properties work correctly from dictionaries
            Assert.AreEqual("PKG1", result[0].PackageCode);
            Assert.AreEqual("PKG2", result[1].PackageCode);

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

        #endregion
    }
}