namespace MedRecProConsole.Models
{
    /**************************************************************/
    /// <summary>
    /// Tracks the results of the import operation for final reporting.
    /// Aggregates statistics from all processed ZIP files including success/failure counts
    /// and entity creation metrics.
    /// </summary>
    /// <remarks>
    /// This model is populated during import execution and used by ConsoleHelper
    /// to display the final results summary.
    /// </remarks>
    /// <seealso cref="ImportParameters"/>
    /// <seealso cref="Services.ImportService"/>
    /// <seealso cref="Helpers.ConsoleHelper"/>
    public class ImportResults
    {
        #region implementation

        /**************************************************************/
        /// <summary>
        /// Gets or sets the total number of ZIP files processed.
        /// </summary>
        /// <remarks>
        /// Includes both successful and failed imports.
        /// </remarks>
        public int TotalZipsProcessed { get; set; }

        /**************************************************************/
        /// <summary>
        /// Gets or sets the number of successfully imported ZIP files.
        /// </summary>
        public int SuccessfulZips { get; set; }

        /**************************************************************/
        /// <summary>
        /// Gets or sets the number of failed ZIP file imports.
        /// </summary>
        public int FailedZips { get; set; }

        /**************************************************************/
        /// <summary>
        /// Gets or sets the list of failed ZIP file names.
        /// </summary>
        /// <remarks>
        /// Used for quick identification of problematic files.
        /// </remarks>
        public List<string> FailedZipNames { get; set; } = new();

        /**************************************************************/
        /// <summary>
        /// Gets or sets the total number of documents created.
        /// </summary>
        public int TotalDocuments { get; set; }

        /**************************************************************/
        /// <summary>
        /// Gets or sets the total number of organizations created.
        /// </summary>
        public int TotalOrganizations { get; set; }

        /**************************************************************/
        /// <summary>
        /// Gets or sets the total number of products created.
        /// </summary>
        public int TotalProducts { get; set; }

        /**************************************************************/
        /// <summary>
        /// Gets or sets the total number of sections created.
        /// </summary>
        public int TotalSections { get; set; }

        /**************************************************************/
        /// <summary>
        /// Gets or sets the total number of ingredients created.
        /// </summary>
        public int TotalIngredients { get; set; }

        /**************************************************************/
        /// <summary>
        /// Gets or sets the list of error messages.
        /// </summary>
        /// <remarks>
        /// Contains detailed error information for troubleshooting failed imports.
        /// </remarks>
        public List<string> Errors { get; set; } = new();

        /**************************************************************/
        /// <summary>
        /// Gets or sets the total elapsed time for the import operation.
        /// </summary>
        public TimeSpan ElapsedTime { get; set; }

        /**************************************************************/
        /// <summary>
        /// Gets a value indicating whether the import was fully successful.
        /// </summary>
        /// <remarks>
        /// Returns true only if no ZIP files failed to import.
        /// </remarks>
        public bool IsFullySuccessful => FailedZips == 0;

        #endregion
    }
}
