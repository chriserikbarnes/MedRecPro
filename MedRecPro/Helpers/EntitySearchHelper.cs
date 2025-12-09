using MedRecPro.Data;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace MedRecPro.Helpers
{
    /**************************************************************/
    /// <summary>
    /// Configuration options for phonetic (Soundex-based) matching.
    /// </summary>
    /// <remarks>
    /// SQL Server's DIFFERENCE function returns a score from 0-4:
    /// 4 = Identical or nearly identical sounds
    /// 3 = Strong similarity (recommended for drug names)
    /// 2 = Moderate similarity
    /// 1 = Weak similarity  
    /// 0 = No similarity
    /// </remarks>
    public class PhoneticMatchOptions
    {
        /// <summary>
        /// Whether phonetic matching is enabled.
        /// </summary>
        public bool Enabled { get; set; }

        /// <summary>
        /// Minimum DIFFERENCE score required for a match (0-4).
        /// Default is 3 (strong similarity), recommended for pharmaceutical names.
        /// </summary>
        /// <remarks>
        /// Guidelines:
        /// - 4: Very strict, only near-exact phonetic matches
        /// - 3: Recommended for drug names (catches common misspellings)
        /// - 2: Loose matching, may produce false positives
        /// - 1-0: Not recommended (too many false positives)
        /// </remarks>
        public int MinimumScore { get; set; } = 3;

        /// <summary>
        /// Disabled phonetic matching (default).
        /// </summary>
        public static PhoneticMatchOptions None => new() { Enabled = false };

        /// <summary>
        /// Standard phonetic matching with threshold of 3.
        /// Recommended for pharmaceutical/drug name searches.
        /// </summary>
        public static PhoneticMatchOptions Standard => new() { Enabled = true, MinimumScore = 3 };

        /// <summary>
        /// Strict phonetic matching with threshold of 4.
        /// Only matches nearly identical phonetic sounds.
        /// </summary>
        public static PhoneticMatchOptions Strict => new() { Enabled = true, MinimumScore = 4 };

        /// <summary>
        /// Loose phonetic matching with threshold of 2.
        /// Use with caution - may produce false positives.
        /// </summary>
        public static PhoneticMatchOptions Loose => new() { Enabled = true, MinimumScore = 2 };

        /// <summary>
        /// Creates custom phonetic matching options.
        /// </summary>
        /// <param name="minimumScore">Minimum DIFFERENCE score (0-4).</param>
        /// <returns>Configured PhoneticMatchOptions.</returns>
        public static PhoneticMatchOptions WithScore(int minimumScore)
            => new() { Enabled = true, MinimumScore = Math.Clamp(minimumScore, 0, 4) };
    }

    /**************************************************************/
    /// <summary>
    /// Provides generic LINQ extension methods for flexible search filtering
    /// across entity types and string properties.
    /// </summary>
    /// <remarks>
    /// Supports single-term partial matching (SQL LIKE) and multi-term exact matching (SQL IN).
    /// All methods are designed for EF Core SQL translation compatibility.
    /// All matching is performed case-insensitively using SQL LOWER() function.
    /// Optional phonetic matching uses SQL Server's SOUNDEX/DIFFERENCE functions.
    /// </remarks>
    public static class SearchFilterExtensions
    {
        #region Public Methods

        /**************************************************************/
        /// <summary>
        /// Filters a queryable by applying flexible search term matching to a specified string property.
        /// Single terms use partial matching (LIKE %term%), multiple terms use exact matching (IN clause).
        /// All matching is case-insensitive.
        /// </summary>
        public static IQueryable<T> FilterBySearchTerms<T>(
            this IQueryable<T> query,
            Expression<Func<T, string?>> propertySelector,
            string? searchInput,
            MultiTermBehavior multiTermBehavior = MultiTermBehavior.ExactMatchAny) where T : class
        {
            #region implementation

            if (string.IsNullOrWhiteSpace(searchInput))
                return query;

            var searchTerms = ParseSearchTerms(searchInput);

            if (!searchTerms.Any())
                return query;

            if (searchTerms.Count == 1)
            {
                return query.Where(buildLikePredicate(propertySelector, searchTerms[0]));
            }

            return multiTermBehavior switch
            {
                MultiTermBehavior.ExactMatchAny => query.Where(buildExactMatchAnyPredicate(propertySelector, searchTerms)),
                MultiTermBehavior.PartialMatchAny => query.Where(buildPartialMatchAnyPredicate(propertySelector, searchTerms)),
                _ => query
            };

            #endregion implementation
        }

        /**************************************************************/
        /// <summary>
        /// Filters a queryable by applying flexible search term matching across multiple string properties.
        /// Properties are combined with OR logic. Supports exclusion terms.
        /// </summary>
        public static IQueryable<T> FilterBySearchTerms<T>(
            this IQueryable<T> query,
            string? searchInput,
            MultiTermBehavior multiTermBehavior,
            string? excludeInput,
            params Expression<Func<T, string?>>[] propertySelectors) where T : class
        {
            return FilterBySearchTerms(query, searchInput, multiTermBehavior, excludeInput, PhoneticMatchOptions.None, propertySelectors);
        }

        /**************************************************************/
        /// <summary>
        /// Filters a queryable by applying flexible search term matching across multiple string properties.
        /// Properties are combined with OR logic - matches if ANY property matches ANY term.
        /// Supports exclusion terms and optional phonetic (Soundex) matching for misspellings.
        /// All matching is case-insensitive.
        /// </summary>
        /// <typeparam name="T">The entity type being queried.</typeparam>
        /// <param name="query">The source queryable to filter.</param>
        /// <param name="searchInput">Search input string. Supports space, comma, or semicolon-delimited terms.</param>
        /// <param name="multiTermBehavior">Determines how multiple terms are matched.</param>
        /// <param name="excludeInput">Optional terms to exclude from results.</param>
        /// <param name="phoneticOptions">Phonetic matching configuration. Use PhoneticMatchOptions.Standard for drug names.</param>
        /// <param name="propertySelectors">One or more expressions selecting string properties to filter on.</param>
        /// <returns>Filtered queryable where ANY property matches the search terms.</returns>
        /// <example>
        /// <code>
        /// // Search with phonetic matching for misspelled drug names
        /// query = query.FilterBySearchTerms(
        ///     "testostone",
        ///     MultiTermBehavior.PartialMatchAny,
        ///     null,
        ///     PhoneticMatchOptions.Standard,
        ///     x => x.ProductName,
        ///     x => x.GenericName);
        /// // Matches "testosterone", "testostone", etc.
        /// 
        /// // Custom threshold
        /// query = query.FilterBySearchTerms(
        ///     "asprin",
        ///     MultiTermBehavior.PartialMatchAny,
        ///     null,
        ///     PhoneticMatchOptions.WithScore(4),  // Strict matching
        ///     x => x.ProductName);
        /// </code>
        /// </example>
        public static IQueryable<T> FilterBySearchTerms<T>(
            this IQueryable<T> query,
            string? searchInput,
            MultiTermBehavior multiTermBehavior,
            string? excludeInput,
            PhoneticMatchOptions phoneticOptions,
            params Expression<Func<T, string?>>[] propertySelectors) where T : class
        {
            #region implementation

            if (string.IsNullOrWhiteSpace(searchInput) || propertySelectors.Length == 0)
                return query;

            var searchTerms = ParseSearchTerms(searchInput);
            if (!searchTerms.Any())
                return query;

            var excludeTerms = ParseSearchTerms(excludeInput);

            // Build predicates for each property and combine with OR
            Expression<Func<T, bool>>? combinedPredicate = null;

            foreach (var propertySelector in propertySelectors)
            {
                Expression<Func<T, bool>> propertyPredicate;

                if (searchTerms.Count == 1)
                {
                    propertyPredicate = buildLikePredicate(propertySelector, searchTerms[0]);

                    if (phoneticOptions.Enabled)
                    {
                        var phoneticPredicate = buildDifferencePredicate(propertySelector, searchTerms[0], phoneticOptions.MinimumScore);
                        propertyPredicate = combineOrPredicates(propertyPredicate, phoneticPredicate);
                    }
                }
                else
                {
                    propertyPredicate = multiTermBehavior switch
                    {
                        MultiTermBehavior.ExactMatchAny => buildExactMatchAnyPredicate(propertySelector, searchTerms),
                        MultiTermBehavior.PartialMatchAny => buildPartialMatchAnyPredicate(propertySelector, searchTerms),
                        _ => throw new ArgumentOutOfRangeException(nameof(multiTermBehavior))
                    };

                    if (phoneticOptions.Enabled)
                    {
                        foreach (var term in searchTerms)
                        {
                            var phoneticPredicate = buildDifferencePredicate(propertySelector, term, phoneticOptions.MinimumScore);
                            propertyPredicate = combineOrPredicates(propertyPredicate, phoneticPredicate);
                        }
                    }
                }

                combinedPredicate = combinedPredicate == null
                    ? propertyPredicate
                    : combineOrPredicates(combinedPredicate, propertyPredicate);
            }

            if (combinedPredicate == null)
                return query;

            query = query.Where(combinedPredicate);

            // Apply exclusion filters
            if (excludeTerms.Any())
            {
                foreach (var propertySelector in propertySelectors)
                {
                    foreach (var excludeTerm in excludeTerms)
                    {
                        var excludePredicate = buildNotLikePredicate(propertySelector, excludeTerm);
                        query = query.Where(excludePredicate);
                    }
                }
            }

            return query;

            #endregion implementation
        }

        /**************************************************************/
        /// <summary>
        /// Parses a search input string into a list of normalized search terms.
        /// </summary>
        public static List<string> ParseSearchTerms(
            string? searchInput,
            char[]? delimiters = null)
        {
            #region implementation

            if (string.IsNullOrWhiteSpace(searchInput))
                return new List<string>();

            delimiters ??= new[] { ' ', ',', ';' };

            return searchInput
                .Split(delimiters, StringSplitOptions.RemoveEmptyEntries)
                .Select(t => t.Trim().ToLowerInvariant())
                .Where(t => t.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            #endregion implementation
        }

        #endregion Public Methods

        #region Private Methods

        /**************************************************************/
        /// <summary>
        /// Builds a LIKE predicate for partial matching.
        /// </summary>
        private static Expression<Func<T, bool>> buildLikePredicate<T>(
            Expression<Func<T, string?>> propertySelector,
            string searchTerm)
        {
            #region implementation

            var parameter = propertySelector.Parameters[0];
            var propertyAccess = propertySelector.Body;

            var toLowerMethod = typeof(string).GetMethod(nameof(string.ToLower), Type.EmptyTypes)!;
            var propertyToLower = Expression.Call(propertyAccess, toLowerMethod);

            var likeMethod = typeof(DbFunctionsExtensions).GetMethod(
                nameof(DbFunctionsExtensions.Like),
                new[] { typeof(DbFunctions), typeof(string), typeof(string) })!;

            var pattern = Expression.Constant($"%{searchTerm}%");
            var efFunctions = Expression.Constant(EF.Functions);
            var likeCall = Expression.Call(likeMethod, efFunctions, propertyToLower, pattern);

            var nullCheck = Expression.NotEqual(propertyAccess, Expression.Constant(null, typeof(string)));
            var combinedExpression = Expression.AndAlso(nullCheck, likeCall);

            return Expression.Lambda<Func<T, bool>>(combinedExpression, parameter);

            #endregion implementation
        }

        /**************************************************************/
        /// <summary>
        /// Builds a NOT LIKE predicate for exclusion matching.
        /// </summary>
        private static Expression<Func<T, bool>> buildNotLikePredicate<T>(
            Expression<Func<T, string?>> propertySelector,
            string excludeTerm)
        {
            #region implementation

            var parameter = propertySelector.Parameters[0];
            var propertyAccess = propertySelector.Body;

            var toLowerMethod = typeof(string).GetMethod(nameof(string.ToLower), Type.EmptyTypes)!;
            var propertyToLower = Expression.Call(propertyAccess, toLowerMethod);

            var likeMethod = typeof(DbFunctionsExtensions).GetMethod(
                nameof(DbFunctionsExtensions.Like),
                new[] { typeof(DbFunctions), typeof(string), typeof(string) })!;

            var pattern = Expression.Constant($"%{excludeTerm}%");
            var efFunctions = Expression.Constant(EF.Functions);
            var likeCall = Expression.Call(likeMethod, efFunctions, propertyToLower, pattern);

            var notLike = Expression.Not(likeCall);
            var nullCheck = Expression.Equal(propertyAccess, Expression.Constant(null, typeof(string)));
            var combinedExpression = Expression.OrElse(nullCheck, notLike);

            return Expression.Lambda<Func<T, bool>>(combinedExpression, parameter);

            #endregion implementation
        }

        /**************************************************************/
        /// <summary>
        /// Builds a DIFFERENCE predicate for phonetic similarity matching.
        /// Uses SQL Server's DIFFERENCE function which returns 0-4 similarity score.
        /// </summary>
        /// <param name="propertySelector">Property to match against.</param>
        /// <param name="searchTerm">Search term to compare phonetically.</param>
        /// <param name="minimumScore">Minimum DIFFERENCE score (0-4) required for a match.</param>
        /// <returns>Predicate expression for WHERE clause.</returns>
        /// <remarks>
        /// Generates SQL: WHERE PropertyName IS NOT NULL AND DIFFERENCE(PropertyName, 'term') >= minimumScore
        /// </remarks>
        private static Expression<Func<T, bool>> buildDifferencePredicate<T>(
            Expression<Func<T, string?>> propertySelector,
            string searchTerm,
            int minimumScore)
        {
            #region implementation

            var parameter = propertySelector.Parameters[0];
            var propertyAccess = propertySelector.Body;

            // Get DIFFERENCE method from ApplicationDbContext
            var differenceMethod = typeof(ApplicationDbContext).GetMethod(nameof(ApplicationDbContext.Difference))!;

            // Build: ApplicationDbContext.Difference(property, searchTerm)
            var differenceCall = Expression.Call(
                differenceMethod,
                propertyAccess,
                Expression.Constant(searchTerm));

            // Build: DIFFERENCE(property, searchTerm) >= minimumScore
            var scoreThreshold = Expression.Constant(minimumScore);
            var scoreComparison = Expression.GreaterThanOrEqual(differenceCall, scoreThreshold);

            // Add null check: property != null && DIFFERENCE(...) >= threshold
            var nullCheck = Expression.NotEqual(propertyAccess, Expression.Constant(null, typeof(string)));
            var combinedExpression = Expression.AndAlso(nullCheck, scoreComparison);

            return Expression.Lambda<Func<T, bool>>(combinedExpression, parameter);

            #endregion implementation
        }

        /**************************************************************/
        /// <summary>
        /// Builds an exact match predicate for multiple terms (IN clause).
        /// </summary>
        private static Expression<Func<T, bool>> buildExactMatchAnyPredicate<T>(
            Expression<Func<T, string?>> propertySelector,
            List<string> searchTerms)
        {
            #region implementation

            var parameter = propertySelector.Parameters[0];
            var propertyAccess = propertySelector.Body;

            var toLowerMethod = typeof(string).GetMethod(nameof(string.ToLower), Type.EmptyTypes)!;
            var propertyToLower = Expression.Call(propertyAccess, toLowerMethod);

            var containsMethod = typeof(List<string>).GetMethod(nameof(List<string>.Contains), new[] { typeof(string) })!;
            var termsConstant = Expression.Constant(searchTerms);
            var containsCall = Expression.Call(termsConstant, containsMethod, propertyToLower);

            var nullCheck = Expression.NotEqual(propertyAccess, Expression.Constant(null, typeof(string)));
            var combined = Expression.AndAlso(nullCheck, containsCall);

            return Expression.Lambda<Func<T, bool>>(combined, parameter);

            #endregion implementation
        }

        /**************************************************************/
        /// <summary>
        /// Builds a partial match predicate for multiple terms (OR'd LIKE).
        /// </summary>
        private static Expression<Func<T, bool>> buildPartialMatchAnyPredicate<T>(
            Expression<Func<T, string?>> propertySelector,
            List<string> searchTerms)
        {
            #region implementation

            var combinedPredicate = buildLikePredicate<T>(propertySelector, searchTerms[0]);

            foreach (var term in searchTerms.Skip(1))
            {
                var termPredicate = buildLikePredicate<T>(propertySelector, term);
                combinedPredicate = combineOrPredicates(combinedPredicate, termPredicate);
            }

            return combinedPredicate;

            #endregion implementation
        }

        /**************************************************************/
        /// <summary>
        /// Combines two predicates with OR logic.
        /// </summary>
        private static Expression<Func<T, bool>> combineOrPredicates<T>(
            Expression<Func<T, bool>> left,
            Expression<Func<T, bool>> right)
        {
            #region implementation

            var parameter = left.Parameters[0];
            var rightBody = new ParameterReplacer(right.Parameters[0], parameter).Visit(right.Body);
            var orExpression = Expression.OrElse(left.Body, rightBody);

            return Expression.Lambda<Func<T, bool>>(orExpression, parameter);

            #endregion implementation
        }

        #endregion Private Methods

        #region Nested Types

        /**************************************************************/
        /// <summary>
        /// Expression visitor for parameter replacement.
        /// </summary>
        private class ParameterReplacer : ExpressionVisitor
        {
            private readonly ParameterExpression _oldParameter;
            private readonly ParameterExpression _newParameter;

            public ParameterReplacer(ParameterExpression oldParameter, ParameterExpression newParameter)
            {
                _oldParameter = oldParameter;
                _newParameter = newParameter;
            }

            protected override Expression VisitParameter(ParameterExpression node)
            {
                return node == _oldParameter ? _newParameter : base.VisitParameter(node);
            }
        }

        #endregion Nested Types
    }

    /**************************************************************/
    /// <summary>
    /// Specifies how multiple search terms should be matched.
    /// </summary>
    public enum MultiTermBehavior
    {
        /// <summary>
        /// Exact match using IN clause (case-insensitive).
        /// </summary>
        ExactMatchAny,

        /// <summary>
        /// Partial match using LIKE with OR (case-insensitive).
        /// </summary>
        PartialMatchAny
    }
}