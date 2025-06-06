

namespace MedRecPro.Models
{
    /// <summary>
    /// Provide summary information for the import process.
    /// </summary>
    public class SplFileImportResult
    {
        public string? FileName { get; set; }
        public bool Success { get; set; }
        public string? Message { get; set; }
        public int DocumentsCreated { get; set; }
        public int OrganizationsCreated { get; set; }
        // Add more counters as needed
        public int ProductsCreated { get; set; }
        public int SectionsCreated { get; set; }
        public List<string> Errors { get; set; } = new List<string>();
    }

    /// <summary>
    /// Represents the result of importing a ZIP files containing
    /// SPL files.
    /// </summary>
    public class SplZipImportResult
    {
        public string? ZipFileName { get; set; }
        public List<SplFileImportResult> FileResults { get; set; } = new List<SplFileImportResult>();
        public bool OverallSuccess => FileResults.All(f => f.Success);
        public int TotalFilesProcessed => FileResults.Count;
        public int TotalFilesSucceeded => FileResults.Count(f => f.Success);
    }
}