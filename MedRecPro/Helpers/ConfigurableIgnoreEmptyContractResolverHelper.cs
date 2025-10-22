using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System.Collections;
using System.Reflection;

namespace MedRecPro.Helpers
{
    /**************************************************************/
    /// <summary>
    /// Configurable contract resolver that can ignore empty collections and/or empty objects during JSON serialization.
    /// This helper extends CamelCasePropertyNamesContractResolver to provide flexible control over which properties
    /// are included in the serialized output based on their content.
    /// </summary>
    /// <remarks>
    /// This class is particularly useful for reducing JSON payload size by excluding properties that contain
    /// no meaningful data, such as empty arrays or objects with all null properties.
    /// </remarks>
    /// <example>
    /// <code>
    /// // Create a serializer that ignores empty collections
    /// var settings = new JsonSerializerSettings
    /// {
    ///     ContractResolver = ConfigurableIgnoreEmptyContractHelper.IgnoreEmptyCollections
    /// };
    /// 
    /// // Create a serializer that ignores both empty collections and empty objects
    /// var settings2 = new JsonSerializerSettings
    /// {
    ///     ContractResolver = ConfigurableIgnoreEmptyContractHelper.IgnoreEmptyCollectionsAndObjects
    /// };
    /// </code>
    /// </example>
    /// <seealso cref="CamelCasePropertyNamesContractResolver"/>
    /// <seealso cref="JsonProperty"/>
    /// <seealso cref="IEnumerable"/>
    public class ConfigurableIgnoreEmptyContractHelper : CamelCasePropertyNamesContractResolver
    {
        #region static instances
        /**************************************************************/
        /// <summary>
        /// Gets a static instance configured to ignore empty collections only.
        /// This instance will exclude properties that are empty arrays, lists, or other enumerable collections.
        /// </summary>
        /// <seealso cref="ConfigurableIgnoreEmptyContractHelper"/>
        /// <seealso cref="IEnumerable"/>
        public static readonly ConfigurableIgnoreEmptyContractHelper IgnoreEmptyCollections = new(true, false);

        /**************************************************************/
        /// <summary>
        /// Gets a static instance configured to ignore both empty collections and empty objects.
        /// This instance will exclude properties that are empty collections or objects with all null/empty properties.
        /// </summary>
        /// <seealso cref="ConfigurableIgnoreEmptyContractHelper"/>
        /// <seealso cref="IEnumerable"/>
        public static readonly ConfigurableIgnoreEmptyContractHelper IgnoreEmptyCollectionsAndObjects = new(true, true);
        #endregion

        #region private fields
        /// <summary>
        /// Determines whether empty collections should be ignored during serialization.
        /// </summary>
        private readonly bool _ignoreEmptyCollections;

        /// <summary>
        /// Determines whether empty objects should be ignored during serialization.
        /// </summary>
        private readonly bool _ignoreEmptyObjects;
        #endregion

        #region constructor
        /**************************************************************/
        /// <summary>
        /// Initializes a new instance of the ConfigurableIgnoreEmptyContractHelper with specified options.
        /// </summary>
        /// <param name="ignoreEmptyCollections">Whether to ignore empty collections during serialization</param>
        /// <param name="ignoreEmptyObjects">Whether to ignore empty objects during serialization</param>
        /// <remarks>
        /// By default, only empty collections are ignored. Set ignoreEmptyObjects to true if you also want
        /// to exclude objects that have all null or empty property values.
        /// </remarks>
        /// <seealso cref="CamelCasePropertyNamesContractResolver"/>
        public ConfigurableIgnoreEmptyContractHelper(bool ignoreEmptyCollections = true, bool ignoreEmptyObjects = false)
        {
            #region implementation
            // Store configuration flags for use during property creation
            _ignoreEmptyCollections = ignoreEmptyCollections;
            _ignoreEmptyObjects = ignoreEmptyObjects;
            #endregion
        }
        #endregion

        #region protected methods
        /**************************************************************/
        /// <summary>
        /// Creates a JsonProperty for the specified member with custom serialization logic applied.
        /// This method determines whether properties should be serialized based on the configured options.
        /// </summary>
        /// <param name="member">The member information for the property being created</param>
        /// <param name="memberSerialization">The serialization mode for the member</param>
        /// <returns>A configured JsonProperty with appropriate ShouldSerialize logic</returns>
        /// <remarks>
        /// This method applies different serialization logic based on the property type:
        /// - For collections: Uses shouldSerializeCollection to check if the collection has items
        /// - For complex objects: Uses shouldSerializeObject to check if the object has meaningful content
        /// </remarks>
        /// <seealso cref="JsonProperty"/>
        /// <seealso cref="MemberInfo"/>
        /// <seealso cref="IEnumerable"/>
        protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
        {
            #region implementation
            // Create the base property using the parent resolver
            var property = base.CreateProperty(member, memberSerialization);

            // Handle collections (arrays, lists, etc.) but exclude strings
            if (_ignoreEmptyCollections &&
                property.PropertyType != typeof(string) &&
                typeof(IEnumerable).IsAssignableFrom(property.PropertyType))
            {
                // Set custom serialization logic for collections
                property.ShouldSerialize = instance => shouldSerializeCollection(property, instance);
            }
            // Handle complex objects (classes but not primitives or strings)
            else if (_ignoreEmptyObjects
                    && property != null
                    && property.PropertyType != null
                    && property.PropertyType.IsClass
                    && property.PropertyType != typeof(string)
                    && !property.PropertyType.IsPrimitive)
            {
                // Set custom serialization logic for objects
                property.ShouldSerialize = instance => shouldSerializeObject(property, instance);
            }

            // Return the configured property or a new empty one if null
            return property ?? new JsonProperty();
            #endregion
        }
        #endregion

        #region private methods
        /**************************************************************/
        /// <summary>
        /// Determines whether a collection property should be serialized based on its content.
        /// Returns false for null collections or collections with zero items.
        /// </summary>
        /// <param name="property">The JsonProperty representing the collection</param>
        /// <param name="instance">The object instance containing the property</param>
        /// <returns>True if the collection has items and should be serialized; otherwise, false</returns>
        /// <remarks>
        /// This method handles both ICollection types (for efficient Count property access) and
        /// general IEnumerable types (using LINQ Any() for checking contents).
        /// </remarks>
        /// <seealso cref="JsonProperty"/>
        /// <seealso cref="ICollection"/>
        /// <seealso cref="IEnumerable"/>
        private static bool shouldSerializeCollection(JsonProperty property, object instance)
        {
            #region implementation
            try
            {
                // Get the actual value of the property from the instance
                var value = property.ValueProvider?.GetValue(instance);

                // Null collections should not be serialized
                if (value == null) return false;

                // For ICollection types, use Count property for efficiency
                if (value is ICollection collection)
                    return collection.Count > 0;

                // For other IEnumerable types, check if any items exist
                if (value is IEnumerable enumerable)
                    return enumerable.Cast<object>().Any();
            }
            catch
            {
                // On any error, default to serializing the property
                return true;
            }

            // Default to serializing if we can't determine emptiness
            return true;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Determines whether an object property should be serialized based on its content.
        /// Returns false for null objects or objects where all properties are null or empty.
        /// </summary>
        /// <param name="property">The JsonProperty representing the object</param>
        /// <param name="instance">The object instance containing the property</param>
        /// <returns>True if the object has meaningful content and should be serialized; otherwise, false</returns>
        /// <remarks>
        /// This method performs a simple check by examining all public instance properties of the object.
        /// An object is considered "empty" if all its properties are null or empty collections.
        /// </remarks>
        /// <seealso cref="JsonProperty"/>
        /// <seealso cref="PropertyInfo"/>
        /// <seealso cref="BindingFlags"/>
        private static bool shouldSerializeObject(JsonProperty property, object instance)
        {
            #region implementation
            try
            {
                // Get the actual value of the property from the instance
                var value = property.ValueProvider?.GetValue(instance);

                // Null objects should not be serialized
                if (value == null) return false;

                // Get all public instance properties of the object
                var properties = value.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);

                // Check if any property has meaningful (non-null, non-empty) content
                return properties.Any(prop =>
                {
                    var propValue = prop.GetValue(value);
                    // Property has meaningful content if it's not null and (if it's a collection) not empty
                    return propValue != null && (!(propValue is ICollection coll) || coll.Count > 0);
                });
            }
            catch
            {
                // On any error, default to serializing the property
                return true;
            }
            #endregion
        }
        #endregion
    }
}