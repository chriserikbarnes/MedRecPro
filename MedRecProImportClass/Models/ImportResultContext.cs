using Newtonsoft.Json;

namespace MedRecProImportClass.Models
{
    /**************************************************************/
    /// <summary>
    /// Represents the context of a completed import operation from the chat interface.
    /// </summary>
    /// <remarks>
    /// This class captures the results of SPL file imports initiated through
    /// the AI chat, allowing the AI to respond appropriately to import outcomes.
    /// The structure matches the frontend's extractImportResults() output.
    /// </remarks>
    /// <seealso cref="AiAgentRequest"/>
    public class ImportResultContext
    {
        /**************************************************************/
        /// <summary>
        /// Indicates whether the import operation completed successfully.
        /// </summary>
        [JsonProperty("success")]
        public bool Success { get; set; }

        /**************************************************************/
        /// <summary>
        /// List of document GUIDs (splGUID) that were successfully imported.
        /// </summary>
        [JsonProperty("documentIds")]
        public List<string>? DocumentIds { get; set; }

        /**************************************************************/
        /// <summary>
        /// Human-readable names of the imported documents (file names).
        /// </summary>
        [JsonProperty("documentNames")]
        public List<string>? DocumentNames { get; set; }

        /**************************************************************/
        /// <summary>
        /// Aggregated statistics from all imported files.
        /// </summary>
        [JsonProperty("statistics")]
        public ImportStatistics? Statistics { get; set; }

        /**************************************************************/
        /// <summary>
        /// Total number of XML files processed across all ZIP archives.
        /// </summary>
        [JsonProperty("totalFilesProcessed")]
        public int TotalFilesProcessed { get; set; }

        /**************************************************************/
        /// <summary>
        /// Total number of XML files that were successfully imported.
        /// </summary>
        [JsonProperty("totalFilesSucceeded")]
        public int TotalFilesSucceeded { get; set; }

        /**************************************************************/
        /// <summary>
        /// A summary message describing the import result.
        /// </summary>
        [JsonProperty("message")]
        public string? Message { get; set; }

        /**************************************************************/
        /// <summary>
        /// Error message if the import failed.
        /// </summary>
        [JsonProperty("error")]
        public string? Error { get; set; }

        /**************************************************************/
        /// <summary>
        /// Operation ID for tracking long-running imports.
        /// </summary>
        [JsonProperty("operationId")]
        public string? OperationId { get; set; }

        /**************************************************************/
        /// <summary>
        /// URL to check import progress status.
        /// </summary>
        [JsonProperty("progressUrl")]
        public string? ProgressUrl { get; set; }
    }

    /**************************************************************/
    /// <summary>
    /// Aggregated statistics from an import operation.
    /// </summary>
    /// <remarks>
    /// Matches the statistics returned in the fileResults from the import API.
    /// These counts represent totals across all successfully imported files.
    /// </remarks>
    public class ImportStatistics
    {
        /**************************************************************/
        /// <summary>
        /// Number of Label.Document entities created.
        /// </summary>
        [JsonProperty("documentsCreated")]
        public int DocumentsCreated { get; set; }

        /**************************************************************/
        /// <summary>
        /// Number of Label.Organization entities created.
        /// </summary>
        [JsonProperty("organizationsCreated")]
        public int OrganizationsCreated { get; set; }

        /**************************************************************/
        /// <summary>
        /// Number of Label.Product entities created.
        /// </summary>
        [JsonProperty("productsCreated")]
        public int ProductsCreated { get; set; }

        /**************************************************************/
        /// <summary>
        /// Number of Label.Section entities created.
        /// </summary>
        [JsonProperty("sectionsCreated")]
        public int SectionsCreated { get; set; }

        /**************************************************************/
        /// <summary>
        /// Number of ingredient entities created (active and inactive).
        /// </summary>
        [JsonProperty("ingredientsCreated")]
        public int IngredientsCreated { get; set; }

        /**************************************************************/
        /// <summary>
        /// Number of product element entities created 
        /// (routes, characteristics, marketing categories, etc.).
        /// </summary>
        [JsonProperty("productElementsCreated")]
        public int ProductElementsCreated { get; set; }

        /**************************************************************/
        /// <summary>
        /// Returns a formatted summary string of the statistics.
        /// </summary>
        /// <returns>Human-readable statistics summary.</returns>
        public override string ToString()
        {
            var parts = new List<string>();

            if (DocumentsCreated > 0) parts.Add($"{DocumentsCreated} document(s)");
            if (OrganizationsCreated > 0) parts.Add($"{OrganizationsCreated} organization(s)");
            if (ProductsCreated > 0) parts.Add($"{ProductsCreated} product(s)");
            if (SectionsCreated > 0) parts.Add($"{SectionsCreated} section(s)");
            if (IngredientsCreated > 0) parts.Add($"{IngredientsCreated} ingredient(s)");
            if (ProductElementsCreated > 0) parts.Add($"{ProductElementsCreated} product element(s)");

            return parts.Count > 0 ? string.Join(", ", parts) : "No entities created";
        }
    }
}