using MedRecPro.Helpers;

namespace MedRecPro.Service.Common
{
    /**************************************************************/
    /// <summary>
    /// Generic abstract base service providing common logging and configuration functionality for derived services.
    /// Centralizes standard logging operations to reduce code duplication across service implementations
    /// and provides a consistent, strongly-typed logging interface for all medical record processing services.
    /// </summary>
    /// <typeparam name="T">The type of the derived service for strongly-typed logging context</typeparam>
    /// <seealso cref="ILogger{TCategoryName}"/>
    /// <seealso cref="IEncryptionService"/>
    /// <seealso cref="IDictionaryUtilityService"/>
    /// <example>
    /// <code>
    /// public class DocumentService : BaseService&lt;DocumentService&gt;
    /// {
    ///     public DocumentService(ILogger&lt;DocumentService&gt; logger) : base(logger)
    ///     {
    ///     }
    ///     
    ///     public void ProcessDocument()
    ///     {
    ///         LogInformation("Starting document processing");
    ///         // Process data with strongly-typed logging context
    ///         LogInformation("Document processing completed");
    ///     }
    /// }
    /// </code>
    /// </example>
    /// <remarks>
    /// This generic base class follows the template method pattern by providing common logging infrastructure
    /// while allowing derived classes to implement specific business logic. The generic constraint ensures
    /// strongly-typed logging with proper category names for better log filtering and analysis.
    /// All logging methods are protected to allow access by derived classes while maintaining encapsulation.
    /// </remarks>
    public abstract class BaseService<T> where T : class
    {
        #region protected fields

        /**************************************************************/
        /// <summary>
        /// Strongly-typed logger instance for recording service operations and diagnostic information.
        /// Provides standardized logging capabilities with proper category naming across all derived service implementations
        /// for consistent operation tracking and error reporting with enhanced filtering capabilities.
        /// </summary>
        /// <seealso cref="ILogger{TCategoryName}"/>
        /// <seealso cref="LogInformation"/>
        /// <seealso cref="LogError"/>
        /// <seealso cref="LogDebug"/>
        protected readonly ILogger<T> Logger;

        #endregion

        #region constructor

        /**************************************************************/
        /// <summary>
        /// Initializes a new instance of the BaseService with the specified strongly-typed logger.
        /// Validates the logger parameter and sets up the common logging infrastructure with proper
        /// category naming for use by derived service implementations.
        /// </summary>
        /// <param name="logger">Strongly-typed logger instance for operation tracking and diagnostics</param>
        /// <seealso cref="ILogger{TCategoryName}"/>
        /// <exception cref="ArgumentNullException">Thrown when logger parameter is null</exception>
        /// <example>
        /// <code>
        /// public class PatientService : BaseService&lt;PatientService&gt;
        /// {
        ///     public PatientService(ILogger&lt;PatientService&gt; logger) : base(logger)
        ///     {
        ///         // Additional initialization if needed
        ///     }
        /// }
        /// 
        /// // Dependency injection registration
        /// services.AddScoped&lt;PatientService&gt;();
        /// </code>
        /// </example>
        /// <remarks>
        /// This constructor enforces the requirement for strongly-typed logging in all derived services
        /// and ensures consistent error handling for null dependencies. The generic type constraint
        /// provides compile-time safety for logging category names.
        /// </remarks>
        protected BaseService(ILogger<T> logger)
        {
            #region implementation

            Logger = logger ?? throw new ArgumentNullException(nameof(logger));

            #endregion
        }

        #endregion

        #region protected logging methods

        /**************************************************************/
        /// <summary>
        /// Logs an informational message with optional parameters for structured logging.
        /// Provides a standardized way for derived services to record operational information
        /// with consistent formatting and parameter handling using the strongly-typed logger.
        /// </summary>
        /// <param name="message">The informational message template to log</param>
        /// <param name="args">Optional parameters for message template substitution</param>
        /// <seealso cref="ILogger"/>
        /// <seealso cref="LogError"/>
        /// <seealso cref="LogDebug"/>
        /// <example>
        /// <code>
        /// LogInformation("Processing document {DocumentId} for user {UserId}", documentId, userId);
        /// LogInformation("Operation completed successfully");
        /// </code>
        /// </example>
        /// <remarks>
        /// Uses structured logging with parameter substitution for better log analysis and filtering.
        /// The message template supports named placeholders that correspond to the provided arguments.
        /// The strongly-typed logger ensures proper category naming for enhanced log organization.
        /// </remarks>
        protected void LogInformation(string message, params object[] args)
        {
            #region implementation

            // Delegate to the underlying strongly-typed logger with structured parameter support
            Logger.LogInformation(message, args);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Logs an error message with exception details and optional parameters for comprehensive error tracking.
        /// Provides a standardized way for derived services to record error information
        /// with exception context and structured parameter support using the strongly-typed logger.
        /// </summary>
        /// <param name="ex">The exception that occurred during the operation</param>
        /// <param name="message">The error message template to log</param>
        /// <param name="args">Optional parameters for message template substitution</param>
        /// <seealso cref="ILogger"/>
        /// <seealso cref="LogInformation"/>
        /// <seealso cref="LogDebug"/>
        /// <example>
        /// <code>
        /// try
        /// {
        ///     ProcessDocument(documentId);
        /// }
        /// catch (Exception ex)
        /// {
        ///     LogError(ex, "Failed to process document {DocumentId}", documentId);
        ///     throw;
        /// }
        /// </code>
        /// </example>
        /// <remarks>
        /// Captures both the exception details and custom message context for comprehensive error reporting.
        /// The exception stack trace and properties are automatically included in the log entry.
        /// The strongly-typed logger ensures proper categorization for error tracking and analysis.
        /// </remarks>
        protected void LogError(Exception ex, string message, params object[] args)
        {
            #region implementation

            // Delegate to the underlying strongly-typed logger with exception context and structured parameters
            Logger.LogError(ex, message, args);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Logs a debug message with optional parameters for detailed diagnostic information.
        /// Provides a standardized way for derived services to record detailed operational information
        /// that is useful during development and troubleshooting scenarios using the strongly-typed logger.
        /// </summary>
        /// <param name="message">The debug message template to log</param>
        /// <param name="args">Optional parameters for message template substitution</param>
        /// <seealso cref="ILogger"/>
        /// <seealso cref="LogInformation"/>
        /// <seealso cref="LogError"/>
        /// <example>
        /// <code>
        /// LogDebug("Starting validation for section {SectionId} with {RuleCount} rules", sectionId, rules.Count);
        /// LogDebug("Cache hit for key {CacheKey}", cacheKey);
        /// </code>
        /// </example>
        /// <remarks>
        /// Debug messages are typically only recorded when debug logging is enabled in the application configuration.
        /// Use for detailed tracing that would be excessive in production but valuable during development.
        /// The strongly-typed logger provides proper categorization for debug message filtering and analysis.
        /// </remarks>
        protected void LogDebug(string message, params object[] args)
        {
            #region implementation

            // Delegate to the underlying strongly-typed logger with structured parameter support for debug-level logging
            Logger.LogDebug(message, args);

            #endregion
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Service interface for common encryption and decryption operations across the application.
    /// Provides standardized methods for handling encrypted medical record data with consistent
    /// decryption logic to avoid duplicating security-sensitive operations throughout the system.
    /// </summary>
    /// <seealso cref="EncryptionService"/>
    /// <seealso cref="TextUtil"/>
    /// <seealso cref="IConfiguration"/>
    /// <remarks>
    /// This interface centralizes encryption operations to ensure consistent security practices
    /// and simplify maintenance of cryptographic functionality across medical record services.
    /// </remarks>
    public interface IEncryptionService
    {
        /**************************************************************/
        /// <summary>
        /// Decrypts an encrypted string value to an integer with null-safe handling.
        /// Attempts to decrypt the provided encrypted value and parse it as an integer,
        /// returning null if the value is null, empty, or cannot be parsed.
        /// </summary>
        /// <param name="encryptedValue">The encrypted string value to decrypt and parse</param>
        /// <returns>The decrypted integer value, or null if decryption or parsing fails</returns>
        /// <seealso cref="DecryptToString"/>
        /// <seealso cref="TextUtil.Decrypt"/>
        /// <example>
        /// <code>
        /// var encryptedId = GetEncryptedPatientId();
        /// var patientId = encryptionService.DecryptToInt(encryptedId);
        /// 
        /// if (patientId.HasValue)
        /// {
        ///     ProcessPatient(patientId.Value);
        /// }
        /// </code>
        /// </example>
        /// <remarks>
        /// This method provides safe integer decryption for encrypted ID fields and numeric data
        /// commonly found in medical record systems where sensitive identifiers are encrypted.
        /// </remarks>
        int? DecryptToInt(string? encryptedValue);

        /**************************************************************/
        /// <summary>
        /// Decrypts an encrypted string value to a plain text string with null-safe handling.
        /// Attempts to decrypt the provided encrypted value, returning an empty string
        /// if the value is null or empty to ensure consistent string handling.
        /// </summary>
        /// <param name="encryptedValue">The encrypted string value to decrypt</param>
        /// <returns>The decrypted string value, or empty string if input is null or empty</returns>
        /// <seealso cref="DecryptToInt"/>
        /// <seealso cref="TextUtil.Decrypt"/>
        /// <example>
        /// <code>
        /// var encryptedName = GetEncryptedPatientName();
        /// var patientName = encryptionService.DecryptToString(encryptedName);
        /// 
        /// DisplayPatientName(patientName); // Safe to use even if original was null
        /// </code>
        /// </example>
        /// <remarks>
        /// This method provides safe string decryption for encrypted text fields such as names,
        /// addresses, and other sensitive textual data in medical record systems.
        /// </remarks>
        string? DecryptToString(string? encryptedValue);
    }

    /**************************************************************/
    /// <summary>
    /// Implementation of encryption service using the existing TextUtil pattern for consistent cryptographic operations.
    /// Provides concrete implementations for decryption operations using configured private key secrets
    /// to ensure standardized and secure handling of encrypted medical record data.
    /// </summary>
    /// <seealso cref="IEncryptionService"/>
    /// <seealso cref="TextUtil"/>
    /// <seealso cref="IConfiguration"/>
    /// <remarks>
    /// This implementation leverages the existing TextUtil cryptographic infrastructure
    /// while providing a service-based interface for dependency injection and testing.
    /// The private key is loaded from application configuration for security flexibility.
    /// </remarks>
    public class EncryptionService : IEncryptionService
    {
        #region private fields

        /**************************************************************/
        /// <summary>
        /// Private key secret used for decrypting encrypted medical record data.
        /// Loaded from application configuration to ensure secure and flexible key management
        /// for cryptographic operations across the medical record system.
        /// </summary>
        /// <seealso cref="IConfiguration"/>
        /// <seealso cref="TextUtil.Decrypt"/>
        private readonly string _pkSecret;

        #endregion

        #region constructor

        /**************************************************************/
        /// <summary>
        /// Initializes a new instance of the EncryptionService with configuration-based key management.
        /// Loads the private key secret from application configuration and validates its availability
        /// to ensure cryptographic operations can be performed reliably.
        /// </summary>
        /// <param name="configuration">Application configuration containing encryption settings</param>
        /// <seealso cref="IConfiguration"/>
        /// <exception cref="InvalidOperationException">Thrown when PK encryption secret is not configured</exception>
        /// <example>
        /// <code>
        /// // Dependency injection registration
        /// services.AddScoped&lt;IEncryptionService, EncryptionService&gt;();
        /// 
        /// // Configuration in appsettings.json:
        /// {
        ///   "Security": {
        ///     "DB": {
        ///       "PKSecret": "your-encryption-key-here"
        ///     }
        ///   }
        /// }
        /// </code>
        /// </example>
        /// <remarks>
        /// The constructor enforces the presence of the encryption key in configuration
        /// to fail fast if security settings are not properly configured.
        /// </remarks>
        public EncryptionService(IConfiguration configuration)
        {
            #region implementation

            // Load private key from configuration with validation to ensure security settings are available
            _pkSecret = configuration.GetSection("Security:DB:PKSecret").Value
                ?? throw new InvalidOperationException("PK encryption secret not configured");

            #endregion
        }

        #endregion

        #region public methods

        /**************************************************************/
        /// <summary>
        /// Decrypts an encrypted string value to an integer with comprehensive null-safe handling and parsing.
        /// Performs decryption using the configured private key and attempts to parse the result as an integer,
        /// providing robust error handling for invalid or null input values.
        /// </summary>
        /// <param name="encryptedValue">The encrypted string value to decrypt and parse as integer</param>
        /// <returns>The decrypted integer value, or null if input is null/empty or parsing fails</returns>
        /// <seealso cref="DecryptToString"/>
        /// <seealso cref="TextUtil.Decrypt"/>
        /// <seealso cref="Int32.TryParse"/>
        /// <example>
        /// <code>
        /// var service = new EncryptionService(configuration);
        /// var encryptedPatientId = database.GetEncryptedPatientId();
        /// var patientId = service.DecryptToInt(encryptedPatientId);
        /// 
        /// if (patientId.HasValue)
        /// {
        ///     var patient = patientRepository.GetById(patientId.Value);
        ///     ProcessPatientData(patient);
        /// }
        /// else
        /// {
        ///     LogError("Invalid or missing patient ID");
        /// }
        /// </code>
        /// </example>
        /// <remarks>
        /// This method handles common scenarios in medical record systems where numeric IDs
        /// are encrypted for security. The null-safe design prevents exceptions when dealing
        /// with missing or corrupted encrypted data.
        /// </remarks>
        public int? DecryptToInt(string? encryptedValue)
        {
            #region implementation

            // Return null immediately for null or empty input to avoid unnecessary processing
            if (string.IsNullOrEmpty(encryptedValue))
                return null;

            // Decrypt the value using the configured private key
            var decryptedValue = TextUtil.Decrypt(encryptedValue, _pkSecret);

            // Attempt to parse the decrypted value as integer, returning null if parsing fails
            return Int32.TryParse(decryptedValue, out int number) ? number : null;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Decrypts an encrypted string value to plain text with null-safe handling and consistent return values.
        /// Performs decryption using the configured private key and ensures a consistent string return
        /// even when input values are null or empty to simplify consuming code.
        /// </summary>
        /// <param name="encryptedValue">The encrypted string value to decrypt</param>
        /// <returns>The decrypted string value, or empty string if input is null or empty</returns>
        /// <seealso cref="DecryptToInt"/>
        /// <seealso cref="TextUtil.Decrypt"/>
        /// <example>
        /// <code>
        /// var service = new EncryptionService(configuration);
        /// var encryptedPatientName = database.GetEncryptedPatientName();
        /// var patientName = service.DecryptToString(encryptedPatientName);
        /// 
        /// // Safe to use without additional null checks
        /// displayLabel.Text = $"Patient: {patientName}";
        /// 
        /// if (!string.IsNullOrEmpty(patientName))
        /// {
        ///     ProcessPatientName(patientName);
        /// }
        /// </code>
        /// </example>
        /// <remarks>
        /// This method provides safe string decryption with predictable return values,
        /// eliminating the need for null checking in consuming code. Returns empty string
        /// instead of null to follow string handling conventions.
        /// </remarks>
        public string? DecryptToString(string? encryptedValue)
        {
            #region implementation

            // Return empty string immediately for null or empty input to provide consistent string handling
            if (string.IsNullOrEmpty(encryptedValue))
                return string.Empty;

            // Decrypt and return the plain text value using the configured private key
            return TextUtil.Decrypt(encryptedValue, _pkSecret);

            #endregion
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Service interface for common dictionary operations used across template helpers and services.
    /// Provides standardized methods for safe dictionary access with flexible key matching
    /// to reduce code duplication and improve reliability when working with dynamic data structures.
    /// </summary>
    /// <seealso cref="DictionaryUtilityService"/>
    /// <seealso cref="IDictionary{TKey, TValue}"/>
    /// <remarks>
    /// This interface addresses common challenges in medical record template processing
    /// where dictionary keys may vary in casing or format, requiring flexible lookup strategies
    /// to ensure reliable data access across different data sources and templates.
    /// </remarks>
    public interface IDictionaryUtilityService
    {
        /**************************************************************/
        /// <summary>
        /// Safely retrieves a value from a dictionary using flexible key matching strategies.
        /// Attempts multiple key matching approaches including exact match, case variations,
        /// and case-insensitive search to handle different key formatting conventions.
        /// </summary>
        /// <param name="dictionary">The dictionary to search for the specified key</param>
        /// <param name="key">The key to search for using flexible matching</param>
        /// <returns>The value associated with the key, or null if not found</returns>
        /// <seealso cref="SafeGet{T}"/>
        /// <seealso cref="GetAvailableKeys"/>
        /// <example>
        /// <code>
        /// var data = new Dictionary&lt;string, object?&gt; { { "PatientName", "John Doe" } };
        /// var value1 = service.SafeGet(data, "PatientName"); // Exact match
        /// var value2 = service.SafeGet(data, "patientName"); // Case-insensitive match
        /// var value3 = service.SafeGet(data, "patient_name"); // Returns null
        /// </code>
        /// </example>
        object? SafeGet(IDictionary<string, object?> dictionary, string key);

        /**************************************************************/
        /// <summary>
        /// Safely retrieves a typed value from a dictionary using flexible key matching and type conversion.
        /// Combines flexible key matching with automatic type conversion to provide strongly-typed
        /// access to dictionary values with graceful handling of type mismatches.
        /// </summary>
        /// <typeparam name="T">The expected return type for the value</typeparam>
        /// <param name="dictionary">The dictionary to search for the specified key</param>
        /// <param name="key">The key to search for using flexible matching</param>
        /// <returns>The value converted to the specified type, or default(T) if not found or conversion fails</returns>
        /// <seealso cref="SafeGet(IDictionary{string, object?}, string)"/>
        /// <seealso cref="Convert"/>
        /// <example>
        /// <code>
        /// var data = new Dictionary&lt;string, object?&gt; { { "PatientAge", "45" } };
        /// var age = service.SafeGet&lt;int&gt;(data, "PatientAge"); // Returns 45
        /// var name = service.SafeGet&lt;string&gt;(data, "PatientName"); // Returns null/default
        /// </code>
        /// </example>
        T? SafeGet<T>(IDictionary<string, object?> dictionary, string key);

        /**************************************************************/
        /// <summary>
        /// Generates a formatted string containing all available keys in a dictionary for diagnostic purposes.
        /// Provides a convenient way to inspect dictionary contents during development and troubleshooting,
        /// with sorted key listing for consistent output formatting.
        /// </summary>
        /// <param name="dictionary">The dictionary to list keys for, or null</param>
        /// <returns>Comma-separated list of sorted keys, or "null" if dictionary is null</returns>
        /// <seealso cref="SafeGet"/>
        /// <seealso cref="SafeGet{T}"/>
        /// <example>
        /// <code>
        /// var data = new Dictionary&lt;string, object&gt; 
        /// { 
        ///     { "PatientName", "John" }, 
        ///     { "Age", 45 } 
        /// };
        /// var keys = service.GetAvailableKeys(data); // Returns "Age, PatientName"
        /// Console.WriteLine($"Available keys: {keys}");
        /// </code>
        /// </example>
        string GetAvailableKeys(IDictionary<string, object>? dictionary);
    }

    /**************************************************************/
    /// <summary>
    /// Implementation of dictionary utilities to reduce duplication in template helpers and provide robust data access.
    /// Offers comprehensive dictionary access methods with flexible key matching, type conversion,
    /// and diagnostic capabilities for reliable template data processing in medical record systems.
    /// </summary>
    /// <seealso cref="IDictionaryUtilityService"/>
    /// <seealso cref="IDictionary{TKey, TValue}"/>
    /// <seealso cref="Convert"/>
    /// <remarks>
    /// This implementation addresses real-world challenges in medical record template processing
    /// where data sources may use different key naming conventions (camelCase, PascalCase, etc.)
    /// and where type safety is important for reliable template rendering.
    /// </remarks>
    public class DictionaryUtilityService : IDictionaryUtilityService
    {
        #region public methods

        /**************************************************************/
        /// <summary>
        /// Safely retrieves a value from a dictionary using comprehensive flexible key matching strategies.
        /// Implements multiple fallback approaches to handle different key naming conventions commonly
        /// found in medical record data sources and template systems.
        /// </summary>
        /// <param name="dictionary">The dictionary to search for the specified key</param>
        /// <param name="key">The key to search for using multiple matching strategies</param>
        /// <returns>The value associated with the matched key, or null if no match is found</returns>
        /// <seealso cref="SafeGet{T}"/>
        /// <seealso cref="StringComparison.OrdinalIgnoreCase"/>
        /// <example>
        /// <code>
        /// var patientData = new Dictionary&lt;string, object?&gt;
        /// {
        ///     { "PatientName", "Jane Smith" },
        ///     { "dateOfBirth", "1980-01-01" },
        ///     { "MEDICAL_ID", "12345" }
        /// };
        /// 
        /// var name1 = service.SafeGet(patientData, "PatientName");    // Exact match
        /// var name2 = service.SafeGet(patientData, "patientName");    // PascalCase conversion
        /// var dob1 = service.SafeGet(patientData, "DateOfBirth");     // camelCase conversion  
        /// var dob2 = service.SafeGet(patientData, "DATEOFBIRTH");     // Case-insensitive match
        /// var id = service.SafeGet(patientData, "medical_id");        // Case-insensitive match
        /// </code>
        /// </example>
        /// <remarks>
        /// The method implements a four-tier matching strategy:
        /// 1. Exact key match for optimal performance
        /// 2. PascalCase conversion (first letter uppercase)
        /// 3. camelCase conversion (first letter lowercase)  
        /// 4. Case-insensitive search as final fallback
        /// This approach handles most common key naming variations in medical record systems.
        /// </remarks>
        public object? SafeGet(IDictionary<string, object?> dictionary, string key)
        {
            #region implementation

            // Return null immediately for invalid input to avoid exceptions
            if (dictionary == null || string.IsNullOrEmpty(key))
                return null;

            // Strategy 1: Try exact match first for optimal performance
            if (dictionary.ContainsKey(key))
                return dictionary[key];

            // Strategy 2: Try PascalCase version (first letter uppercase)
            var pascalKey = char.ToUpper(key[0]) + key.Substring(1);
            if (dictionary.ContainsKey(pascalKey))
                return dictionary[pascalKey];

            // Strategy 3: Try camelCase version (first letter lowercase)
            var camelKey = char.ToLower(key[0]) + key.Substring(1);
            if (dictionary.ContainsKey(camelKey))
                return dictionary[camelKey];

            // Strategy 4: Try case-insensitive search as final fallback
            var foundKey = dictionary.Keys.FirstOrDefault(k =>
                string.Equals(k, key, StringComparison.OrdinalIgnoreCase));

            return foundKey != null ? dictionary[foundKey] : null;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Safely retrieves a typed value from a dictionary with flexible key matching and robust type conversion.
        /// Combines the flexible key matching capabilities with automatic type conversion to provide
        /// strongly-typed access to dictionary values with comprehensive error handling.
        /// </summary>
        /// <typeparam name="T">The expected return type for the value</typeparam>
        /// <param name="dictionary">The dictionary to search for the specified key</param>
        /// <param name="key">The key to search for using flexible matching strategies</param>
        /// <returns>The value converted to the specified type, or default(T) if not found or conversion fails</returns>
        /// <seealso cref="SafeGet(IDictionary{string, object?}, string)"/>
        /// <example>
        /// <code>
        /// var medicalData = new Dictionary&lt;string, object?&gt;
        /// {
        ///     { "PatientAge", "45" },        // String that can convert to int
        ///     { "Temperature", 98.6 },       // Double value
        ///     { "IsActive", "true" },        // String that can convert to bool
        ///     { "Notes", null }              // Null value
        /// };
        /// 
        /// var age = service.SafeGet&lt;int&gt;(medicalData, "patientAge");      // Returns 45
        /// var temp = service.SafeGet&lt;double&gt;(medicalData, "Temperature");  // Returns 98.6
        /// var active = service.SafeGet&lt;bool&gt;(medicalData, "IsActive");    // Returns true
        /// var notes = service.SafeGet&lt;string&gt;(medicalData, "Notes");       // Returns null
        /// var missing = service.SafeGet&lt;int&gt;(medicalData, "Missing");     // Returns 0 (default)
        /// </code>
        /// </example>
        /// <remarks>
        /// This method first uses SafeGet to locate the value with flexible key matching,
        /// then attempts type conversion using multiple strategies:
        /// 1. Direct cast if the value is already the target type
        /// 2. Convert.ChangeType for compatible type conversions
        /// 3. Returns default(T) if conversion fails or value is not found
        /// The robust error handling ensures no exceptions are thrown during conversion attempts.
        /// </remarks>
        public T? SafeGet<T>(IDictionary<string, object?> dictionary, string key)
        {
            #region implementation

            // Use flexible key matching to retrieve the value
            var value = SafeGet(dictionary, key);

            // If value is already the correct type, return it directly for optimal performance
            if (value is T typedValue)
                return typedValue;

            // Attempt type conversion with comprehensive error handling
            try
            {
                return (T?)Convert.ChangeType(value, typeof(T));
            }
            catch
            {
                // Return default value if conversion fails - prevents exceptions in calling code
                return default(T);
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Generates a formatted string containing all available keys in a dictionary for diagnostic and debugging purposes.
        /// Provides a convenient inspection method for understanding dictionary contents during development,
        /// template troubleshooting, and data validation scenarios.
        /// </summary>
        /// <param name="dictionary">The dictionary to list keys for, or null</param>
        /// <returns>Comma-separated list of keys in alphabetical order, or "null" if dictionary is null</returns>
        /// <seealso cref="SafeGet"/>
        /// <example>
        /// <code>
        /// var patientData = new Dictionary&lt;string, object&gt;
        /// {
        ///     { "PatientName", "John Doe" },
        ///     { "Age", 30 },
        ///     { "BloodType", "O+" },
        ///     { "Allergies", new[] { "Peanuts", "Shellfish" } }
        /// };
        /// 
        /// var availableKeys = service.GetAvailableKeys(patientData);
        /// Console.WriteLine($"Patient data contains: {availableKeys}");
        /// // Output: "Patient data contains: Age, Allergies, BloodType, PatientName"
        /// 
        /// var emptyDict = service.GetAvailableKeys(null);
        /// Console.WriteLine($"Empty dictionary: {emptyDict}");
        /// // Output: "Empty dictionary: null"
        /// </code>
        /// </example>
        /// <remarks>
        /// This method is particularly useful during template development and debugging
        /// when you need to understand what data is available in dynamic dictionaries.
        /// The alphabetical sorting provides consistent output for reliable diagnostics.
        /// </remarks>
        public string GetAvailableKeys(IDictionary<string, object>? dictionary)
        {
            #region implementation

            // Handle null dictionary case with explicit "null" indicator
            if (dictionary == null)
                return "null";

            // Return alphabetically sorted, comma-separated list of keys for consistent diagnostic output
            return string.Join(", ", dictionary.Keys.OrderBy(k => k));

            #endregion
        }

        #endregion
    }
}