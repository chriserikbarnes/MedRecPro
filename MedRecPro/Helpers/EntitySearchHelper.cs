using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace MedRecPro.Helpers
{
    /**************************************************************/
    /// <summary>
    /// Provides generic LINQ extension methods for flexible search filtering
    /// across entity types and string properties.
    /// </summary>
    /// <remarks>
    /// Supports single-term partial matching (SQL LIKE) and multi-term exact matching (SQL IN).
    /// All methods are designed for EF Core SQL translation compatibility.
    /// All matching is performed case-insensitively using SQL LOWER() function.
    /// </remarks>
    /// <seealso cref="EF.Functions"/>
    /// <seealso cref="IQueryable{T}"/>
    public static class SearchFilterExtensions
    {
        #region Public Methods
        /**************************************************************/
        /// <summary>
        /// Filters a queryable by applying flexible search term matching to a specified string property.
        /// Single terms use partial matching (LIKE %term%), multiple terms use exact matching (IN clause).
        /// All matching is case-insensitive.
        /// </summary>
        /// <typeparam name="T">The entity type being queried.</typeparam>
        /// <param name="query">The source queryable to filter.</param>
        /// <param name="propertySelector">Expression selecting the string property to filter on.</param>
        /// <param name="searchInput">
        /// Search input string. Supports space, comma, or semicolon-delimited terms.
        /// </param>
        /// <param name="multiTermBehavior">
        /// Determines how multiple terms are matched. Defaults to exact matching (IN clause).
        /// </param>
        /// <returns>Filtered queryable based on search terms.</returns>
        /// <example>
        /// <code>
        /// // Single property filter (case-insensitive)
        /// query = query.FilterBySearchTerms(s => s.MarketingCategoryCode, "NDA");
        /// // Matches: "NDA", "nda", "Nda", etc.
        /// 
        /// // Multiple terms with exact matching (default, case-insensitive)
        /// query = query.FilterBySearchTerms(s => s.MarketingCategoryCode, "NDA, ANDA, BLA");
        /// // Matches: "nda", "ANDA", "bla", etc.
        /// 
        /// // Multiple terms with partial matching (case-insensitive)
        /// query = query.FilterBySearchTerms(s => s.ProductName, "aspirin tablet", MultiTermBehavior.PartialMatchAny);
        /// // Matches: "ASPIRIN 100mg", "Tablet Form", etc.
        /// </code>
        /// </example>
        /// <seealso cref="MultiTermBehavior"/>
        /// <seealso cref="ParseSearchTerms"/>
        public static IQueryable<T> FilterBySearchTerms<T>(
            this IQueryable<T> query,
            Expression<Func<T, string?>> propertySelector,
            string? searchInput,
            MultiTermBehavior multiTermBehavior = MultiTermBehavior.ExactMatchAny) where T : class
        {
            #region implementation

            // Early exit if no search input provided
            if (string.IsNullOrWhiteSpace(searchInput))
                return query;

            // Parse and normalize search terms (converted to lowercase for case-insensitive matching)
            var searchTerms = ParseSearchTerms(searchInput);

            if (!searchTerms.Any())
                return query;

            // Single term: always use partial matching (LIKE)
            if (searchTerms.Count == 1)
            {
                return query.Where(buildLikePredicate(propertySelector, searchTerms[0]));
            }

            // Multiple terms: behavior depends on multiTermBehavior parameter
            return multiTermBehavior switch
            {
                MultiTermBehavior.ExactMatchAny => applyExactMatchAny(query, propertySelector, searchTerms),
                MultiTermBehavior.PartialMatchAny => applyPartialMatchAny(query, propertySelector, searchTerms),
                _ => query
            };

            #endregion implementation
        }

        /**************************************************************/
        /// <summary>
        /// Filters a queryable by applying flexible search term matching across multiple string properties.
        /// Properties are combined with OR logic - matches if ANY property matches ANY term.
        /// All matching is case-insensitive.
        /// </summary>
        /// <typeparam name="T">The entity type being queried.</typeparam>
        /// <param name="query">The source queryable to filter.</param>
        /// <param name="searchInput">Search input string. Supports space, comma, or semicolon-delimited terms.</param>
        /// <param name="multiTermBehavior">Determines how multiple terms are matched.</param>
        /// <param name="propertySelectors">One or more expressions selecting string properties to filter on.</param>
        /// <returns>Filtered queryable where ANY property matches the search terms.</returns>
        /// <example>
        /// <code>
        /// // Search across multiple properties (OR logic)
        /// query = query.FilterBySearchTerms("ANDA", MultiTermBehavior.ExactMatchAny,
        ///     x => x.MarketingCategoryCode,
        ///     x => x.MarketingCategoryName);
        /// // Matches if MarketingCategoryCode contains "ANDA" OR MarketingCategoryName contains "ANDA"
        /// 
        /// // With partial matching
        /// query = query.FilterBySearchTerms("aspirin", MultiTermBehavior.PartialMatchAny,
        ///     x => x.ProductName,
        ///     x => x.GenericName,
        ///     x => x.BrandName);
        /// </code>
        /// </example>
        /// <seealso cref="MultiTermBehavior"/>
        public static IQueryable<T> FilterBySearchTerms<T>(
            this IQueryable<T> query,
            string? searchInput,
            MultiTermBehavior multiTermBehavior,
            params Expression<Func<T, string?>>[] propertySelectors) where T : class
        {
            #region implementation

            if (string.IsNullOrWhiteSpace(searchInput) || propertySelectors.Length == 0)
                return query;

            var searchTerms = ParseSearchTerms(searchInput);
            if (!searchTerms.Any())
                return query;

            // Build predicates for each property and combine with OR
            Expression<Func<T, bool>>? combinedPredicate = null;

            foreach (var propertySelector in propertySelectors)
            {
                Expression<Func<T, bool>> propertyPredicate;

                if (searchTerms.Count == 1)
                {
                    // Single term: use LIKE
                    propertyPredicate = buildLikePredicate(propertySelector, searchTerms[0]);
                }
                else
                {
                    // Multiple terms: build predicate based on behavior
                    propertyPredicate = multiTermBehavior switch
                    {
                        MultiTermBehavior.ExactMatchAny => buildExactMatchAnyPredicate(propertySelector, searchTerms),
                        MultiTermBehavior.PartialMatchAny => buildPartialMatchAnyPredicate(propertySelector, searchTerms),
                        _ => throw new ArgumentOutOfRangeException(nameof(multiTermBehavior))
                    };
                }

                combinedPredicate = combinedPredicate == null
                    ? propertyPredicate
                    : combineOrPredicates(combinedPredicate, propertyPredicate);
            }

            return combinedPredicate != null ? query.Where(combinedPredicate) : query;

            #endregion implementation
        }

        /**************************************************************/
        /// <summary>
        /// Filters a queryable by applying flexible search term matching across multiple string properties.
        /// Properties are combined with OR logic - matches if ANY property matches ANY term.
        /// Supports exclusion terms to filter out unwanted matches.
        /// All matching is case-insensitive.
        /// </summary>
        /// <typeparam name="T">The entity type being queried.</typeparam>
        /// <param name="query">The source queryable to filter.</param>
        /// <param name="searchInput">Search input string. Supports space, comma, or semicolon-delimited terms.</param>
        /// <param name="multiTermBehavior">Determines how multiple terms are matched.</param>
        /// <param name="excludeInput">Optional terms to exclude from results. Same delimiter support as searchInput.</param>
        /// <param name="propertySelectors">One or more expressions selecting string properties to filter on.</param>
        /// <returns>Filtered queryable where ANY property matches the search terms, excluding specified terms.</returns>
        /// <example>
        /// <code>
        /// // Search for "NDA" but exclude "ANDA"
        /// query = query.FilterBySearchTerms("NDA", MultiTermBehavior.PartialMatchAny, "ANDA",
        ///     x => x.MarketingCategoryCode,
        ///     x => x.MarketingCategoryName);
        /// </code>
        /// </example>
        public static IQueryable<T> FilterBySearchTerms<T>(
            this IQueryable<T> query,
            string? searchInput,
            MultiTermBehavior multiTermBehavior,
            string? excludeInput,
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
                }
                else
                {
                    propertyPredicate = multiTermBehavior switch
                    {
                        MultiTermBehavior.ExactMatchAny => buildExactMatchAnyPredicate(propertySelector, searchTerms),
                        MultiTermBehavior.PartialMatchAny => buildPartialMatchAnyPredicate(propertySelector, searchTerms),
                        _ => throw new ArgumentOutOfRangeException(nameof(multiTermBehavior))
                    };
                }

                combinedPredicate = combinedPredicate == null
                    ? propertyPredicate
                    : combineOrPredicates(combinedPredicate, propertyPredicate);
            }

            if (combinedPredicate == null)
                return query;

            // Apply inclusion filter
            query = query.Where(combinedPredicate);

            // Apply exclusion filters if any
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
        /// All terms are converted to lowercase for case-insensitive matching.
        /// </summary>
        /// <param name="searchInput">Raw search input string.</param>
        /// <param name="delimiters">
        /// Optional custom delimiters. Defaults to space, comma, and semicolon.
        /// </param>
        /// <returns>List of trimmed, lowercase, non-empty search terms.</returns>
        /// <remarks>
        /// Terms are normalized to lowercase using <see cref="string.ToLowerInvariant"/>
        /// to ensure consistent case-insensitive matching across all database operations.
        /// </remarks>
        /// <example>
        /// <code>
        /// var terms = SearchFilterExtensions.ParseSearchTerms("NDA, ANDA; BLA");
        /// // Returns: ["nda", "anda", "bla"]
        /// 
        /// var mixedCase = SearchFilterExtensions.ParseSearchTerms("AnDa, bLa");
        /// // Returns: ["anda", "bla"]
        /// </code>
        /// </example>
        /// <seealso cref="string.ToLowerInvariant"/>
        public static List<string> ParseSearchTerms(
            string? searchInput,
            char[]? delimiters = null)
        {
            #region implementation

            if (string.IsNullOrWhiteSpace(searchInput))
                return new List<string>();

            // Use default delimiters if none provided
            delimiters ??= new[] { ' ', ',', ';' };

            return searchInput
                .Split(delimiters, StringSplitOptions.RemoveEmptyEntries)
                .Select(t => t.Trim().ToLowerInvariant()) // Normalize to lowercase for case-insensitive matching
                .Where(t => t.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            #endregion implementation
        }
        #endregion Public Methods

        #region Private Methods

        /**************************************************************/
        /// <summary>
        /// Builds a NOT LIKE predicate expression for excluding matches.
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

            // Negate the LIKE: NOT LIKE '%term%'
            var notLike = Expression.Not(likeCall);

            // NULL values should pass through (not be excluded)
            var nullCheck = Expression.Equal(propertyAccess, Expression.Constant(null, typeof(string)));
            var combinedExpression = Expression.OrElse(nullCheck, notLike);

            return Expression.Lambda<Func<T, bool>>(combinedExpression, parameter);

            #endregion implementation
        }

        /**************************************************************/
        /// <summary>
        /// Builds an exact match predicate for multiple terms (IN clause equivalent).
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

            // Add null check
            var nullCheck = Expression.NotEqual(propertyAccess, Expression.Constant(null, typeof(string)));
            var combined = Expression.AndAlso(nullCheck, containsCall);

            return Expression.Lambda<Func<T, bool>>(combined, parameter);

            #endregion implementation
        }

        /**************************************************************/
        /// <summary>
        /// Builds a partial match predicate for multiple terms (multiple LIKE with OR).
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
        /// Builds a LIKE predicate expression for case-insensitive partial matching on a string property.
        /// </summary>
        /// <typeparam name="T">The entity type.</typeparam>
        /// <param name="propertySelector">Expression selecting the property to match against.</param>
        /// <param name="searchTerm">The search term to match (should be lowercase, will be wrapped with %).</param>
        /// <returns>Expression suitable for use in a Where clause.</returns>
        /// <remarks>
        /// Generates SQL equivalent to: WHERE LOWER(PropertyName) LIKE '%searchterm%'
        /// The property value is converted to lowercase using SQL LOWER() function
        /// to ensure case-insensitive matching regardless of database collation.
        /// </remarks>
        /// <seealso cref="DbFunctionsExtensions.Like(DbFunctions, string, string)"/>
        /// <seealso cref="string.ToLower"/>
        private static Expression<Func<T, bool>> buildLikePredicate<T>(
            Expression<Func<T, string?>> propertySelector,
            string searchTerm)
        {
            #region implementation

            // Get the parameter from the property selector (e.g., 's' from s => s.PropertyName)
            var parameter = propertySelector.Parameters[0];

            // Get the property access expression (e.g., s.PropertyName)
            var propertyAccess = propertySelector.Body;

            // Build: s.PropertyName.ToLower() for case-insensitive comparison
            var toLowerMethod = typeof(string).GetMethod(
                nameof(string.ToLower),
                Type.EmptyTypes)!;
            var propertyToLower = Expression.Call(propertyAccess, toLowerMethod);

            // Build: EF.Functions.Like(s.PropertyName.ToLower(), "%term%")
            var likeMethod = typeof(DbFunctionsExtensions).GetMethod(
                nameof(DbFunctionsExtensions.Like),
                new[] { typeof(DbFunctions), typeof(string), typeof(string) })!;

            // Pattern is already lowercase from ParseSearchTerms
            var pattern = Expression.Constant($"%{searchTerm}%");
            var efFunctions = Expression.Constant(EF.Functions);

            var likeCall = Expression.Call(
                likeMethod,
                efFunctions,
                propertyToLower, // Use lowercase property value
                pattern);

            // Build null check: s.PropertyName != null
            var nullCheck = Expression.NotEqual(
                propertyAccess,
                Expression.Constant(null, typeof(string)));

            // Combine: s.PropertyName != null && EF.Functions.Like(s.PropertyName.ToLower(), "%term%")
            var combinedExpression = Expression.AndAlso(nullCheck, likeCall);

            return Expression.Lambda<Func<T, bool>>(combinedExpression, parameter);

            #endregion implementation
        }

        /**************************************************************/
        /// <summary>
        /// Applies case-insensitive exact match filtering for any of the provided terms (SQL IN clause).
        /// </summary>
        /// <typeparam name="T">The entity type.</typeparam>
        /// <param name="query">The source queryable.</param>
        /// <param name="propertySelector">Expression selecting the property to match against.</param>
        /// <param name="searchTerms">List of lowercase terms to match exactly.</param>
        /// <returns>Filtered queryable where property equals any of the terms (case-insensitive).</returns>
        /// <remarks>
        /// Generates SQL equivalent to: WHERE LOWER(PropertyName) IN ('term1', 'term2', ...)
        /// The property value is converted to lowercase using SQL LOWER() function
        /// and compared against lowercase search terms for case-insensitive matching.
        /// </remarks>
        /// <seealso cref="List{T}.Contains(T)"/>
        private static IQueryable<T> applyExactMatchAny<T>(
            IQueryable<T> query,
            Expression<Func<T, string?>> propertySelector,
            List<string> searchTerms) where T : class
        {
            #region implementation

            var parameter = propertySelector.Parameters[0];
            var propertyAccess = propertySelector.Body;

            // Build: s.PropertyName.ToLower() for case-insensitive comparison
            var toLowerMethod = typeof(string).GetMethod(
                nameof(string.ToLower),
                Type.EmptyTypes)!;
            var propertyToLower = Expression.Call(propertyAccess, toLowerMethod);

            // Build: searchTerms.Contains(s.PropertyName.ToLower())
            // Note: searchTerms are already lowercase from ParseSearchTerms
            var containsMethod = typeof(List<string>).GetMethod(
                nameof(List<string>.Contains),
                new[] { typeof(string) })!;

            var termsConstant = Expression.Constant(searchTerms);
            var containsCall = Expression.Call(termsConstant, containsMethod, propertyToLower);

            var predicate = Expression.Lambda<Func<T, bool>>(containsCall, parameter);

            return query.Where(predicate);

            #endregion implementation
        }

        /**************************************************************/
        /// <summary>
        /// Applies case-insensitive partial match filtering for any of the provided terms (multiple LIKE with OR).
        /// </summary>
        /// <typeparam name="T">The entity type.</typeparam>
        /// <param name="query">The source queryable.</param>
        /// <param name="propertySelector">Expression selecting the property to match against.</param>
        /// <param name="searchTerms">List of lowercase terms to partially match.</param>
        /// <returns>Filtered queryable where property contains any of the terms (case-insensitive).</returns>
        /// <remarks>
        /// Generates SQL equivalent to: WHERE LOWER(PropertyName) LIKE '%term1%' OR LOWER(PropertyName) LIKE '%term2%' ...
        /// </remarks>
        /// <seealso cref="buildLikePredicate{T}"/>
        private static IQueryable<T> applyPartialMatchAny<T>(
            IQueryable<T> query,
            Expression<Func<T, string?>> propertySelector,
            List<string> searchTerms) where T : class
        {
            #region implementation

            // Start with the first term's predicate
            var combinedPredicate = buildLikePredicate<T>(propertySelector, searchTerms[0]);

            // OR together predicates for remaining terms
            foreach (var term in searchTerms.Skip(1))
            {
                var termPredicate = buildLikePredicate<T>(propertySelector, term);
                combinedPredicate = combineOrPredicates(combinedPredicate, termPredicate);
            }

            return query.Where(combinedPredicate);

            #endregion implementation
        }

        /**************************************************************/
        /// <summary>
        /// Combines two predicate expressions using logical OR.
        /// </summary>
        /// <typeparam name="T">The entity type.</typeparam>
        /// <param name="left">First predicate expression.</param>
        /// <param name="right">Second predicate expression.</param>
        /// <returns>Combined predicate expression (left OR right).</returns>
        /// <remarks>
        /// Uses parameter replacement to ensure both expressions share the same parameter,
        /// which is required for proper EF Core SQL translation.
        /// </remarks>
        /// <seealso cref="ParameterReplacer"/>
        /// <seealso cref="Expression.OrElse"/>
        private static Expression<Func<T, bool>> combineOrPredicates<T>(
            Expression<Func<T, bool>> left,
            Expression<Func<T, bool>> right)
        {
            #region implementation

            // Use the parameter from the left expression
            var parameter = left.Parameters[0];

            // Replace the parameter in the right expression to match the left
            var rightBody = new ParameterReplacer(right.Parameters[0], parameter)
                .Visit(right.Body);

            // Combine with OR
            var orExpression = Expression.OrElse(left.Body, rightBody);

            return Expression.Lambda<Func<T, bool>>(orExpression, parameter);

            #endregion implementation
        }

        #endregion Private Methods

        #region Nested Types

        /**************************************************************/
        /// <summary>
        /// Expression visitor that replaces one parameter with another.
        /// Required for combining expressions that use different parameter instances.
        /// </summary>
        /// <seealso cref="ExpressionVisitor"/>
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
    /// Specifies how multiple search terms should be matched against a property.
    /// </summary>
    /// <remarks>
    /// Single terms always use partial matching regardless of this setting.
    /// All matching is performed case-insensitively.
    /// </remarks>
    public enum MultiTermBehavior
    {
        /// <summary>
        /// Multiple terms are matched exactly using SQL IN clause (case-insensitive).
        /// Example: "NDA ANDA" matches records where property equals "NDA", "nda", "ANDA", "anda", etc.
        /// </summary>
        ExactMatchAny,

        /// <summary>
        /// Multiple terms are matched partially using SQL LIKE with OR (case-insensitive).
        /// Example: "asp tab" matches records where property contains "asp", "ASP", "tab", "TAB", etc.
        /// </summary>
        PartialMatchAny
    }
}