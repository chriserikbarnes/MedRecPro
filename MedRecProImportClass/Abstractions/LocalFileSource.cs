namespace MedRecProImportClass.Abstractions;

/**************************************************************/
/// <summary>
/// Implementation of <see cref="IFileSource"/> for local file system files.
/// </summary>
public class LocalFileSource : IFileSource
{
    private readonly string _filePath;

    /**************************************************************/
    /// <summary>
    /// Initializes a new instance with the specified file path.
    /// </summary>
    /// <param name="filePath">Full path to the local file.</param>
    public LocalFileSource(string filePath)
    {
        _filePath = filePath ?? throw new ArgumentNullException(nameof(filePath));

        if (!File.Exists(filePath))
            throw new FileNotFoundException("File not found.", filePath);
    }

    /// <inheritdoc/>
    public string FileName => Path.GetFileName(_filePath);

    /// <inheritdoc/>
    public long Length => new FileInfo(_filePath).Length;

    /// <inheritdoc/>
    public Stream OpenReadStream() => File.OpenRead(_filePath);

    /// <inheritdoc/>
    public async Task CopyToAsync(Stream target, CancellationToken cancellationToken = default)
    {
        await using var source = OpenReadStream();
        await source.CopyToAsync(target, cancellationToken);
    }
}
