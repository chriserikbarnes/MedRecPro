using MedRecPro.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MedRecPro.Service.Test
{
    /**************************************************************/
    /// <summary>
    /// Tests public entity search helper options and query-building methods.
    /// </summary>
    /// <remarks>
    /// Query tests assert expression construction rather than executing EF.Functions
    /// against LINQ-to-Objects, which would test the framework instead of the helper.
    /// </remarks>
    /// <seealso cref="PhoneticMatchOptions"/>
    /// <seealso cref="SearchFilterExtensions"/>
    [TestClass]
    public class EntitySearchHelperTests
    {
        #region implementation

        /**************************************************************/
        /// <summary>
        /// Verifies phonetic matching options clamp custom scores into the SQL range.
        /// </summary>
        /// <seealso cref="PhoneticMatchOptions.WithScore"/>
        [TestMethod]
        public void WithScore_OutOfRangeValues_ClampsToDifferenceScoreRange()
        {
            #region implementation
            var low = PhoneticMatchOptions.WithScore(-10);
            var high = PhoneticMatchOptions.WithScore(99);

            Assert.IsTrue(low.Enabled);
            Assert.AreEqual(0, low.MinimumScore);
            Assert.IsTrue(high.Enabled);
            Assert.AreEqual(4, high.MinimumScore);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies search-term parsing trims, lowercases, de-duplicates, and honors custom delimiters.
        /// </summary>
        /// <seealso cref="SearchFilterExtensions.ParseSearchTerms"/>
        [TestMethod]
        public void ParseSearchTerms_DelimitedInput_ReturnsDistinctNormalizedTerms()
        {
            #region implementation
            var defaultTerms = SearchFilterExtensions.ParseSearchTerms(" Alpha, beta;ALPHA  ");
            var customTerms = SearchFilterExtensions.ParseSearchTerms("alpha|beta||gamma", new[] { '|' });

            CollectionAssert.AreEqual(new List<string> { "alpha", "beta" }, defaultTerms);
            CollectionAssert.AreEqual(new List<string> { "alpha", "beta", "gamma" }, customTerms);
            Assert.AreEqual(0, SearchFilterExtensions.ParseSearchTerms(" ").Count);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies property-selector filtering returns the original query for blank search text.
        /// </summary>
        /// <seealso cref="SearchFilterExtensions.FilterBySearchTerms{T}(IQueryable{T}, System.Linq.Expressions.Expression{Func{T, string?}}, string?, MultiTermBehavior)"/>
        [TestMethod]
        public void FilterBySearchTerms_BlankSearch_ReturnsOriginalQuery()
        {
            #region implementation
            var query = sampleEntities().AsQueryable();

            var result = query.FilterBySearchTerms(entity => entity.Name, " ");

            Assert.AreSame(query, result);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies property-selector filtering adds a LIKE predicate for single terms.
        /// </summary>
        /// <seealso cref="SearchFilterExtensions.FilterBySearchTerms{T}(IQueryable{T}, System.Linq.Expressions.Expression{Func{T, string?}}, string?, MultiTermBehavior)"/>
        [TestMethod]
        public void FilterBySearchTerms_SinglePropertySearch_BuildsLikePredicate()
        {
            #region implementation
            var query = sampleEntities().AsQueryable();

            var result = query.FilterBySearchTerms(entity => entity.Name, "Alpha");
            var expressionText = result.Expression.ToString();

            StringAssert.Contains(expressionText, "Like");
            StringAssert.Contains(expressionText, "Name");
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies multi-property filtering can include exclusions and phonetic predicates.
        /// </summary>
        /// <seealso cref="SearchFilterExtensions.FilterBySearchTerms{T}(IQueryable{T}, string?, MultiTermBehavior, string?, System.Linq.Expressions.Expression{Func{T, string?}}[])"/>
        /// <seealso cref="SearchFilterExtensions.FilterBySearchTerms{T}(IQueryable{T}, string?, MultiTermBehavior, string?, PhoneticMatchOptions, System.Linq.Expressions.Expression{Func{T, string?}}[])"/>
        [TestMethod]
        public void FilterBySearchTerms_MultiplePropertiesAndPhoneticOptions_BuildsExpectedPredicates()
        {
            #region implementation
            var query = sampleEntities().AsQueryable();

            var excluded = query.FilterBySearchTerms(
                "Alpha Beta",
                MultiTermBehavior.PartialMatchAny,
                "retired",
                entity => entity.Name,
                entity => entity.Alias);
            var phonetic = query.FilterBySearchTerms(
                "asprin",
                MultiTermBehavior.PartialMatchAny,
                null,
                PhoneticMatchOptions.Standard,
                entity => entity.Name);

            StringAssert.Contains(excluded.Expression.ToString(), "Not");
            StringAssert.Contains(excluded.Expression.ToString(), "Alias");
            StringAssert.Contains(phonetic.Expression.ToString(), "Difference");
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates sample entities for query-expression tests.
        /// </summary>
        /// <returns>Sample entities.</returns>
        /// <seealso cref="SampleSearchEntity"/>
        private static List<SampleSearchEntity> sampleEntities()
        {
            #region implementation
            return new List<SampleSearchEntity>
            {
                new() { Name = "Alpha", Alias = "A" },
                new() { Name = "Beta", Alias = "B" }
            };
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Simple searchable entity for expression tests.
        /// </summary>
        private sealed class SampleSearchEntity
        {
            /**************************************************************/
            /// <summary>
            /// Gets or sets the sample name.
            /// </summary>
            public string? Name { get; set; }

            /**************************************************************/
            /// <summary>
            /// Gets or sets the sample alias.
            /// </summary>
            public string? Alias { get; set; }
        }

        #endregion
    }
}
