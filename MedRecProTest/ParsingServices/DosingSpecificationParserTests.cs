using MedRecProImportClass.Service.ParsingServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static MedRecProImportClass.Models.Label;

namespace MedRecPro.Service.Test.ParsingServices
{
    /**************************************************************/
    /// <summary>
    /// Tests public factory and parser methods on <see cref="DosingSpecificationParser"/>.
    /// </summary>
    /// <remarks>
    /// Coverage focuses on default object creation, null/invalid guard behavior, and persistence from
    /// representative consumedIn/substanceAdministration XML.
    /// </remarks>
    /// <seealso cref="DosingSpecificationParser"/>
    /// <seealso cref="DosingSpecification"/>
    [TestClass]
    public class DosingSpecificationParserTests
    {
        #region Factory Tests

        /**************************************************************/
        /// <summary>
        /// Verifies that the default dosing specification uses the oral route default.
        /// </summary>
        /// <seealso cref="DosingSpecificationParser.CreateDefault"/>
        [TestMethod]
        public void CreateDefault_DefaultsToOralRoute()
        {
            #region implementation
            var result = DosingSpecificationParser.CreateDefault(12);

            Assert.AreEqual(12, result.ProductID);
            Assert.AreEqual("ORAL", result.RouteCode);
            Assert.AreEqual("2.16.840.1.113883.3.26.1.1", result.RouteCodeSystem);
            Assert.AreEqual("Oral route", result.RouteDisplayName);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that custom dose values are copied into the default entity.
        /// </summary>
        /// <seealso cref="DosingSpecificationParser.CreateDefault"/>
        [TestMethod]
        public void CreateDefault_CustomDose_PopulatesDoseValueAndUnit()
        {
            #region implementation
            var result = DosingSpecificationParser.CreateDefault(12, "C38288", 500m, "mg");

            Assert.AreEqual("C38288", result.RouteCode);
            Assert.AreEqual(500m, result.DoseQuantityValue);
            Assert.AreEqual("mg", result.DoseQuantityUnit);
            #endregion
        }

        #endregion

        #region Validation Service Tests

        /**************************************************************/
        /// <summary>
        /// Verifies dosing specification validation accepts valid route, unit, dose, and entity inputs.
        /// </summary>
        /// <seealso cref="DosingSpecificationValidationService.ValidateDosingSpecification"/>
        /// <seealso cref="DosingSpecificationValidationService.ValidateRouteCode"/>
        /// <seealso cref="DosingSpecificationValidationService.ValidateDoseQuantity"/>
        /// <seealso cref="DosingSpecificationValidationService.ValidateUcumUnit"/>
        [TestMethod]
        public void DosingSpecificationValidationService_ValidInputs_ReturnValidResults()
        {
            #region implementation
            var validator = new DosingSpecificationValidationService(NullLogger.Instance);
            var dosingSpec = DosingSpecificationParser.CreateDefault(12, "C38288", 500m, "mg");

            Assert.IsTrue(validator.ValidateDosingSpecification(dosingSpec).IsValid);
            Assert.IsTrue(validator.ValidateRouteCode("C38288", "2.16.840.1.113883.3.26.1.1").IsValid);
            Assert.IsTrue(validator.ValidateDoseQuantity(500m, "mg").IsValid);
            Assert.IsTrue(validator.ValidateUcumUnit("mg").IsValid);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies parser validation result helpers aggregate errors.
        /// </summary>
        /// <seealso cref="ValidationResult.AddError"/>
        /// <seealso cref="ValidationResult.MergeWith"/>
        [TestMethod]
        public void ValidationResult_AddErrorAndMergeWith_AggregatesErrors()
        {
            #region implementation
            var result = new ValidationResult();
            var nested = new ValidationResult();

            result.AddError("first");
            nested.AddError("second");
            result.MergeWith(nested);

            Assert.IsFalse(result.IsValid);
            CollectionAssert.AreEqual(new[] { "first", "second" }, result.Errors);
            #endregion
        }

        #endregion

        #region Build Tests

        /**************************************************************/
        /// <summary>
        /// Verifies that null required inputs return zero created rows.
        /// </summary>
        /// <returns>A task representing the asynchronous test.</returns>
        /// <seealso cref="DosingSpecificationParser.BuildDosingSpecificationAsync"/>
        [TestMethod]
        public async Task BuildDosingSpecificationAsync_NullInputs_ReturnsZero()
        {
            #region implementation
            using var database = ParsingServiceTestHelper.CreateDatabase();
            var parseContext = database.CreateParseContext();

            var result = await DosingSpecificationParser.BuildDosingSpecificationAsync(null!, null!, parseContext);

            Assert.AreEqual(0, result);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that a valid consumedIn structure creates a dosing specification row.
        /// </summary>
        /// <returns>A task representing the asynchronous test.</returns>
        /// <seealso cref="DosingSpecificationParser.BuildDosingSpecificationAsync"/>
        [TestMethod]
        public async Task BuildDosingSpecificationAsync_ValidConsumedInStructure_CreatesRecord()
        {
            #region implementation
            using var database = ParsingServiceTestHelper.CreateDatabase();
            var parseContext = database.CreateParseContext();
            var seed = await database.SeedCommonContextAsync(parseContext);

            var count = await DosingSpecificationParser.BuildDosingSpecificationAsync(
                ParsingServiceTestHelper.DosingSpecification(),
                seed.Product,
                parseContext);

            Assert.AreEqual(1, count);
            var saved = await database.DbContext.Set<DosingSpecification>().SingleAsync();
            Assert.AreEqual(seed.Product.ProductID, saved.ProductID);
            Assert.AreEqual("C38288", saved.RouteCode);
            Assert.AreEqual(500m, saved.DoseQuantityValue);
            Assert.AreEqual("mg", saved.DoseQuantityUnit);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that variable-dose XML without doseQuantity can still create a valid record.
        /// </summary>
        /// <returns>A task representing the asynchronous test.</returns>
        /// <seealso cref="DosingSpecificationParser.BuildDosingSpecificationAsync"/>
        [TestMethod]
        public async Task BuildDosingSpecificationAsync_MissingDoseQuantity_CreatesVariableDoseWhenValid()
        {
            #region implementation
            using var database = ParsingServiceTestHelper.CreateDatabase();
            var parseContext = database.CreateParseContext();
            var seed = await database.SeedCommonContextAsync(parseContext);
            var element = ParsingServiceTestHelper.Element("""
                <manufacturedProduct xmlns="urn:hl7-org:v3">
                  <consumedIn>
                    <substanceAdministration>
                      <routeCode code="C38288" codeSystem="2.16.840.1.113883.3.26.1.1" displayName="ORAL" />
                    </substanceAdministration>
                  </consumedIn>
                </manufacturedProduct>
                """);

            var count = await DosingSpecificationParser.BuildDosingSpecificationAsync(element, seed.Product, parseContext);

            Assert.AreEqual(1, count);
            var saved = await database.DbContext.Set<DosingSpecification>().SingleAsync();
            Assert.IsNull(saved.DoseQuantityValue);
            Assert.IsNull(saved.DoseQuantityUnit);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that invalid route or dose data does not create a row.
        /// </summary>
        /// <returns>A task representing the asynchronous test.</returns>
        /// <seealso cref="DosingSpecificationParser.BuildDosingSpecificationAsync"/>
        [TestMethod]
        public async Task BuildDosingSpecificationAsync_InvalidRouteOrDose_DoesNotCreateRecord()
        {
            #region implementation
            using var database = ParsingServiceTestHelper.CreateDatabase();
            var parseContext = database.CreateParseContext();
            var seed = await database.SeedCommonContextAsync(parseContext);
            var element = ParsingServiceTestHelper.Element("""
                <manufacturedProduct xmlns="urn:hl7-org:v3">
                  <consumedIn>
                    <substanceAdministration>
                      <routeCode code="INVALID" codeSystem="2.16.840.1.113883.3.26.1.1" displayName="Broken route" />
                      <doseQuantity value="-1" unit="mg" />
                    </substanceAdministration>
                  </consumedIn>
                </manufacturedProduct>
                """);

            var count = await DosingSpecificationParser.BuildDosingSpecificationAsync(element, seed.Product, parseContext);

            Assert.AreEqual(0, count);
            Assert.AreEqual(0, await database.DbContext.Set<DosingSpecification>().CountAsync());
            #endregion
        }

        #endregion
    }
}
