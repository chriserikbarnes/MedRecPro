using MedRecPro.Models;
using ImportBufferedFile = MedRecProImportClass.Models.BufferedFile;
using ImportSplImportService = MedRecProImportClass.Service.SplImportService;
using ImportSplZipImportResult = MedRecProImportClass.Models.SplZipImportResult;

namespace MedRecPro.Service
{
    /**************************************************************/
    /// <summary>
    /// Compatibility adapter for the import-library SPL ZIP import service.
    /// </summary>
    /// <remarks>
    /// Preserves the web project's existing upload boundary while delegating ZIP traversal,
    /// XML extraction, duplicate checks, and parser orchestration to the import library.
    /// </remarks>
    /// <seealso cref="ImportSplImportService"/>
    /// <seealso cref="BufferedFile"/>
    public class SplImportService
    {
        #region implementation

        /**************************************************************/
        /// <summary>
        /// Import-library service that owns the SPL import workflow.
        /// </summary>
        /// <seealso cref="ImportSplImportService"/>
        private readonly ImportSplImportService _importService;

        /**************************************************************/
        /// <summary>
        /// Initializes a new adapter instance for SPL ZIP import processing.
        /// </summary>
        /// <param name="importService">Import-library service that performs the actual import workflow.</param>
        /// <exception cref="ArgumentNullException">Thrown when importService is null.</exception>
        /// <remarks>
        /// The adapter keeps constructor injection explicit and avoids resolving services
        /// from the container at runtime.
        /// </remarks>
        /// <example>
        /// <code>
        /// services.AddScoped&lt;SplImportService&gt;();
        /// </code>
        /// </example>
        /// <seealso cref="ImportSplImportService"/>
        public SplImportService(ImportSplImportService importService)
        {
            #region implementation
            _importService = importService ?? throw new ArgumentNullException(nameof(importService));
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Processes web-buffered SPL ZIP files by delegating to the import-library service.
        /// </summary>
        /// <param name="bufferedFiles">Web-project buffered upload files to process.</param>
        /// <param name="currentUserId">Identifier of the user who initiated the import.</param>
        /// <param name="token">Cancellation token for the import operation.</param>
        /// <param name="fileCounter">Callback for progress percentage updates.</param>
        /// <param name="updateStatus">Optional callback for status text updates.</param>
        /// <param name="results">Optional callback for accumulated import-library results.</param>
        /// <returns>Import-library ZIP result DTOs for each processed upload file.</returns>
        /// <exception cref="ArgumentNullException">Thrown when bufferedFiles or fileCounter is null.</exception>
        /// <exception cref="OperationCanceledException">Thrown when the cancellation token is already canceled.</exception>
        /// <remarks>
        /// The only local transformation is the web <see cref="BufferedFile"/> to import-library
        /// buffered file conversion. Import workflow behavior stays in the import library.
        /// </remarks>
        /// <example>
        /// <code>
        /// var results = await splImportService.ProcessZipFilesAsync(files, userId, token, progress => { });
        /// </code>
        /// </example>
        /// <seealso cref="ImportSplImportService.ProcessZipFilesAsync"/>
        /// <seealso cref="ImportSplZipImportResult"/>
        public Task<List<ImportSplZipImportResult>> ProcessZipFilesAsync(
            List<BufferedFile> bufferedFiles,
            long? currentUserId,
            CancellationToken token,
            Action<int> fileCounter,
            Action<string>? updateStatus = null,
            Action<List<ImportSplZipImportResult>>? results = null)
        {
            #region implementation
            ArgumentNullException.ThrowIfNull(bufferedFiles);
            ArgumentNullException.ThrowIfNull(fileCounter);
            token.ThrowIfCancellationRequested();

            var importBufferedFiles = bufferedFiles
                .Select(toImportBufferedFile)
                .ToList();

            return _importService.ProcessZipFilesAsync(
                importBufferedFiles,
                currentUserId,
                token,
                fileCounter,
                updateStatus,
                results);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Converts one web buffered file reference into the import-library buffered file shape.
        /// </summary>
        /// <param name="bufferedFile">Web buffered file reference to convert.</param>
        /// <returns>Import-library buffered file reference pointing to the same temporary file.</returns>
        /// <exception cref="ArgumentNullException">Thrown when bufferedFile is null.</exception>
        /// <remarks>
        /// The adapter does not copy file contents; ownership and cleanup remain with the caller
        /// that created the temporary files.
        /// </remarks>
        /// <seealso cref="BufferedFile"/>
        /// <seealso cref="ImportBufferedFile"/>
        private static ImportBufferedFile toImportBufferedFile(BufferedFile bufferedFile)
        {
            #region implementation
            ArgumentNullException.ThrowIfNull(bufferedFile);

            return new ImportBufferedFile
            {
                FileName = bufferedFile.FileName,
                TempFilePath = bufferedFile.TempFilePath
            };
            #endregion
        }

        #endregion
    }
}
