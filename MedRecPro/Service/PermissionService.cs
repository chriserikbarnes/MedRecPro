using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using MedRecPro.Helpers; // For StringCipher
using MedRecPro.Models; // For Permission, ActorType, PermissionType
using static MedRecPro.Models.Constant;
using MedRecPro.Service;


namespace MedRecPro.Service
{
    /**************************************************************/
    /// <summary>
    /// Implementation of <see cref="IPermissionService"/> that provides permission management,
    /// validation, serialization, encryption, and authorization support.
    /// </summary>
    /// <remarks>
    /// This service is registered as scoped in the DI container and provides:
    /// <list type="bullet">
    ///     <item>Permission CRUD operations (Append, Remove, Update, Clone)</item>
    ///     <item>Permission validation (HasPermission, HasAnyActorType, ValidateUserRole)</item>
    ///     <item>Serialization/Deserialization (ToJson, FromJson)</item>
    ///     <item>Encryption/Decryption for secure storage</item>
    /// </list>
    /// 
    /// The service requires an encryption key configured in <c>Security:DB:PKSecret</c>.
    /// </remarks>
    /// <seealso cref="IPermissionService"/>
    /// <seealso cref="Permission"/>
    /// <seealso cref="MedRecPro.Filters.RequireUserRoleAttribute"/>
    /// <seealso cref="MedRecPro.Filters.RequireActorAttribute"/>
    public class PermissionService : IPermissionService
    {
        #region Private Fields

        private readonly IConfiguration _configuration;
        private readonly ILogger<PermissionService> _logger; // Typed logger
        private readonly StringCipher _stringCipher;
        private readonly string _encryptionKey;

        #endregion

        #region Constructor

        /**************************************************************/
        /// <summary>
        /// Initializes a new instance of the <see cref="PermissionService"/> class.
        /// </summary>
        /// <param name="configuration">The application configuration containing encryption settings.</param>
        /// <param name="logger">The logger for diagnostic output.</param>
        /// <param name="stringCipher">The encryption service for permission data.</param>
        /// <exception cref="ArgumentNullException">Thrown when any required dependency is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the encryption key is not configured.</exception>
        public PermissionService(
            IConfiguration configuration,
            ILogger<PermissionService> logger,
            StringCipher stringCipher) // Inject StringCipher
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _stringCipher = stringCipher ?? throw new ArgumentNullException(nameof(stringCipher));

            _encryptionKey = _configuration["Security:DB:PKSecret"] ?? string.Empty;

            if (string.IsNullOrWhiteSpace(_encryptionKey))
            {
                _logger.LogError("Encryption key (Security:DB:PKSecret) is missing or empty in configuration.");
                throw new InvalidOperationException("Encryption key (Security:DB:PKSecret) is missing or empty in configuration.");
            }
        }

        #endregion

        #region Permission Validation Methods

        /**************************************************************/
        /// <summary>
        /// Checks if a given permission exists in the list.
        /// </summary>
        /// <param name="permissions">The list of permissions to search.</param>
        /// <param name="actor">The actor type to match.</param>
        /// <param name="resource">The resource identifier to match.</param>
        /// <param name="type">The permission type to match.</param>
        /// <param name="maskedPII">Whether PII should be masked. Defaults to true.</param>
        /// <returns>True if the permission exists or if actor is SystemAdmin; otherwise, false.</returns>
        /// <seealso cref="ActorType"/>
        /// <seealso cref="PermissionType"/>
        public bool HasPermission(
            List<Permission> permissions,
            ActorType actor,
            string resource,
            PermissionType type,
            bool maskedPII = true)
        {
            #region implementation
            if (permissions == null || !permissions.Any() || string.IsNullOrWhiteSpace(resource))
                return false;

            // SystemAdmin has all permissions
            if (actor == ActorType.SystemAdmin)
                return true;

            return permissions.Any(p =>
                p.Actor == actor &&
                p.Resource == resource &&
                p.Type == type &&
                p.MaskedPII == maskedPII
            );
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Checks if any of the specified actor types exist in the permissions list.
        /// </summary>
        /// <param name="permissions">The list of permissions to search.</param>
        /// <param name="actorTypes">The actor types to check for (OR logic).</param>
        /// <returns>True if any matching actor type is found; otherwise, false.</returns>
        /// <remarks>
        /// If the user has <see cref="ActorType.SystemAdmin"/> in their permissions,
        /// this method returns true regardless of the specified actor types.
        /// </remarks>
        /// <example>
        /// <code>
        /// bool hasAccess = _permissionService.HasAnyActorType(
        ///     userPermissions,
        ///     ActorType.LabelAdmin,
        ///     ActorType.LabelManager);
        /// </code>
        /// </example>
        /// <seealso cref="ActorType"/>
        /// <seealso cref="MedRecPro.Filters.RequireActorAttribute"/>
        public bool HasAnyActorType(List<Permission> permissions, params ActorType[] actorTypes)
        {
            #region implementation
            if (permissions == null || !permissions.Any())
            {
                _logger.LogDebug("HasAnyActorType: Permissions list is null or empty.");
                return false;
            }

            if (actorTypes == null || actorTypes.Length == 0)
            {
                _logger.LogDebug("HasAnyActorType: No actor types specified.");
                return false;
            }

            // Check if user has SystemAdmin - bypass all checks
            if (permissions.Any(p => p.Actor == ActorType.SystemAdmin))
            {
                _logger.LogDebug("HasAnyActorType: User has SystemAdmin actor - granting access.");
                return true;
            }

            // Check if user has any of the specified actor types
            foreach (var actorType in actorTypes)
            {
                if (permissions.Any(p => p.Actor == actorType))
                {
                    _logger.LogDebug("HasAnyActorType: Found matching actor type '{ActorType}'.", actorType);
                    return true;
                }
            }

            _logger.LogDebug(
                "HasAnyActorType: No matching actor types found. Required: [{Required}]",
                string.Join(", ", actorTypes));

            return false;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Validates that a user has one of the required roles.
        /// </summary>
        /// <param name="user">The user to validate.</param>
        /// <param name="allowedRoles">The roles that are permitted (OR logic).</param>
        /// <returns>True if the user's role matches any allowed role (case-insensitive); otherwise, false.</returns>
        /// <example>
        /// <code>
        /// bool isAdmin = _permissionService.ValidateUserRole(user, UserRole.Admin, UserRole.UserAdmin);
        /// </code>
        /// </example>
        /// <seealso cref="User.UserRole"/>
        /// <seealso cref="MedRecPro.Models.UserRole"/>
        /// <seealso cref="MedRecPro.Filters.RequireUserRoleAttribute"/>
        public bool ValidateUserRole(User user, params string[] allowedRoles)
        {
            #region implementation
            if (user == null)
            {
                _logger.LogDebug("ValidateUserRole: User is null.");
                return false;
            }

            if (string.IsNullOrEmpty(user.UserRole))
            {
                _logger.LogDebug("ValidateUserRole: User has no role assigned.");
                return false;
            }

            if (allowedRoles == null || allowedRoles.Length == 0)
            {
                _logger.LogDebug("ValidateUserRole: No allowed roles specified.");
                return false;
            }

            // Case-insensitive comparison of user role against allowed roles
            bool isValid = allowedRoles.Any(role =>
                !string.IsNullOrEmpty(role) &&
                user.UserRole.Equals(role, StringComparison.OrdinalIgnoreCase));

            if (isValid)
            {
                _logger.LogDebug(
                    "ValidateUserRole: User role '{UserRole}' matches allowed roles.",
                    user.UserRole);
            }
            else
            {
                _logger.LogDebug(
                    "ValidateUserRole: User role '{UserRole}' not in allowed roles [{AllowedRoles}].",
                    user.UserRole,
                    string.Join(", ", allowedRoles));
            }

            return isValid;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets all distinct actor types present in a permissions list.
        /// </summary>
        /// <param name="permissions">The list of permissions to analyze.</param>
        /// <returns>A list of distinct ActorType values found in the permissions.</returns>
        /// <seealso cref="ActorType"/>
        public List<ActorType> GetActorTypes(List<Permission> permissions)
        {
            #region implementation
            if (permissions == null || !permissions.Any())
            {
                return new List<ActorType>();
            }

            return permissions
                .Select(p => p.Actor)
                .Distinct()
                .ToList();
            #endregion
        }

        #endregion

        #region Permission CRUD Methods

        /**************************************************************/
        /// <summary>
        /// Appends a permission to a list (adds only if not duplicate).
        /// </summary>
        /// <param name="permissions">The existing permissions list.</param>
        /// <param name="toAdd">The permission to add.</param>
        /// <returns>The updated permissions list.</returns>
        public List<Permission> Append(List<Permission> permissions, Permission toAdd)
        {
            #region implementation
            if (permissions == null) permissions = new List<Permission>(); // Ensure list exists
            if (toAdd == null) return permissions;


            if (!permissions.Any(p =>
                    p.Actor == toAdd.Actor &&
                    p.Resource == toAdd.Resource &&
                    p.Type == toAdd.Type &&
                    p.MaskedPII == toAdd.MaskedPII))
            {
                permissions.Add(toAdd);
            }
            else
            {
                _logger.LogWarning("Attempted to add a duplicate permission: Actor={Actor}, Resource={Resource}, Type={Type}, MaskedPII={MaskedPII}",
                    toAdd.Actor, toAdd.Resource, toAdd.Type, toAdd.MaskedPII);
            }

            return permissions;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Removes a permission from the list (by value).
        /// </summary>
        /// <param name="permissions">The existing permissions list.</param>
        /// <param name="toRemove">The permission to remove.</param>
        /// <returns>The updated permissions list.</returns>
        public List<Permission> Remove(List<Permission> permissions, Permission toRemove)
        {
            #region implementation
            if (permissions == null || toRemove == null)
                return permissions ?? new List<Permission>();

            permissions.RemoveAll(p =>
                    p.Actor == toRemove.Actor &&
                    p.Resource == toRemove.Resource &&
                    p.Type == toRemove.Type &&
                    p.MaskedPII == toRemove.MaskedPII
                );

            return permissions;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Updates a permission in the list. If not found, adds it.
        /// </summary>
        /// <param name="permissions">The existing permissions list.</param>
        /// <param name="updated">The updated permission.</param>
        /// <returns>The updated permissions list.</returns>
        public List<Permission> Update(List<Permission> permissions, Permission updated)
        {
            #region implementation
            if (permissions == null) permissions = new List<Permission>();

            if (updated == null) return permissions;

            // Remove existing if it's there (based on matching properties)
            Remove(permissions, updated);

            // Add the new/updated one
            permissions.Add(updated);
            return permissions;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Converts the permission list to a dictionary for fast lookup by resource.
        /// </summary>
        /// <param name="permissions">The permissions list to convert.</param>
        /// <returns>A dictionary keyed by resource string.</returns>
        public Dictionary<string, List<Permission>> ToDictionary(List<Permission> permissions)
        {
            #region implementation
            if (permissions == null) return new Dictionary<string, List<Permission>>();
            return permissions
                .GroupBy(p => p.Resource ?? string.Empty)
                .ToDictionary(g => g.Key, g => g.ToList());
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Returns a deep copy of the permissions list.
        /// </summary>
        /// <param name="permissions">The permissions list to clone.</param>
        /// <returns>A new list with cloned permissions.</returns>
        public List<Permission> Clone(List<Permission> permissions)
        {
            #region implementation
            if (permissions == null) return new List<Permission>();
            var json = ToJson(permissions);
            return FromJson(json);
            #endregion
        }

        #endregion

        #region Serialization Methods

        /**************************************************************/
        /// <summary>
        /// Serializes a list of permissions to a JSON string.
        /// </summary>
        /// <param name="permissions">The permissions to serialize.</param>
        /// <returns>A JSON string representation of the permissions.</returns>
        public string ToJson(List<Permission> permissions)
        {
            #region implementation
            if (permissions == null) return "[]";

            // Log the start of serialization for debugging purposes
            _logger.LogInformation("Serializing permissions to JSON with count: {Count}", permissions.Count);

            return JsonConvert.SerializeObject(permissions);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Deserializes a JSON string to a list of permissions.
        /// </summary>
        /// <param name="json">The JSON string to deserialize.</param>
        /// <returns>A list of Permission objects.</returns>
        public List<Permission> FromJson(string json)
        {
            #region implementation
            if (string.IsNullOrWhiteSpace(json))
                return new List<Permission>();

            // Log the start of deserialization for debugging purposes
            _logger.LogInformation("Deserializing permissions from JSON: {JsonStart}", json.Substring(0, Math.Min(json.Length, 20)));

            // Use JsonConvert to deserialize the JSON string into a list of Permission objects
            return JsonConvert.DeserializeObject<List<Permission>>(json) ?? new List<Permission>();
            #endregion
        }

        #endregion

        #region Encryption Methods

        /**************************************************************/
        /// <summary>
        /// Encrypts the serialized permissions using the configured encryption key.
        /// </summary>
        /// <param name="permissions">The permissions to encrypt.</param>
        /// <returns>An encrypted string representation of the permissions.</returns>
        public string Encrypt(List<Permission> permissions)
        {
            #region implementation
            try
            {
                if (permissions == null || !permissions.Any())
                {
                    _logger.LogWarning("No permissions to encrypt.");

                    return string.Empty;
                }

                var json = ToJson(permissions);

                if (string.IsNullOrWhiteSpace(json))
                {
                    _logger.LogWarning("Serialized JSON is null or empty.");
                    return string.Empty;
                }

                _logger.LogInformation("Attempting to encrypt permissions with encryption key");

                return StringCipher.Encrypt(json, _encryptionKey, StringCipher.EncryptionStrength.Fast);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Permission encryption failed.");
                throw;
            }
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Attempts to decrypt and deserialize, returning false if unsuccessful.
        /// </summary>
        /// <param name="encrypted">The encrypted string to decrypt.</param>
        /// <param name="result">The decrypted permissions list, or empty list on failure.</param>
        /// <returns>True if decryption succeeded; otherwise, false.</returns>
        public bool TryDecrypt(string encrypted, out List<Permission> result)
        {
            #region implementation
            try
            {
                if (string.IsNullOrWhiteSpace(encrypted))
                {
                    _logger.LogWarning("Encrypted data is null or empty.");
                    result = new List<Permission>();
                    return false;
                }

                result = Decrypt(encrypted);

                if (result == null || !result.Any())
                {
                    _logger.LogWarning("Decrypted permissions list is null or empty.");
                    return false;
                }

                _logger.LogInformation("Permission decryption successful for encrypted data (first 20 chars): {EncryptedDataStart}", encrypted.Substring(0, Math.Min(encrypted.Length, 20)));

                return true;
            }
            catch (Exception ex) // Catch specific exceptions if possible
            {
                _logger.LogWarning(ex, "Permission decryption attempt failed for encrypted data (first 20 chars): {EncryptedDataStart}", encrypted?.Substring(0, Math.Min(encrypted?.Length ?? 0, 20)));

                result = new List<Permission>();

                return false;
            }
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Decrypts an encrypted string into a list of permissions using the configured encryption key.
        /// </summary>
        /// <param name="encrypted">The encrypted string to decrypt.</param>
        /// <returns>The decrypted list of permissions.</returns>
        public List<Permission> Decrypt(string encrypted)
        {
            #region implementation
            try
            {
                if (string.IsNullOrWhiteSpace(encrypted))
                {
                    _logger.LogWarning("Encrypted data is null or empty.");
                    return new List<Permission>();
                }

                _logger.LogInformation("Attempting to decrypt permissions with encryption key");

                var json = _stringCipher.Decrypt(encrypted, _encryptionKey);

                if (string.IsNullOrWhiteSpace(json))
                {
                    _logger.LogWarning("Decrypted JSON is null or empty.");

                    return new List<Permission>();
                }

                return FromJson(json);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Permission decryption failed.");
                throw;
            }
            #endregion
        }

        #endregion
    }
}