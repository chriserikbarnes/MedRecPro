namespace MedRecProImportClass.Abstractions;

/**************************************************************/
/// <summary>
/// Abstraction for file input sources, replacing ASP.NET Core's IFormFile.
/// Enables the import library to work with both web uploads and local files.
/// </summary>
public interface IFileSource
{
    /// <summary>
    /// Gets the file name.
    /// </summary>
    string FileName { get; }

    /// <summary>
    /// Gets the file length in bytes.
    /// </summary>
    long Length { get; }

    /// <summary>
    /// Opens the file stream for reading.
    /// </summary>
    Stream OpenReadStream();

    /// <summary>
    /// Copies the file content to a destination stream asynchronously.
    /// </summary>
    Task CopyToAsync(Stream target, CancellationToken cancellationToken = default);
}
