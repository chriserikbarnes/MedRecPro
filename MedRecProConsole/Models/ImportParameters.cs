namespace MedRecProConsole.Models
{
    /**************************************************************/
    /// <summary>
    /// Contains the user-specified parameters for the import operation.
    /// Captures database connection, folder path, runtime limits, and discovered ZIP files.
    /// </summary>
    /// <remarks>
    /// This model is populated by the ConsoleHelper during user input gathering
    /// and passed to the ImportService for execution.
    /// </remarks>
    /// <seealso cref="ImportResults"/>
    /// <seealso cref="Services.ImportService"/>
    /// <seealso cref="Helpers.ConsoleHelper"/>
    public class ImportParameters
    {
        #region implementation

        /**************************************************************/
        /// <summary>
        /// Gets or sets the database connection string.
        /// </summary>
        /// <remarks>
        /// Can be the default Local Database Dev connection or a custom connection string
        /// provided by the user.
        /// </remarks>
        public string ConnectionString { get; set; } = string.Empty;

        /**************************************************************/
        /// <summary>
        /// Gets or sets the folder path to scan for ZIP files.
        /// </summary>
        /// <remarks>
        /// The folder is scanned recursively for all ZIP files.
        /// </remarks>
        public string ImportFolder { get; set; } = string.Empty;

        /**************************************************************/
        /// <summary>
        /// Gets or sets the optional maximum runtime in minutes.
        /// </summary>
        /// <remarks>
        /// When set, the import operation will be cancelled after this duration.
        /// Valid range is 1-1440 minutes (1 minute to 24 hours).
        /// </remarks>
        public int? MaxRuntimeMinutes { get; set; }

        /**************************************************************/
        /// <summary>
        /// Gets or sets the list of ZIP file paths found in the import folder.
        /// </summary>
        /// <remarks>
        /// Populated by scanning the ImportFolder recursively for *.zip files.
        /// </remarks>
        public List<string> ZipFiles { get; set; } = new();

        /**************************************************************/
        /// <summary>
        /// Gets or sets whether verbose mode is enabled.
        /// </summary>
        /// <remarks>
        /// When true, additional diagnostic output is displayed including
        /// orphan detection reports and Entity Framework warnings.
        /// </remarks>
        public bool VerboseMode { get; set; }

        #endregion
    }
}
