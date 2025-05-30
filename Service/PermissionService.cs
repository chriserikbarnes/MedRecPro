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
    public class PermissionService : IPermissionService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<PermissionService> _logger; // Typed logger
        private readonly StringCipher _stringCipher;
        private readonly string _encryptionKey;

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

        /**************************************************************/
        /// <summary>
        /// Checks if a given permission exists in the list.
        /// </summary>
        public bool HasPermission(
            List<Permission> permissions,
            ActorType actor,
            string resource,
            PermissionType type,
            bool maskedPII = true)
        {
            if (permissions == null || !permissions.Any() || string.IsNullOrWhiteSpace(resource))
                return false;

            if (actor == ActorType.SystemAdmin) // SystemAdmin has all permissions
                return true;

            return permissions.Any(p =>
                p.Actor == actor &&
                p.Resource == resource &&
                p.Type == type &&
                p.MaskedPII == maskedPII
            );
        }

        /**************************************************************/
        /// <summary>
        /// Appends a permission to a list (adds only if not duplicate).
        /// </summary>
        public List<Permission> Append(List<Permission> permissions, Permission toAdd)
        {
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
        }

        /**************************************************************/
        /// <summary>
        /// Removes a permission from the list (by value).
        /// </summary>
        public List<Permission> Remove(List<Permission> permissions, Permission toRemove)
        {
            if (permissions == null || toRemove == null)
                return permissions ?? new List<Permission>();

            permissions.RemoveAll(p =>
                    p.Actor == toRemove.Actor &&
                    p.Resource == toRemove.Resource &&
                    p.Type == toRemove.Type &&
                    p.MaskedPII == toRemove.MaskedPII
                );

            return permissions;
        }

        /**************************************************************/
        /// <summary>
        /// Updates a permission in the list. If not found, adds it.
        /// </summary>
        public List<Permission> Update(List<Permission> permissions, Permission updated)
        {
            if (permissions == null) permissions = new List<Permission>();

            if (updated == null) return permissions;

            // Remove existing if it's there (based on matching properties)
            Remove(permissions, updated);

            // Add the new/updated one
            permissions.Add(updated);
            return permissions;
        }

        /**************************************************************/
        /// <summary>
        /// Converts the permission list to a dictionary for fast lookup by resource.
        /// </summary>
        public Dictionary<string, List<Permission>> ToDictionary(List<Permission> permissions)
        {
            if (permissions == null) return new Dictionary<string, List<Permission>>();
            return permissions
                .GroupBy(p => p.Resource ?? string.Empty)
                .ToDictionary(g => g.Key, g => g.ToList());
        }

        /**************************************************************/
        /// <summary>
        /// Returns a deep copy of the permissions list.
        /// </summary>
        public List<Permission> Clone(List<Permission> permissions)
        {
            if (permissions == null) return new List<Permission>();
            var json = ToJson(permissions);
            return FromJson(json);
        }

        /**************************************************************/
        /// <summary>
        /// Serializes a list of permissions to a JSON string.
        /// </summary>
        public string ToJson(List<Permission> permissions)
        {
            if (permissions == null) return "[]";

            // Log the start of serialization for debugging purposes
            _logger.LogInformation("Serializing permissions to JSON with count: {Count}", permissions.Count);

            return JsonConvert.SerializeObject(permissions);
        }

        /**************************************************************/
        /// <summary>
        /// Deserializes a JSON string to a list of permissions.
        /// </summary>
        public List<Permission> FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return new List<Permission>();

            // Log the start of deserialization for debugging purposes
            _logger.LogInformation("Deserializing permissions from JSON: {JsonStart}", json.Substring(0, Math.Min(json.Length, 20)));

            // Use JsonConvert to deserialize the JSON string into a list of Permission objects
            return JsonConvert.DeserializeObject<List<Permission>>(json) ?? new List<Permission>();
        }

        /**************************************************************/
        /// <summary>
        /// Encrypts the serialized permissions using the configured encryption key.
        /// </summary>
        public string Encrypt(List<Permission> permissions)
        {
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

                return StringCipher.Encrypt(json, _encryptionKey);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Permission encryption failed.");
                throw;
            }
        }

        /**************************************************************/
        /// <summary>
        /// Attempts to decrypt and deserialize, returning false if unsuccessful.
        /// </summary>
        public bool TryDecrypt(string encrypted, out List<Permission> result)
        {
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
        }

        /**************************************************************/
        /// <summary>
        /// Decrypts an encrypted string into a list of permissions using the configured encryption key.
        /// </summary>
        public List<Permission> Decrypt(string encrypted)
        {
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
        }
    }
}
