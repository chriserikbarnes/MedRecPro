
using Newtonsoft.Json;

namespace MedRecPro.Models
{
    /**************************************************************/
    /// <summary>
    /// Represents the context of a completed import operation from the chat interface.
    /// </summary>
    /// <remarks>
    /// This class captures the results of SPL file imports initiated through
    /// the AI chat, allowing the AI to respond appropriately to import outcomes.
    /// </remarks>
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
        /// List of document GUIDs that were successfully imported.
        /// </summary>
        [JsonProperty("documentIds")]
        public List<string>? DocumentIds { get; set; }

        /**************************************************************/
        /// <summary>
        /// Human-readable names of the imported documents.
        /// </summary>
        [JsonProperty("documentNames")]
        public List<string>? DocumentNames { get; set; }

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
    }
}
