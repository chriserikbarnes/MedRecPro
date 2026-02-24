using MedRecProImportClass.Data;
using MedRecProImportClass.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Reflection;

namespace MedRecProImportClass.Service.ParsingServices
{
    /*************************************************************/
    /// <summary>
    /// Loads FDA Orange Book patent use code definitions from an embedded JSON resource
    /// and upserts them into the <see cref="OrangeBook.PatentUseCodeDefinition"/> table.
    /// The definitions map each Patent_Use_Code value (e.g., "U-141") to its human-readable
    /// description of the approved therapeutic indication covered by the patent.
    /// </summary>
    /// <remarks>
    /// The patent.txt file in the Orange Book ZIP contains use code values but NOT their
    /// definitions. Definitions are published separately on the FDA website and maintained
    /// as an embedded JSON resource (<c>OrangeBookPatentUseCodes.json</c>) in this assembly.
    ///
    /// This service is designed for a small, long-lived dataset (~4,400 rows) that does not
    /// require batching. All records are upserted in a single <see cref="DbContext.SaveChangesAsync"/>
    /// call. The upsert key is the natural primary key (<see cref="OrangeBook.PatentUseCodeDefinition.Code"/>).
    ///
    /// Follows the same constructor pattern as <see cref="OrangeBookPatentParsingService"/>
    /// (<see cref="IServiceScopeFactory"/> + <see cref="ILogger{T}"/>) and creates its own
    /// <see cref="IServiceScope"/> for thread safety.
    /// </remarks>
    /// <seealso cref="OrangeBook.PatentUseCodeDefinition"/>
    /// <seealso cref="OrangeBook.Patent"/>
    /// <seealso cref="OrangeBookPatentParsingService"/>
    /// <seealso cref="OrangeBookImportResult"/>
    public class OrangeBookPatentUseCodeParsingService
    {
        #region private fields

        /*************************************************************/
        /// <summary>
        /// Factory for creating dependency injection scopes on the processing thread.
        /// </summary>
        /// <seealso cref="IServiceScopeFactory"/>
        private readonly IServiceScopeFactory _scopeFactory;

        /*************************************************************/
        /// <summary>
        /// Logger for tracking import operations and reporting errors.
        /// </summary>
        /// <seealso cref="ILogger{OrangeBookPatentUseCodeParsingService}"/>
        private readonly ILogger<OrangeBookPatentUseCodeParsingService> _logger;

        #endregion

        #region constants

        /// <summary>
        /// Embedded resource name following the pattern: {DefaultNamespace}.{FolderPath}.{FileName}.
        /// The JSON file is located at Resources/OrangeBookPatentUseCodes.json within the assembly.
        /// </summary>
        private const string RESOURCE_NAME =
            "MedRecProImportClass.Resources.OrangeBookPatentUseCodes.json";

        #endregion

        /*************************************************************/
        /// <summary>
        /// Initializes a new instance of the <see cref="OrangeBookPatentUseCodeParsingService"/>
        /// with the required dependency injection services.
        /// </summary>
        /// <param name="scopeFactory">Factory for creating scoped service providers on the processing thread.</param>
        /// <param name="logger">Logger for tracking import operations and errors.</param>
        /// <example>
        /// <code>
        /// // Typically registered via DI:
        /// services.AddScoped&lt;OrangeBookPatentUseCodeParsingService&gt;();
        /// </code>
        /// </example>
        /// <seealso cref="IServiceScopeFactory"/>
        /// <seealso cref="ILogger{OrangeBookPatentUseCodeParsingService}"/>
        public OrangeBookPatentUseCodeParsingService(
            IServiceScopeFactory scopeFactory,
            ILogger<OrangeBookPatentUseCodeParsingService> logger)
        {
            #region implementation
            _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            #endregion
        }

        /*************************************************************/
        /// <summary>
        /// Loads patent use code definitions from the embedded JSON resource and upserts
        /// all <see cref="OrangeBook.PatentUseCodeDefinition"/> records. Existing records
        /// with changed definitions are updated; new records are inserted. The operation
        /// is idempotent and safe to re-run.
        /// </summary>
        /// <param name="result">
        /// The shared <see cref="OrangeBookImportResult"/> from prior import phases.
        /// The <see cref="OrangeBookImportResult.PatentUseCodesLoaded"/> field is populated
        /// by this method.
        /// </param>
        /// <param name="token">Cancellation token for cooperative cancellation.</param>
        /// <param name="reportProgress">Optional delegate for progress reporting.</param>
        /// <returns>The same <see cref="OrangeBookImportResult"/> with patent use code fields populated.</returns>
        /// <example>
        /// <code>
        /// var service = scope.ServiceProvider.GetRequiredService&lt;OrangeBookPatentUseCodeParsingService&gt;();
        /// var result = await service.ProcessPatentUseCodesAsync(importResult, cancellationToken);
        /// Console.WriteLine($"Loaded {result.PatentUseCodesLoaded} patent use codes");
        /// </code>
        /// </example>
        /// <remarks>
        /// Creates its own <see cref="IServiceScope"/> and <see cref="ApplicationDbContext"/>
        /// for thread safety. The database connection is manually opened and held open for
        /// the duration of the import to avoid repeated open/close overhead (following the
        /// pattern in <see cref="OrangeBookPatentParsingService"/>).
        ///
        /// The entire dataset (~4,400 rows) is upserted in a single
        /// <see cref="DbContext.SaveChangesAsync"/> call since the data is small enough
        /// to not require batching.
        /// </remarks>
        /// <seealso cref="OrangeBookImportResult"/>
        /// <seealso cref="OrangeBook.PatentUseCodeDefinition"/>
        public async Task<OrangeBookImportResult> ProcessPatentUseCodesAsync(
            OrangeBookImportResult result,
            CancellationToken token,
            Action<string>? reportProgress = null)
        {
            #region implementation

            // Create a dedicated scope for this import thread
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            // Manually open connection and keep it open for the entire import
            var connection = context.Database.GetDbConnection();
            await connection.OpenAsync(token);

            try
            {
                // Step 1: Load JSON from embedded resource
                reportProgress?.Invoke("Loading patent use codes from embedded resource...");
                var jsonContent = loadEmbeddedResource();
                _logger.LogInformation("Loaded embedded resource: {ResourceName}", RESOURCE_NAME);

                token.ThrowIfCancellationRequested();

                // Step 2: Deserialize to list of use code entries
                reportProgress?.Invoke("Parsing patent use code definitions...");
                var useCodes = JsonConvert.DeserializeObject<List<PatentUseCodeJsonEntry>>(jsonContent);

                if (useCodes == null || useCodes.Count == 0)
                {
                    result.Success = false;
                    result.Errors.Add("Patent use code JSON resource is empty or could not be deserialized.");
                    return result;
                }

                _logger.LogInformation("Deserialized {Count} patent use code definitions from JSON", useCodes.Count);

                token.ThrowIfCancellationRequested();

                // Step 3: Load existing use codes for upsert comparison
                reportProgress?.Invoke("Loading existing patent use codes for comparison...");
                var existing = await context.Set<OrangeBook.PatentUseCodeDefinition>()
                    .ToDictionaryAsync(
                        uc => uc.Code ?? string.Empty,
                        uc => uc,
                        token);

                _logger.LogInformation("Loaded {Count} existing patent use codes from database", existing.Count);

                token.ThrowIfCancellationRequested();

                // Step 4: Upsert all entries (single batch — small dataset)
                int created = 0;
                int updated = 0;

                foreach (var entry in useCodes)
                {
                    token.ThrowIfCancellationRequested();

                    var code = entry.Code?.Trim() ?? string.Empty;
                    var definition = entry.Definition?.Trim() ?? string.Empty;

                    if (string.IsNullOrWhiteSpace(code))
                    {
                        _logger.LogWarning("Skipping patent use code entry with empty code");
                        continue;
                    }

                    if (existing.TryGetValue(code, out var existingUc))
                    {
                        // Update definition if changed
                        if (!string.Equals(existingUc.Definition, definition, StringComparison.Ordinal))
                        {
                            existingUc.Definition = definition;
                            updated++;
                        }
                    }
                    else
                    {
                        // Insert new use code
                        var newEntry = new OrangeBook.PatentUseCodeDefinition
                        {
                            Code = code,
                            Definition = definition
                        };

                        context.Set<OrangeBook.PatentUseCodeDefinition>().Add(newEntry);
                        existing[code] = newEntry;
                        created++;
                    }
                }

                // Step 5: Save all changes in a single batch
                reportProgress?.Invoke($"Saving {created + updated} patent use codes...");
                await context.SaveChangesAsync(token);

                result.PatentUseCodesLoaded = created + updated;

                var message = $"{useCodes.Count} patent use codes processed ({created} created, {updated} updated).";
                result.Message = result.Message != null
                    ? $"{result.Message} {message}"
                    : message;

                _logger.LogInformation("Patent use codes: {Created} created, {Updated} updated", created, updated);
                reportProgress?.Invoke(message);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Orange Book patent use code import was cancelled.");
                result.Success = false;
                result.Errors.Add("Patent use code import was cancelled.");
            }
            catch (Exception ex)
            {
                var fullMessage = getFullExceptionMessage(ex);
                _logger.LogError(ex, "Critical error during Orange Book patent use code import: {FullMessage}", fullMessage);
                result.Success = false;
                result.Errors.Add($"Patent use code import critical error: {fullMessage}");
            }
            finally
            {
                await connection.CloseAsync();
            }

            return result;

            #endregion
        }

        #region private methods

        /*************************************************************/
        /// <summary>
        /// Loads the embedded JSON resource from the current assembly.
        /// </summary>
        /// <returns>The full JSON text content of the embedded resource.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the embedded resource is not found, typically due to a missing
        /// <c>&lt;EmbeddedResource&gt;</c> entry in the .csproj or an incorrect resource name.
        /// The exception message includes the list of available resource names for diagnostics.
        /// </exception>
        /// <seealso cref="RESOURCE_NAME"/>
        private string loadEmbeddedResource()
        {
            #region implementation

            var assembly = Assembly.GetExecutingAssembly();
            using var stream = assembly.GetManifestResourceStream(RESOURCE_NAME);

            if (stream == null)
            {
                var availableResources = string.Join(", ", assembly.GetManifestResourceNames());
                throw new InvalidOperationException(
                    $"Embedded resource '{RESOURCE_NAME}' not found in assembly " +
                    $"'{assembly.GetName().Name}'. Available resources: [{availableResources}]");
            }

            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();

            #endregion
        }

        #endregion

        #region exception helpers

        /*************************************************************/
        /// <summary>
        /// Walks the <see cref="Exception.InnerException"/> chain and concatenates all
        /// messages into a single string separated by " → ". This is critical for
        /// <see cref="Microsoft.EntityFrameworkCore.DbUpdateException"/> which wraps
        /// the actual SQL Server error in one or more inner exceptions.
        /// </summary>
        /// <param name="ex">The outermost exception.</param>
        /// <returns>
        /// A concatenated string of all exception messages in the chain,
        /// e.g., "An error occurred... → String or binary data would be truncated."
        /// </returns>
        /// <seealso cref="ProcessPatentUseCodesAsync"/>
        private static string getFullExceptionMessage(Exception ex)
        {
            #region implementation

            var messages = new List<string>();
            var current = ex;

            while (current != null)
            {
                if (!string.IsNullOrWhiteSpace(current.Message))
                    messages.Add(current.Message);
                current = current.InnerException;
            }

            return string.Join(" → ", messages);

            #endregion
        }

        #endregion

        #region private types

        /*************************************************************/
        /// <summary>
        /// DTO for deserializing the embedded JSON resource. Matches the JSON property
        /// names in OrangeBookPatentUseCodes.json: <c>Code</c> and <c>Definition</c>.
        /// </summary>
        /// <remarks>
        /// This is a separate DTO from <see cref="OrangeBook.PatentUseCodeDefinition"/>
        /// because the JSON property names may differ from the EF Core entity property
        /// names (e.g., if column mapping attributes are used). Using a dedicated DTO
        /// decouples the serialization format from the database schema.
        /// </remarks>
        private class PatentUseCodeJsonEntry
        {
            /*************************************************************/
            /// <summary>
            /// The patent use code (e.g., "U-1", "U-141").
            /// </summary>
            public string? Code { get; set; }

            /*************************************************************/
            /// <summary>
            /// The human-readable definition of the use code.
            /// </summary>
            public string? Definition { get; set; }
        }

        #endregion
    }
}
