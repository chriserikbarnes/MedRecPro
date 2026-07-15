using MedRecPro.Service.Common;
using MedRecPro.Service.LabelQuery.Common;

namespace MedRecPro.Service.LabelQuery.Implementation
{
    /**************************************************************/
    /// <summary>
    /// Creates short-lived label query implementations for the legacy static compatibility boundary.
    /// </summary>
    /// <remarks>
    /// This factory intentionally has no cached state and never captures an <c>ApplicationDbContext</c>.
    /// Runtime request paths resolve the same implementation through dependency injection.
    /// </remarks>
    /// <seealso cref="LabelQueryDataAccess"/>
    internal static class LabelQueryLegacyCompatibility
    {
        /**************************************************************/
        /// <summary>
        /// Creates one stateless implementation instance for a legacy forwarding invocation.
        /// </summary>
        /// <returns>A new label query implementation.</returns>
        /// <seealso cref="LabelQueryDataAccess"/>
        internal static LabelQueryDataAccess Create()
        {
            #region implementation

            var cachePolicy = new QueryCachePolicy(new PerformanceAppCache());
            return new LabelQueryDataAccess(cachePolicy, new LegacyDtoLabelCacheKeyBuilder());

            #endregion
        }
    }
}
