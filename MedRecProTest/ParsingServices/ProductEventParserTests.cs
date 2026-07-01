using MedRecProImportClass.Service.ParsingServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static MedRecProImportClass.Models.Label;

namespace MedRecPro.Service.Test.ParsingServices
{
    /**************************************************************/
    /// <summary>
    /// Tests public factory and parser methods on <see cref="ProductEventParser"/>.
    /// </summary>
    /// <remarks>
    /// Coverage includes event factory validation, XML-driven creation, duplicate prevention,
    /// and bulk event creation from representative SPL productEvent snippets.
    /// </remarks>
    /// <seealso cref="ProductEventParser"/>
    /// <seealso cref="ProductEvent"/>
    [TestClass]
    public class ProductEventParserTests
    {
        #region Factory Tests

        /**************************************************************/
        /// <summary>
        /// Verifies that the default distributed event uses the FDA SPL code system.
        /// </summary>
        /// <seealso cref="ProductEventParser.CreateDefault"/>
        [TestMethod]
        public void CreateDefault_DistributedEvent_PopulatesFdaCodeSystem()
        {
            #region implementation
            var result = ProductEventParser.CreateDefault(44);

            Assert.AreEqual(44, result.PackagingLevelID);
            Assert.AreEqual("C106325", result.EventCode);
            Assert.AreEqual("2.16.840.1.113883.3.26.1.1", result.EventCodeSystem);
            Assert.AreEqual("Distributed per reporting interval", result.EventDisplayName);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that valid distributed event input returns a populated ProductEvent.
        /// </summary>
        /// <seealso cref="ProductEventParser.PopulateProductEvent"/>
        [TestMethod]
        public void PopulateProductEvent_ValidDistributedEvent_ReturnsEntity()
        {
            #region implementation
            using var database = ParsingServiceTestHelper.CreateDatabase();
            var parseContext = database.CreateParseContext();
            var effectiveTime = new DateTime(2025, 1, 1);

            var result = ProductEventParser.PopulateProductEvent(
                44,
                "C106325",
                "2.16.840.1.113883.3.26.1.1",
                "Distributed per reporting interval",
                12,
                "1",
                effectiveTime,
                parseContext);

            Assert.IsNotNull(result);
            Assert.AreEqual(44, result.PackagingLevelID);
            Assert.AreEqual(12, result.QuantityValue);
            Assert.AreEqual(effectiveTime, result.EffectiveTimeLow);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that invalid product event codes fail validation.
        /// </summary>
        /// <seealso cref="ProductEventParser.PopulateProductEvent"/>
        [TestMethod]
        public void PopulateProductEvent_InvalidEventCode_ReturnsNull()
        {
            #region implementation
            using var database = ParsingServiceTestHelper.CreateDatabase();
            var parseContext = database.CreateParseContext();

            var result = ProductEventParser.PopulateProductEvent(
                44,
                "INVALID",
                "2.16.840.1.113883.3.26.1.1",
                "Broken",
                12,
                "1",
                DateTime.UtcNow,
                parseContext);

            Assert.IsNull(result);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that distributed-event factory input creates a validated entity.
        /// </summary>
        /// <seealso cref="ProductEventParser.CreateDistributedEvent"/>
        [TestMethod]
        public void CreateDistributedEvent_ValidInput_ReturnsDistributedEvent()
        {
            #region implementation
            using var database = ParsingServiceTestHelper.CreateDatabase();
            var parseContext = database.CreateParseContext();
            var effectiveTime = new DateTime(2025, 1, 1);

            var result = ProductEventParser.CreateDistributedEvent(44, 12, effectiveTime, parseContext);

            Assert.IsNotNull(result);
            Assert.AreEqual("C106325", result.EventCode);
            Assert.AreEqual(12, result.QuantityValue);
            Assert.AreEqual(effectiveTime, result.EffectiveTimeLow);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that returned-event factory input creates a returned event without effective time.
        /// </summary>
        /// <seealso cref="ProductEventParser.CreateReturnedEvent"/>
        [TestMethod]
        public void CreateReturnedEvent_ValidInput_ReturnsReturnedEventWithoutEffectiveTime()
        {
            #region implementation
            using var database = ParsingServiceTestHelper.CreateDatabase();
            var parseContext = database.CreateParseContext();

            var result = ProductEventParser.CreateReturnedEvent(44, 2, parseContext);

            Assert.IsNotNull(result);
            Assert.AreEqual("C106328", result.EventCode);
            Assert.AreEqual(2, result.QuantityValue);
            Assert.IsNull(result.EffectiveTimeLow);
            #endregion
        }

        #endregion

        #region Validation Service Tests

        /**************************************************************/
        /// <summary>
        /// Verifies product event validation accepts the supported event contract.
        /// </summary>
        /// <seealso cref="ProductEventValidationService.ValidateProductEvent"/>
        /// <seealso cref="ProductEventValidationService.ValidateEventCode"/>
        /// <seealso cref="ProductEventValidationService.ValidateQuantity"/>
        /// <seealso cref="ProductEventValidationService.ValidateEffectiveTime"/>
        [TestMethod]
        public void ProductEventValidationService_ValidInputs_ReturnValidResults()
        {
            #region implementation
            var validator = new ProductEventValidationService(NullLogger.Instance);
            var productEvent = ProductEventParser.CreateDefault(44, "C106325", 12, new DateTime(2025, 1, 1));

            Assert.IsTrue(validator.ValidateProductEvent(productEvent).IsValid);
            Assert.IsTrue(validator.ValidateEventCode("C106325", "2.16.840.1.113883.3.26.1.1", "Distributed per reporting interval").IsValid);
            Assert.IsTrue(validator.ValidateQuantity(12, "1").IsValid);
            Assert.IsTrue(validator.ValidateEffectiveTime("C106325", new DateTime(2025, 1, 1)).IsValid);
            #endregion
        }

        #endregion

        #region Build Tests

        /**************************************************************/
        /// <summary>
        /// Verifies that XML product events create persisted rows for a packaging level.
        /// </summary>
        /// <returns>A task representing the asynchronous test.</returns>
        /// <seealso cref="ProductEventParser.BuildProductEventAsync"/>
        [TestMethod]
        public async Task BuildProductEventAsync_ValidSubjectOf_CreatesRecord()
        {
            #region implementation
            using var database = ParsingServiceTestHelper.CreateDatabase();
            var parseContext = database.CreateParseContext();
            var seed = await database.SeedCommonContextAsync(parseContext);

            var count = await ProductEventParser.BuildProductEventAsync(
                ParsingServiceTestHelper.ProductEvents(),
                seed.PackagingLevel,
                parseContext);

            Assert.AreEqual(2, count);
            Assert.AreEqual(2, await database.DbContext.Set<ProductEvent>().CountAsync());
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that reparsing the same event data does not create duplicate rows.
        /// </summary>
        /// <returns>A task representing the asynchronous test.</returns>
        /// <seealso cref="ProductEventParser.BuildProductEventAsync"/>
        [TestMethod]
        public async Task BuildProductEventAsync_DuplicateEvent_DoesNotDuplicate()
        {
            #region implementation
            using var database = ParsingServiceTestHelper.CreateDatabase();
            var parseContext = database.CreateParseContext();
            var seed = await database.SeedCommonContextAsync(parseContext);
            var eventsElement = ParsingServiceTestHelper.ProductEvents();

            await ProductEventParser.BuildProductEventAsync(eventsElement, seed.PackagingLevel, parseContext);
            await ProductEventParser.BuildProductEventAsync(eventsElement, seed.PackagingLevel, parseContext);

            Assert.AreEqual(2, await database.DbContext.Set<ProductEvent>().CountAsync());
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that bulk event creation persists all valid product events.
        /// </summary>
        /// <returns>A task representing the asynchronous test.</returns>
        /// <seealso cref="ProductEventParser.BulkCreateProductEventsAsync"/>
        [TestMethod]
        public async Task BulkCreateProductEventsAsync_MultipleEvents_CreatesExpectedRecords()
        {
            #region implementation
            using var database = ParsingServiceTestHelper.CreateDatabase();
            var parseContext = database.CreateParseContext();
            var seed = await database.SeedCommonContextAsync(parseContext);

            var count = await ProductEventParser.BulkCreateProductEventsAsync(
                ParsingServiceTestHelper.ProductEvents(),
                seed.PackagingLevel.PackagingLevelID!.Value,
                parseContext);

            Assert.AreEqual(2, count);
            Assert.AreEqual(2, await database.DbContext.Set<ProductEvent>().CountAsync());
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that null bulk inputs return zero.
        /// </summary>
        /// <returns>A task representing the asynchronous test.</returns>
        /// <seealso cref="ProductEventParser.BulkCreateProductEventsAsync"/>
        [TestMethod]
        public async Task BulkCreateProductEventsAsync_NullContextOrParent_ReturnsZero()
        {
            #region implementation
            using var database = ParsingServiceTestHelper.CreateDatabase();
            var parseContext = database.CreateParseContext();

            var count = await ProductEventParser.BulkCreateProductEventsAsync(null!, 1, parseContext);

            Assert.AreEqual(0, count);
            #endregion
        }

        #endregion
    }
}
