using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml;
using Microsoft.Extensions.Logging;

namespace MedRecPro.Helpers
{
    #region Documentation DTOs

    /**************************************************************/
    /// <summary>
    /// Represents documentation for a class.
    /// </summary>
    /// <remarks>
    /// This DTO is used to encapsulate class-level documentation including the class summary
    /// and details about its public instance properties for documentation generation purposes.
    /// </remarks>
    public class ClassDocumentation
    {
        /// <summary>
        /// The simple name of the class.
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        /// The full name of the class, including namespace and any outer types.
        /// </summary>
        public string? FullName { get; set; }

        /// <summary>
        /// The XML documentation summary for the class.
        /// </summary>
        public string? Summary { get; set; }

        /// <summary>
        /// A list of documentation for the class's public instance properties.
        /// </summary>
        public List<PropertyDocumentation> Properties { get; set; } = new List<PropertyDocumentation>();
    }

    /**************************************************************/
    /// <summary>
    /// Represents documentation for a property.
    /// </summary>
    /// <remarks>
    /// This DTO encapsulates property-level documentation including type information,
    /// nullability status, and XML documentation summary for documentation generation.
    /// </remarks>
    public class PropertyDocumentation
    {
        /// <summary>
        /// The name of the property.
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        /// The full name of the property's type.
        /// </summary>
        public string? TypeName { get; set; }

        /// <summary>
        /// The XML documentation summary for the property.
        /// </summary>
        public string? Summary { get; set; }

        /// <summary>
        /// Indicates if the property is nullable (e.g., int?, string?, Guid?).
        /// </summary>
        public bool IsNullable { get; set; }
    }

    #endregion

    /**************************************************************/
    /// <summary>
    /// Provides static extension methods and utilities for transforming entity objects into DTOs,
    /// generating entity menus from nested types, and retrieving class documentation from XML files.
    /// </summary>
    /// <remarks>
    /// This class uses reflection extensively to analyze entity types and their properties.
    /// It also provides caching mechanisms for XML documentation files to improve performance.
    /// All transformation methods are designed to handle null inputs gracefully and provide detailed logging.
    /// </remarks>
    public static class DtoTransformer
    {
        #region implementation

        /// <summary>
        /// Cache for loaded XML documentation files to avoid repeated file IO and parsing operations
        /// </summary>
        private static readonly Dictionary<Assembly, XmlDocument?> _xmlDocsCache = new Dictionary<Assembly, XmlDocument?>();

        /// <summary>
        /// Lock object for thread-safe access to the XML documentation cache
        /// </summary>
        private static readonly object _xmlDocsCacheLock = new object();

        #endregion

        /**************************************************************/
        /// <summary>
        /// Transforms an entity object into a dictionary, replacing its primary key
        /// with an encrypted version. It assumes the primary key property 
        /// follows the convention 'ClassNameID', 'ClassNameId', or 'Id'.
        /// The original PK property is omitted from the output dictionary.
        /// </summary>
        /// <param name="entity">The entity object to transform. Can be null.</param>
        /// <param name="pkEncryptionSecret">The secret key used for encrypting the primary key.</param>
        /// <param name="logger">Logger for recording any issues during transformation.</param>
        /// <returns>
        /// A dictionary representing the transformed entity with an encrypted PK.
        /// Returns an empty dictionary if the input entity is null.
        /// The encrypted PK will be named 'Encrypted' + OriginalPKName (e.g., 'EncryptedDocumentID').
        /// </returns>
        /// <example>
        /// <code>
        /// var user = new User { UserID = 123, Name = "John Doe" };
        /// var result = user.ToEntityWithEncryptedId("mySecret", logger);
        /// // Result: { "EncryptedUserID": "encrypted_string", "Name": "John Doe" }
        /// </code>
        /// </example>
        /// <remarks>
        /// This method uses reflection to find the primary key property based on naming conventions.
        /// If encryption fails, the encrypted field will not be added to the result dictionary.
        /// All other public readable properties are copied to the output dictionary unchanged.
        /// </remarks>
        public static Dictionary<string, object?> ToEntityWithEncryptedId(
            this object? entity,
            string pkEncryptionSecret,
            ILogger logger)
        {
            #region implementation

            // Handle null entity input gracefully
            if (entity == null)
            {
                if (logger != null)
                    logger.LogTrace("Attempted to transform a null entity. Returning empty dictionary.");

                // Return an empty dictionary if the entity is null
                return new Dictionary<string, object?>();
            }

            // Initialize result dictionary for transformed entity
            var dto = new Dictionary<string, object?>();

            // Get the type information for reflection operations
            var entityType = entity.GetType();

            PropertyInfo? pkProperty = null;

            #region primary key property discovery

            // Convention 1: ClassNameID (e.g., DocumentID for Label.Document)
            string pkNameConvention1 = entityType.Name + "ID";
            pkProperty = entityType.GetProperty(pkNameConvention1, BindingFlags.Public | BindingFlags.Instance);

            // Convention 2: ClassNameId (e.g., DocumentId for Label.Document)
            if (pkProperty == null)
            {
                string pkNameConvention2 = entityType.Name + "Id";
                pkProperty = entityType.GetProperty(pkNameConvention2, BindingFlags.Public | BindingFlags.Instance);
            }

            // Convention 3: Id (case-insensitive, common for Identity entities)
            if (pkProperty == null)
            {
                pkProperty = entityType.GetProperty("Id", BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            }

            #endregion

            #region primary key encryption processing

            if (pkProperty != null)
            {
                // Extract the primary key value from the entity
                object? pkValue = pkProperty.GetValue(entity);

                // Create the encrypted field name by prefixing with "Encrypted"
                string encryptedPkFieldName = "Encrypted" + pkProperty.Name;

                if (pkValue != null)
                {
                    try
                    {
                        // StringCipher.Encrypt is static as per your provided StringCipher class
                        string encryptedPkString = StringCipher.Encrypt(pkValue.ToString()!, pkEncryptionSecret);

                        // Add the encrypted PK to the DTO
                        dto[encryptedPkFieldName] = encryptedPkString;
                    }
                    catch (Exception ex)
                    {
                        if (logger == null)
                        {
                            // If logger is not provided, we cannot log the error.
                            throw new InvalidOperationException("Encryption failed but no logger was provided to log the error.", ex);
                        }

                        // Encrypted field is not added if encryption fails.
                        logger.LogError(ex, "Failed to encrypt PK for entity type {EntityType}, PK property {PKProperty}. PK Value: {PKValue}. Field '{EncryptedPKField}' will not be added.",
                                       entityType.FullName, pkProperty.Name, pkValue.ToString(), encryptedPkFieldName);
                    }
                }
                else
                {
                    // PK value is null, so encrypted PK is also null.
                    dto[encryptedPkFieldName] = null;

                    if (logger != null)
                        // Log trace when PK is null
                        logger.LogTrace("PK {PKProperty} for entity type {EntityType} is null. Field '{EncryptedPKField}' will be null.",
                                   pkProperty.Name, entityType.FullName, encryptedPkFieldName);
                }
            }
            else
            {
                if (logger != null)
                    // Log warning when no primary key property is found using conventions
                    logger.LogWarning("Could not find PK property for type {EntityType} using conventions ('{Convention1}', '{Convention2}', 'Id'). No encrypted ID field will be added.",
                                     entityType.FullName, entityType.Name + "ID", entityType.Name + "Id");
            }

            #endregion

            #region copy remaining properties

            // Copy all other readable public instance properties
            foreach (var prop in entityType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                // Skip properties that cannot be read
                if (!prop.CanRead) continue;

                // Skip the original PK property as it's being replaced/masked
                if (pkProperty != null && prop.Name == pkProperty.Name)
                {
                    continue;
                }

                // Copy the property value to the result dictionary
                dto[prop.Name] = prop.GetValue(entity);
            }

            #endregion

            return dto;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Transforms an entity object into a sorted list of its nested public class names.
        /// This method is specifically designed to work with entity types that contain nested classes
        /// representing different data sections or tables.
        /// </summary>
        /// <param name="entity">The entity object to inspect for nested types. Can be null.</param>
        /// <param name="logger">Optional logger for tracing and error reporting.</param>
        /// <returns>A sorted list of nested class names, or an empty list if no nested classes are found or entity is null.</returns>
        /// <example>
        /// <code>
        /// var label = new Label(); // Has nested classes like Document, Organization, etc.
        /// var menu = label.ToEntityMenu(logger);
        /// // Returns: ["Address", "Document", "Organization", ...]
        /// </code>
        /// </example>
        /// <remarks>
        /// This method uses reflection to inspect the entity's public nested types (classes).
        /// Only public nested classes are included in the result.
        /// The returned list is sorted alphabetically for consistent ordering.
        /// Primarily used for generating dynamic menu options for API endpoints.
        /// </remarks>
        public static List<string> ToEntityMenu(this object? entity, ILogger logger)
        {
            #region implementation

            // Initialize empty menu list to store nested class names
            var menu = new List<string>();

            // Early return if entity is null with optional logging
            if (entity == null && logger != null)
            {
                logger.LogTrace("Attempted to transform a null entity into a menu. Returning empty list.");
                return menu;
            }
            if (entity == null) return menu;

            try
            {
                #region reflection setup
                // Get the runtime type of the entity for reflection
                var entityType = entity.GetType();
                #endregion

                #region nested type enumeration

                // Iterate through all public nested types (classes) using reflection
                foreach (var nestedType in entityType.GetNestedTypes(BindingFlags.Public))
                {
                    // Validate nested type is a class with a valid name
                    if (nestedType != null && nestedType.IsClass && !string.IsNullOrWhiteSpace(nestedType.Name))
                    {
                        // Add the nested type name to our menu collection
                        menu.Add(nestedType.Name);
                    }
                }

                #endregion

                #region result processing
                // Sort the collected nested class names alphabetically for consistent output
                if (menu.Any())
                {
                    menu.Sort();
                }
                else if (logger != null)
                {
                    // Log warning if no nested classes were discovered
                    logger.LogWarning("No public nested classes found for entity type {EntityType}. Menu will be empty.", entityType.FullName);
                }
                #endregion
            }
            catch (Exception e)
            {
                // Log the error with context and rethrow to maintain original behavior
                if (logger != null)
                    logger.LogError(e, "Error in DtoTransformer.ToEntityMenu for type {EntityType}", entity?.GetType().FullName);

                // Preserve original exception handling by rethrowing
                throw;
            }

            return menu;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Loads XML documentation for the specified assembly from the application's base directory.
        /// Results are cached to avoid repeated file I/O operations for the same assembly.
        /// </summary>
        /// <param name="assembly">The assembly for which to load XML documentation.</param>
        /// <param name="logger">Logger for recording load attempts and failures.</param>
        /// <returns>XmlDocument containing the documentation, or null if not found or failed to load.</returns>
        /// <remarks>
        /// This method expects XML documentation files to be named {AssemblyName}.xml and located
        /// in the application's base directory. Results are cached in memory for performance.
        /// Thread-safe implementation using lock for cache access.
        /// </remarks>
        private static XmlDocument? loadXmlDocumentation(Assembly assembly, ILogger logger)
        {
            #region implementation

            // Thread-safe cache access
            lock (_xmlDocsCacheLock)
            {
                // Check if documentation is already cached for this assembly
                if (_xmlDocsCache.TryGetValue(assembly, out var cachedDoc))
                {
                    return cachedDoc; // Return cached doc (could be null if previously not found)
                }

                // Get assembly name for constructing XML file path
                string assemblyName = assembly.GetName().Name ?? string.Empty;
                if (string.IsNullOrEmpty(assemblyName))
                {
                    logger.LogWarning("Assembly name is null or empty, cannot load XML documentation.");
                    _xmlDocsCache[assembly] = null;
                    return null;
                }

                // Construct expected XML documentation file path
                string xmlFilePath = Path.Combine(AppContext.BaseDirectory, $"{assemblyName}.xml");

                if (File.Exists(xmlFilePath))
                {
                    try
                    {
                        // Load and parse the XML documentation file
                        var xmlDoc = new XmlDocument();
                        xmlDoc.Load(xmlFilePath);
                        _xmlDocsCache[assembly] = xmlDoc;
                        logger.LogTrace("Successfully loaded XML documentation from {XmlFilePath}", xmlFilePath);
                        return xmlDoc;
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Failed to load XML documentation from {XmlFilePath}", xmlFilePath);
                        _xmlDocsCache[assembly] = null; // Cache null to avoid retrying on failure
                        return null;
                    }
                }
                else
                {
                    logger.LogTrace("XML documentation file not found at {XmlFilePath}", xmlFilePath);
                    _xmlDocsCache[assembly] = null; // Cache null as not found
                    return null;
                }
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Retrieves the XML documentation summary for a specific member (class, property, method, etc.)
        /// from the loaded XML documentation file.
        /// </summary>
        /// <param name="memberNamePrefix">The member type prefix (T for types, P for properties, M for methods, etc.).</param>
        /// <param name="elementFullName">The full name of the element including namespace and type.</param>
        /// <param name="assembly">The assembly containing the member.</param>
        /// <param name="logger">Logger for recording lookup attempts and failures.</param>
        /// <returns>The summary text from XML documentation, or null if not found or failed to parse.</returns>
        /// <remarks>
        /// Uses XPath queries to locate specific member documentation within the XML file.
        /// Handles XPath exceptions gracefully and returns null for missing documentation.
        /// Member names follow .NET XML documentation conventions (e.g., "T:Namespace.ClassName").
        /// </remarks>
        private static string? getXmlSummary(string memberNamePrefix, string elementFullName, Assembly assembly, ILogger logger)
        {
            #region implementation

            // Load the XML documentation for this assembly
            var xmlDoc = loadXmlDocumentation(assembly, logger);
            if (xmlDoc == null)
            {
                return null;
            }

            // Construct full member name for XML lookup, e.g., "T:Namespace.ClassName" or "P:Namespace.ClassName.PropertyName"
            string fullXmlMemberName = $"{memberNamePrefix}:{elementFullName}";

            try
            {
                // Use XPath to find the summary node for this specific member
                XmlNode? summaryNode = xmlDoc.SelectSingleNode($"//member[@name='{fullXmlMemberName}']/summary");
                return summaryNode?.InnerText.Trim();
            }
            catch (System.Xml.XPath.XPathException ex)
            {
                logger.LogWarning(ex, "XPathException while trying to get summary for {FullXmlMemberName} in assembly {AssemblyName}.", fullXmlMemberName, assembly.GetName().Name);
                return null;
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Determines whether a property is nullable based on its type information.
        /// Handles both reference types (inherently nullable) and Nullable&lt;T&gt; value types.
        /// </summary>
        /// <param name="property">The PropertyInfo object to analyze for nullability.</param>
        /// <returns>True if the property can hold null values, false otherwise.</returns>
        /// <remarks>
        /// Reference types (classes, interfaces, delegates, strings) are considered nullable.
        /// Value types are nullable only if they are Nullable&lt;T&gt; (e.g., int?, DateTime?).
        /// This method does not detect C# 8.0+ nullable reference type annotations, which would
        /// require more complex reflection involving NullabilityInfoContext (.NET 5.0+).
        /// </remarks>
        private static bool isPropertyNullable(PropertyInfo property)
        {
            #region implementation

            // Check for reference types (class, interface, delegate, string) which are inherently nullable
            if (!property.PropertyType.IsValueType || property.PropertyType == typeof(string))
            {
                return true;
            }

            // Check for Nullable<T> value types (e.g., int?, DateTime?)
            if (Nullable.GetUnderlyingType(property.PropertyType) != null)
            {
                return true;
            }

            // For C# 8.0+ nullable reference types, this requires deeper inspection of attributes
            // which is complex. For simplicity, this basic check covers value types and string.
            // A more robust check would involve NullabilityInfoContext (net5.0+).
            // For now, we'll assume non-Nullable<T> value types are not nullable in the C# sense
            // unless C# 8 NRT attributes say otherwise, which we aren't checking here.
            return false;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Retrieves comprehensive documentation for a given class type, including its summary and
        /// details for its public instance properties (name, type, summary, nullability).
        /// Relies on the presence of an XML documentation file (e.g., AssemblyName.xml)
        /// in the application's base directory.
        /// </summary>
        /// <param name="type">The class Type object to document.</param>
        /// <param name="logger">Logger for recording any issues during documentation retrieval.</param>
        /// <returns>A ClassDocumentation object containing all discovered information, or null if the type is invalid or documentation cannot be retrieved.</returns>
        /// <example>
        /// <code>
        /// var docInfo = DtoTransformer.GetClassDocumentation(typeof(User), logger);
        /// Console.WriteLine($"Class: {docInfo.Name}");
        /// Console.WriteLine($"Summary: {docInfo.Summary}");
        /// foreach(var prop in docInfo.Properties)
        /// {
        ///     Console.WriteLine($"Property: {prop.Name} ({prop.TypeName}) - {prop.Summary}");
        /// }
        /// </code>
        /// </example>
        /// <remarks>
        /// This method combines reflection-based type analysis with XML documentation parsing.
        /// Only readable public instance properties are included in the documentation.
        /// Property type names include full namespace information for clarity.
        /// Nullability detection covers basic scenarios but may not capture all C# 8.0+ nullable reference type cases.
        /// </remarks>
        public static ClassDocumentation? GetClassDocumentation(Type type, ILogger logger)
        {
            #region implementation

            // Validate input type parameter
            if (type == null)
            {
                if (logger != null)
                    logger.LogWarning("GetClassDocumentation called with a null type.");
                return null;
            }

            // Create the class documentation object with basic type information
            var classDoc = new ClassDocumentation
            {
                Name = type.Name,
                FullName = type.FullName, // FullName for nested types is like Namespace.Outer+Inner
                Summary = getXmlSummary("T", type.FullName!, type.Assembly, logger)
            };

            #region property documentation collection

            // Get all public instance properties using reflection
            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (var prop in properties)
            {
                // Skip write-only properties as they cannot be read
                if (!prop.CanRead) continue;

                // For property summary, member name is P:Namespace.Type.PropertyName
                // Type.FullName already includes Outer+Nested for nested types.
                string propertyXmlFullName = $"{type.FullName}.{prop.Name}";

                // Create property documentation with all available information
                classDoc.Properties.Add(new PropertyDocumentation
                {
                    Name = prop.Name,
                    TypeName = prop.PropertyType.ToString(), // Gives full type name like System.String, System.Nullable`1[[System.Int32...]]
                    IsNullable = isPropertyNullable(prop),
                    Summary = getXmlSummary("P", propertyXmlFullName, type.Assembly, logger)
                });
            }

            #endregion

            return classDoc;

            #endregion
        }
    }
}