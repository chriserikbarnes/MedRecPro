using static MedRecPro.Models.Label;

namespace MedRecPro.Models
{
    /**************************************************************/
    /// <summary>
    /// Provides summary information for the import process of a single SPL file.
    /// </summary>
    /// <remarks>
    /// Contains detailed metrics about entities created during the import process including
    /// documents, organizations, products, sections, ingredients, and product elements.
    /// Tracks both success status and any errors encountered during processing.
    /// Used to provide comprehensive feedback about individual file processing results.
    /// </remarks>
    /// <seealso cref="SplZipImportResult"/>
    /// <seealso cref="Label"/>
    public class SplFileImportResult
    {
        #region implementation

        /**************************************************************/
        /// <summary>
        /// Gets or sets the name of the SPL file that was processed.
        /// </summary>
        /// <seealso cref="Label"/>
        public string? FileName { get; set; }

        /**************************************************************/
        /// <summary>
        /// Gets the GUID for the label processed.
        /// </summary>
        /// <seealso cref="Label"/>
        public Guid SplGUID => Guid.TryParse(FileName?.Replace(".xml", string.Empty), out var guid)
            ? guid : Guid.Empty;

        /**************************************************************/
        /// <summary>
        /// Gets or sets a value indicating whether the file import was successful.
        /// </summary>
        /// <seealso cref="Label"/>
        public bool Success { get; set; }

        /**************************************************************/
        /// <summary>
        /// Gets or sets a summary message about the import operation result.
        /// </summary>
        /// <seealso cref="Label"/>
        public string? Message { get; set; }

        /**************************************************************/
        /// <summary>
        /// Gets or sets the number of Document entities created during import.
        /// </summary>
        /// <seealso cref="Label"/>
        public int DocumentsCreated { get; set; }

        /**************************************************************/
        /// <summary>
        /// Gets or sets the number of Organization entities created during import.
        /// </summary>
        /// <seealso cref="Label"/>
        public int OrganizationsCreated { get; set; }

        /**************************************************************/
        /// <summary>
        /// Gets or sets the number of Product entities created during import.
        /// </summary>
        /// <seealso cref="Label"/>
        public int ProductsCreated { get; set; }

        /**************************************************************/
        /// <summary>
        /// Gets or sets the number of Section entities created during import.
        /// </summary>
        /// <seealso cref="Label"/>
        public int SectionsCreated { get; set; }

        /**************************************************************/
        /// <summary>
        /// Gets or sets the number of Ingredient entities created during import.
        /// </summary>
        /// <seealso cref="Label"/>
        public int IngredientsCreated { get; set; }

        /**************************************************************/
        /// <summary>
        /// Gets or sets the number of ProductElement entities created during import.
        /// </summary>
        /// <seealso cref="Label"/>
        public int ProductElementsCreated { get; set; }

        /**************************************************************/
        /// <summary>
        /// Gets or sets the collection of error messages encountered during import.
        /// </summary>
        /// <remarks>
        /// Contains detailed error messages for troubleshooting failed imports.
        /// Even successful imports may contain non-fatal errors or warnings.
        /// </remarks>
        /// <seealso cref="Label"/>
        public List<string> Errors { get; set; } = new List<string>();

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Represents the result of importing a ZIP file containing multiple SPL files.
    /// </summary>
    /// <remarks>
    /// Aggregates results from multiple individual SPL file imports within a single ZIP archive.
    /// Provides summary statistics and overall success indicators for the entire ZIP processing operation.
    /// Supports detailed analysis of import results including per-file success rates and overall metrics.
    /// </remarks>
    /// <seealso cref="SplFileImportResult"/>
    /// <seealso cref="Label"/>
    public class SplZipImportResult
    {
        #region implementation

        /**************************************************************/
        /// <summary>
        /// Gets or sets the name of the ZIP file that was processed.
        /// </summary>
        /// <seealso cref="Label"/>
        public string? ZipFileName { get; set; }

        /**************************************************************/
        /// <summary>
        /// Gets or sets the collection of import results for individual SPL files within the ZIP.
        /// </summary>
        /// <seealso cref="SplFileImportResult"/>
        /// <seealso cref="Label"/>
        public List<SplFileImportResult> FileResults { get; set; } = new List<SplFileImportResult>();

        /**************************************************************/
        /// <summary>
        /// Gets a value indicating whether all files in the ZIP were imported successfully.
        /// </summary>
        /// <remarks>
        /// Returns true only if every individual SPL file in the ZIP was processed without errors.
        /// A single failed file will cause this property to return false for the entire ZIP.
        /// </remarks>
        /// <seealso cref="SplFileImportResult"/>
        /// <seealso cref="Label"/>
        public bool OverallSuccess => FileResults.All(f => f.Success);

        /**************************************************************/
        /// <summary>
        /// Gets the total number of SPL files processed from the ZIP archive.
        /// </summary>
        /// <remarks>
        /// Includes both successful and failed file processing attempts.
        /// Represents the total count of SPL files found and processed within the ZIP.
        /// </remarks>
        /// <seealso cref="SplFileImportResult"/>
        /// <seealso cref="Label"/>
        public int TotalFilesProcessed => FileResults.Count;

        /**************************************************************/
        /// <summary>
        /// Gets the number of SPL files that were successfully imported from the ZIP.
        /// </summary>
        /// <remarks>
        /// Counts only files that completed processing without errors.
        /// Can be compared with TotalFilesProcessed to determine success rate.
        /// </remarks>
        /// <seealso cref="SplFileImportResult"/>
        /// <seealso cref="Label"/>
        public int TotalFilesSucceeded => FileResults.Count(f => f.Success);

        #endregion
    }
}