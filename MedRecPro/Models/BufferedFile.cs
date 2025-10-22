using static MedRecPro.Models.Label;

namespace MedRecPro.Models
{
    /**************************************************************/
    /// <summary>
    /// Represents a file that has been buffered to temporary storage for processing.
    /// </summary>
    /// <remarks>
    /// This class provides a way to store uploaded files in temporary locations while maintaining
    /// reference to the original filename. Includes utility methods for buffering multiple files
    /// asynchronously with proper cleanup handling in case of failures.
    /// </remarks>
    /// <seealso cref="IFormFile"/>
    /// <seealso cref="Label"/>
    public class BufferedFile
    {
        #region implementation

        /**************************************************************/
        /// <summary>
        /// Gets or sets the original filename of the uploaded file.
        /// </summary>
        /// <seealso cref="Label"/>
        public string FileName { get; set; } = default!;

        /**************************************************************/
        /// <summary>
        /// Gets or sets the temporary file path where the file content is stored.
        /// </summary>
        /// <seealso cref="Label"/>
        public string TempFilePath { get; set; } = default!;

        #endregion

        /**************************************************************/
        /// <summary>
        /// Asynchronously buffers multiple uploaded files to temporary storage locations.
        /// </summary>
        /// <param name="files">Collection of uploaded files to buffer to temporary storage.</param>
        /// <param name="token">Cancellation token for aborting the operation if needed.</param>
        /// <returns>A list of BufferedFile objects containing original filenames and temporary paths.</returns>
        /// <remarks>
        /// This method processes files sequentially, copying each to a temporary location.
        /// If any file fails to buffer, all previously created temporary files are cleaned up
        /// and the original exception is rethrown to maintain operation atomicity.
        /// Temporary files should be cleaned up by the caller when processing is complete.
        /// </remarks>
        /// <example>
        /// <code>
        /// var bufferedFiles = await BufferFilesToTempAsync(uploadedFiles, cancellationToken);
        /// try
        /// {
        ///     // Process buffered files
        ///     foreach (var file in bufferedFiles)
        ///     {
        ///         // Work with file.TempFilePath
        ///     }
        /// }
        /// finally
        /// {
        ///     // Clean up temporary files
        ///     foreach (var file in bufferedFiles)
        ///     {
        ///         File.Delete(file.TempFilePath);
        ///     }
        /// }
        /// </code>
        /// </example>
        /// <seealso cref="IFormFile"/>
        /// <seealso cref="CancellationToken"/>
        /// <seealso cref="FileStream"/>
        /// <seealso cref="Label"/>
        public async Task<List<BufferedFile>> BufferFilesToTempAsync(List<IFormFile> files, CancellationToken token)
        {
            #region implementation
            var result = new List<BufferedFile>();

            // Process each uploaded file sequentially
            foreach (var file in files)
            {
                // Create a unique temporary file path
                var tempFile = Path.GetTempFileName();

                try
                {
                    // Copy the uploaded file content to the temporary location
                    using (var fs = new FileStream(tempFile, FileMode.Create, FileAccess.Write))
                    {
                        await file.CopyToAsync(fs, token);
                    }

                    // Add successfully buffered file to the result collection
                    result.Add(new BufferedFile
                    {
                        FileName = file.FileName,
                        TempFilePath = tempFile
                    });
                }
                catch
                {
                    // Clean up any temp files created so far if something fails
                    foreach (var buffered in result)
                    {
                        try { File.Delete(buffered.TempFilePath); } catch { /* Ignore cleanup errors */ }
                    }

                    // Clean up the current temp file that failed
                    try { File.Delete(tempFile); } catch { /* Ignore cleanup errors */ }

                    throw; // Rethrow original exception to maintain error context
                }
            }

            return result;
            #endregion
        }
    }
}