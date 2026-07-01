using WebSplFileImportResult = MedRecPro.Models.SplFileImportResult;
using WebSplZipImportResult = MedRecPro.Models.SplZipImportResult;
using ImportSplFileImportResult = MedRecProImportClass.Models.SplFileImportResult;
using ImportSplZipImportResult = MedRecProImportClass.Models.SplZipImportResult;

namespace MedRecPro.Mappers
{
    /**************************************************************/
    /// <summary>
    /// Maps SPL import result DTOs from the import library into the web API result DTOs.
    /// </summary>
    /// <remarks>
    /// Keeps the import-library boundary explicit while preserving the existing web API
    /// progress contract and computed result properties.
    /// </remarks>
    /// <seealso cref="WebSplZipImportResult"/>
    /// <seealso cref="ImportSplZipImportResult"/>
    public static class ImportResultMapper
    {
        #region implementation

        /**************************************************************/
        /// <summary>
        /// Converts import-library ZIP import results into web API ZIP import results.
        /// </summary>
        /// <param name="sourceResults">Import-library result DTOs returned by the SPL import runtime.</param>
        /// <returns>Web API result DTOs suitable for import progress responses.</returns>
        /// <remarks>
        /// Null source collections map to an empty list. Computed target properties are not
        /// assigned directly; they remain derived from the mapped file result collection.
        /// </remarks>
        /// <example>
        /// <code>
        /// status.Results = ImportResultMapper.ToWebResults(importResults);
        /// </code>
        /// </example>
        /// <seealso cref="WebSplZipImportResult"/>
        /// <seealso cref="ImportSplZipImportResult"/>
        public static List<WebSplZipImportResult> ToWebResults(IEnumerable<ImportSplZipImportResult>? sourceResults)
        {
            #region implementation
            if (sourceResults == null)
            {
                return new List<WebSplZipImportResult>();
            }

            return sourceResults.Select(toWebZipResult).ToList();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Converts one import-library ZIP import result into the web API shape.
        /// </summary>
        /// <param name="sourceResult">Import-library ZIP result to convert.</param>
        /// <returns>A web API ZIP import result with mapped file results.</returns>
        /// <remarks>
        /// Only stored fields are copied. Web computed properties recalculate from the
        /// mapped file result list.
        /// </remarks>
        /// <seealso cref="WebSplZipImportResult"/>
        /// <seealso cref="ImportSplZipImportResult"/>
        private static WebSplZipImportResult toWebZipResult(ImportSplZipImportResult sourceResult)
        {
            #region implementation
            return new WebSplZipImportResult
            {
                ZipFileName = sourceResult.ZipFileName,
                FileResults = sourceResult.FileResults?
                    .Select(toWebFileResult)
                    .ToList() ?? new List<WebSplFileImportResult>()
            };
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Converts one import-library file import result into the web API shape.
        /// </summary>
        /// <param name="sourceResult">Import-library file result to convert.</param>
        /// <returns>A web API file import result with copied counters and errors.</returns>
        /// <remarks>
        /// Errors are copied into a new list so later mutation on one side of the boundary
        /// does not change the other side.
        /// </remarks>
        /// <seealso cref="WebSplFileImportResult"/>
        /// <seealso cref="ImportSplFileImportResult"/>
        private static WebSplFileImportResult toWebFileResult(ImportSplFileImportResult sourceResult)
        {
            #region implementation
            return new WebSplFileImportResult
            {
                FileName = sourceResult.FileName,
                Success = sourceResult.Success,
                Message = sourceResult.Message,
                DocumentsCreated = sourceResult.DocumentsCreated,
                OrganizationsCreated = sourceResult.OrganizationsCreated,
                ProductsCreated = sourceResult.ProductsCreated,
                SectionsCreated = sourceResult.SectionsCreated,
                IngredientsCreated = sourceResult.IngredientsCreated,
                ProductElementsCreated = sourceResult.ProductElementsCreated,
                Errors = sourceResult.Errors?.ToList() ?? new List<string>()
            };
            #endregion
        }

        #endregion
    }
}
