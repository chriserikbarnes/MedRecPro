using System.Collections.Generic;
using MedRecPro.Models; // For Permission
using static MedRecPro.Models.Constant; // For ActorType, PermissionType

namespace MedRecPro.Service
{
    /// <summary>
    /// Defines the contract for a service that manages and manipulates permissions within the system.
    /// This service provides functionalities for checking, modifying, serializing, deserializing,
    /// encrypting, and decrypting lists of <see cref="Permission"/> objects.
    /// </summary>
    /// <remarks>
    /// Implementations of this interface are typically registered with a dependency injection container
    /// and injected into other services or controllers that require permission management capabilities.
    /// It centralizes the logic for handling permission collections, ensuring consistent application
    /// of rules like duplicate prevention and system administrator privileges.
    /// </remarks>
    /// <example>
    /// How to inject and use the IPermissionService:
    /// <code>
    /// public class UserAccessManager
    /// {
    ///     private readonly IPermissionService _permissionService;
    ///     private readonly ILogger[UserAccessManager] _logger;
    ///
    ///     public UserAccessManager(IPermissionService permissionService, ILogger[UserAccessManager] logger)
    ///     {
    ///         _permissionService = permissionService;
    ///         _logger = logger;
    ///     }
    ///
    ///     public void GrantReadAccessToPatientRecord(List[Permission] userPermissions, string patientIdResource)
    ///     {
    ///         if (!_permissionService.HasPermission(userPermissions, ActorType.Clinician, patientIdResource, PermissionType.Read))
    ///         {
    ///             var readPermission = Permission.New(ActorType.Clinician, patientIdResource, PermissionType.Read, maskedPII: false);
    ///             userPermissions = _permissionService.Append(userPermissions, readPermission);
    ///             _logger.LogInformation($"Granted read access for {patientIdResource} to Clinician.");
    ///         }
    ///
    ///         // Example of encrypting permissions for storage
    ///         string encryptedPermissions = _permissionService.Encrypt(userPermissions);
    ///         _logger.LogInformation($"User permissions encrypted: {encryptedPermissions.Substring(0, 20)}..."); // Log snippet
    ///     }
    /// }
    /// </code>
    /// </example>
    public interface IPermissionService
    {
        /**************************************************************/
        /// <summary>
        /// Checks if a specific permission exists within a given list of permissions.
        /// </summary>
        /// <param name="permissions">The list of <see cref="Permission"/> objects to search within.</param>
        /// <param name="actor">The <see cref="ActorType"/> to match (e.g., Patient, Clinician).</param>
        /// <param name="resource">The resource string to match (e.g., "PatientRecord/123", "LabResult/456").</param>
        /// <param name="type">The <see cref="PermissionType"/> to match (e.g., Read, Write, Own).</param>
        /// <param name="maskedPII">
        /// Indicates whether the permission being checked applies to de-identified (masked) PII.
        /// Defaults to <c>true</c>. Set to <c>false</c> for full data access checks.
        /// </param>
        /// <returns>
        /// <c>true</c> if a permission matching all criteria exists in the list, or if the actor is <see cref="ActorType.SystemAdmin"/>;
        /// otherwise, <c>false</c>. Returns <c>false</c> if the input list is null or empty, or if the resource is null or whitespace.
        /// </returns>
        /// <remarks>
        /// The <see cref="ActorType.SystemAdmin"/> is considered to have all permissions, so this method will always return <c>true</c>
        /// if the specified <paramref name="actor"/> is <see cref="ActorType.SystemAdmin"/>, regardless of other parameters
        /// (unless the input <paramref name="permissions"/> list is null or the <paramref name="resource"/> is invalid).
        /// </remarks>
        /// <example>
        /// <code>
        /// var userPermissions = new List[Permission]
        /// {
        ///     Permission.New(ActorType.Patient, "PatientRecord/Self", PermissionType.Read, false),
        ///     Permission.New(ActorType.Clinician, "PatientRecord/123", PermissionType.Write, false)
        /// };
        ///
        /// bool canReadRecord = _permissionService.HasPermission(userPermissions, ActorType.Patient, "PatientRecord/Self", PermissionType.Read, false);
        /// // canReadRecord will be true
        ///
        /// bool canDeleteRecord = _permissionService.HasPermission(userPermissions, ActorType.Patient, "PatientRecord/Self", PermissionType.Delete, false);
        /// // canDeleteRecord will be false
        ///
        /// bool adminCanDoAnything = _permissionService.HasPermission(userPermissions, ActorType.SystemAdmin, "AnyResource", PermissionType.Delete);
        /// // adminCanDoAnything will be true
        /// </code>
        /// </example>
        bool HasPermission(
            List<Permission> permissions,
            ActorType actor,
            string resource,
            PermissionType type,
            bool maskedPII = true);

        /**************************************************************/
        /// <summary>
        /// Appends a new permission to a list, but only if an identical permission (matching Actor, Resource, Type, and MaskedPII)
        /// does not already exist in the list.
        /// </summary>
        /// <param name="permissions">The list of <see cref="Permission"/> objects to which the new permission might be added. If null, a new list will be initialized.</param>
        /// <param name="toAdd">The <see cref="Permission"/> object to add.</param>
        /// <returns>The list of permissions, potentially with the new permission added. Returns the original list if <paramref name="toAdd"/> is null or a duplicate.</returns>
        /// <remarks>
        /// This method ensures that the permission list does not contain duplicate entries based on all properties of the <see cref="Permission"/> object.
        /// If the input <paramref name="permissions"/> list is null, it will be treated as an empty list.
        /// </remarks>
        /// <example>
        /// <code>
        /// var permissions = new List[Permission]();
        /// var p1 = Permission.New(ActorType.Researcher, "Dataset/A", PermissionType.Read, true);
        ///
        /// permissions = _permissionService.Append(permissions, p1);
        /// // permissions now contains p1
        ///
        /// var p2_duplicate = Permission.New(ActorType.Researcher, "Dataset/A", PermissionType.Read, true);
        /// permissions = _permissionService.Append(permissions, p2_duplicate);
        /// // permissions still only contains one instance equivalent to p1, as p2_duplicate is a duplicate.
        ///
        /// var p3_different = Permission.New(ActorType.Researcher, "Dataset/B", PermissionType.Read, true);
        /// permissions = _permissionService.Append(permissions, p3_different);
        /// // permissions now contains p1 and p3_different.
        /// </code>
        /// </example>
        List<Permission> Append(List<Permission> permissions, Permission toAdd);

        /**************************************************************/
        /// <summary>
        /// Removes all occurrences of a permission from the list that match the properties of the specified permission.
        /// </summary>
        /// <param name="permissions">The list of <see cref="Permission"/> objects from which to remove. If null, an empty list is assumed and returned.</param>
        /// <param name="toRemove">The <see cref="Permission"/> object whose properties will be used to identify permissions to remove.
        /// All permissions in the list that have matching Actor, Resource, Type, and MaskedPII will be removed.</param>
        /// <returns>The modified list of permissions. If <paramref name="permissions"/> or <paramref name="toRemove"/> is null, it returns the original (or an empty) list.</returns>
        /// <remarks>
        /// Removal is based on value equality of all <see cref="Permission"/> properties.
        /// If multiple identical permissions exist (which shouldn't happen if <see cref="Append"/> is used consistently), all will be removed.
        /// </remarks>
        /// <example>
        /// <code>
        /// var p1 = Permission.New(ActorType.Aggregator, "SummaryReport/All", PermissionType.Read, true);
        /// var p2 = Permission.New(ActorType.Clinician, "PatientRecord/123", PermissionType.Write, false);
        /// var permissions = new List[Permission] { p1, p2 };
        ///
        /// var permissionToRemove = Permission.New(ActorType.Aggregator, "SummaryReport/All", PermissionType.Read, true);
        /// permissions = _permissionService.Remove(permissions, permissionToRemove);
        /// // permissions now only contains p2.
        ///
        /// permissions = _permissionService.Remove(permissions, null);
        /// // permissions remains unchanged (still only p2).
        /// </code>
        /// </example>
        List<Permission> Remove(List<Permission> permissions, Permission toRemove);

        /**************************************************************/
        /// <summary>
        /// Updates a permission in the list. This is typically achieved by removing any existing permission
        /// that matches the key properties (Actor, Resource, Type, MaskedPII) of the <paramref name="updated"/> permission,
        /// and then adding the <paramref name="updated"/> permission.
        /// If no matching permission is found, the <paramref name="updated"/> permission is simply added.
        /// </summary>
        /// <param name="permissions">The list of <see cref="Permission"/> objects to update. If null, a new list will be initialized.</param>
        /// <param name="updated">The <see cref="Permission"/> object containing the new values. Its properties are used to find and replace/add.</param>
        /// <returns>The modified list of permissions. Returns the original list if <paramref name="updated"/> is null.</returns>
        /// <remarks>
        /// This effectively ensures that there's at most one permission for a given combination of Actor, Resource, Type, and MaskedPII,
        /// and that its state matches the <paramref name="updated"/> permission.
        /// If the input <paramref name="permissions"/> list is null, it will be treated as an empty list.
        /// </remarks>
        /// <example>
        /// <code>
        /// var p1 = Permission.New(ActorType.UserAdmin, "User/789", PermissionType.Read, false);
        /// var permissions = new List[Permission] { p1 };
        ///
        /// // Update p1 to also have Write permission (assuming Permission object doesn't have sub-properties to change, so we replace it)
        /// var updatedP1 = Permission.New(ActorType.UserAdmin, "User/789", PermissionType.Write, false);
        /// permissions = _permissionService.Update(permissions, updatedP1);
        /// // permissions now contains updatedP1 (Type is Write) instead of the original p1.
        ///
        /// var newPermission = Permission.New(ActorType.UserAdmin, "Role/Editor", PermissionType.Assign, false);
        /// permissions = _permissionService.Update(permissions, newPermission);
        /// // permissions now also contains newPermission, as it didn't exist before.
        /// </code>
        /// </example>
        List<Permission> Update(List<Permission> permissions, Permission updated);

        /**************************************************************/
        /// <summary>
        /// Converts a list of permissions into a dictionary, grouping them by the <see cref="Permission.Resource"/> string.
        /// </summary>
        /// <param name="permissions">The list of <see cref="Permission"/> objects to convert. If null, an empty dictionary is returned.</param>
        /// <returns>
        /// A <see cref="Dictionary{TKey, TValue}"/> where the key is the resource string
        /// (or <see cref="string.Empty"/> if <see cref="Permission.Resource"/> is null)
        /// and the value is a <see cref="List{T}"/> of <see cref="Permission"/> objects associated with that resource.
        /// </returns>
        /// <remarks>
        /// This is useful for scenarios where you need to quickly access all permissions related to a specific resource.
        /// Permissions with a null or empty <see cref="Permission.Resource"/> will be grouped under an empty string key.
        /// </remarks>
        /// <example>
        /// <code>
        /// var p1 = Permission.New(ActorType.Patient, "Record/1", PermissionType.Read);
        /// var p2 = Permission.New(ActorType.Patient, "Record/1", PermissionType.Share);
        /// var p3 = Permission.New(ActorType.Clinician, "Record/2", PermissionType.Write);
        /// var permissions = new List[Permission] { p1, p2, p3 };
        ///
        /// var dict = _permissionService.ToDictionary(permissions);
        /// // dict will have two keys: "Record/1" and "Record/2"
        /// // dict["Record/1"] will be a list containing p1 and p2.
        /// // dict["Record/2"] will be a list containing p3.
        ///
        /// List[Permission] record1Permissions = dict.ContainsKey("Record/1") ? dict["Record/1"] : new List[Permission]();
        /// </code>
        /// </example>
        Dictionary<string, List<Permission>> ToDictionary(List<Permission> permissions);

        /**************************************************************/
        /// <summary>
        /// Creates a deep copy of a list of permissions.
        /// </summary>
        /// <param name="permissions">The list of <see cref="Permission"/> objects to clone. If null, an empty list is returned.</param>
        /// <returns>A new <see cref="List{T}"/> containing deep copies of the original <see cref="Permission"/> objects.</returns>
        /// <remarks>
        /// The cloning process typically involves serializing the list to JSON and then deserializing it,
        /// ensuring that the new list and its items are completely independent of the original.
        /// </remarks>
        /// <example>
        /// <code>
        /// var originalPermissions = new List[Permission] { Permission.New(ActorType.Patient, "Data/A", PermissionType.Read) };
        /// var clonedPermissions = _permissionService.Clone(originalPermissions);
        ///
        /// // Modify the clone
        /// if (clonedPermissions.Any()) { clonedPermissions[0].MaskedPII = false; }
        ///
        /// // Original remains unchanged
        /// // originalPermissions[0].MaskedPII will still be true (its default or original value)
        /// </code>
        /// </example>
        List<Permission> Clone(List<Permission> permissions);

        /**************************************************************/
        /// <summary>
        /// Serializes a list of permissions into a JSON string.
        /// </summary>
        /// <param name="permissions">The list of <see cref="Permission"/> objects to serialize. If null, an empty JSON array "[]" is returned.</param>
        /// <returns>A JSON string representation of the list. Returns "[]" if the input list is null.</returns>
        /// <remarks>
        /// This method typically uses a JSON serialization library like Newtonsoft.Json.
        /// </remarks>
        /// <example>
        /// <code>
        /// var permissions = new List[Permission] { Permission.New(ActorType.Patient, "Record/XYZ", PermissionType.Own) };
        /// string json = _permissionService.ToJson(permissions);
        /// // json might be: "[{\"Actor\":0,\"Resource\":\"Record/XYZ\",\"Type\":2,\"MaskedPII\":true}]"
        /// // (Actual enum values depend on their definitions)
        /// </code>
        /// </example>
        string ToJson(List<Permission> permissions);

        /**************************************************************/
        /// <summary>
        /// Deserializes a JSON string back into a list of permissions.
        /// </summary>
        /// <param name="json">The JSON string to deserialize.</param>
        /// <returns>
        /// A <see cref="List{T}"/> of <see cref="Permission"/> objects.
        /// Returns an empty list if the JSON string is null, empty, whitespace, or represents an empty/null array.
        /// </returns>
        /// <remarks>
        /// This method typically uses a JSON deserialization library like Newtonsoft.Json.
        /// It's designed to be robust against null or invalid JSON, returning an empty list in such cases.
        /// </remarks>
        /// <example>
        /// <code>
        /// string json = "[{\"Actor\":0,\"Resource\":\"Record/XYZ\",\"Type\":2,\"MaskedPII\":true}]";
        /// List[Permission] permissions = _permissionService.FromJson(json);
        /// // permissions will contain one Permission object if deserialization is successful.
        ///
        /// List[Permission] emptyPermissions = _permissionService.FromJson(null);
        /// // emptyPermissions will be an empty list.
        /// </code>
        /// </example>
        List<Permission> FromJson(string json);

        /**************************************************************/
        /// <summary>
        /// Encrypts a list of permissions by first serializing it to JSON and then encrypting the JSON string.
        /// </summary>
        /// <param name="permissions">The list of <see cref="Permission"/> objects to encrypt.</param>
        /// <returns>An encrypted string representing the permissions list.</returns>
        /// <remarks>
        /// The encryption mechanism (e.g., algorithm, key) is determined by the service's implementation
        /// and its configuration (e.g., injected `StringCipher` and encryption key).
        /// This method will throw an exception if encryption fails (e.g., due to configuration issues or internal errors).
        /// </remarks>
        /// <example>
        /// <code>
        /// var permissionsToStore = new List[Permission] { Permission.New(ActorType.System, "Config", PermissionType.Manage) };
        /// try
        /// {
        ///     string encryptedData = _permissionService.Encrypt(permissionsToStore);
        ///     // Store encryptedData securely
        /// }
        /// catch (Exception ex)
        /// {
        ///     // Log encryption error
        /// }
        /// </code>
        /// </example>
        string Encrypt(List<Permission> permissions);

        /**************************************************************/
        /// <summary>
        /// Attempts to decrypt an encrypted string and deserialize it into a list of permissions.
        /// </summary>
        /// <param name="encrypted">The encrypted string to decrypt.</param>
        /// <param name="result">
        /// When this method returns, contains the decrypted and deserialized <see cref="List{T}"/> of <see cref="Permission"/> objects
        /// if the decryption and deserialization were successful; otherwise, an empty list.
        /// This parameter is passed uninitialized.
        /// </param>
        /// <returns><c>true</c> if decryption and deserialization were successful; otherwise, <c>false</c>.</returns>
        /// <remarks>
        /// This method is preferred over <see cref="Decrypt"/> when you want to handle decryption failures gracefully
        /// without exceptions. It internally catches exceptions that might occur during decryption or deserialization.
        /// </remarks>
        /// <example>
        /// <code>
        /// string encryptedDataFromStorage = "some_encrypted_string_here";
        /// List[Permission] decryptedPermissions;
        ///
        /// if (_permissionService.TryDecrypt(encryptedDataFromStorage, out decryptedPermissions))
        /// {
        ///     // Use decryptedPermissions
        ///     if (decryptedPermissions.Any()) { /* ... */ }
        /// }
        /// else
        /// {
        ///     // Handle decryption failure (e.g., log, use default permissions)
        ///     _logger.LogWarning("Failed to decrypt permissions.");
        /// }
        /// </code>
        /// </example>
        bool TryDecrypt(string encrypted, out List<Permission> result);

        /**************************************************************/
        /// <summary>
        /// Decrypts an encrypted string and deserializes it into a list of permissions.
        /// </summary>
        /// <param name="encrypted">The encrypted string to decrypt.</param>
        /// <returns>A <see cref="List{T}"/> of <see cref="Permission"/> objects if successful.</returns>
        /// <remarks>
        /// This method will throw an exception if decryption or deserialization fails (e.g., invalid key, corrupted data, invalid JSON).
        /// If robust error handling is required without exceptions, use <see cref="TryDecrypt"/> instead.
        /// The decryption mechanism is determined by the service's implementation and configuration.
        /// </remarks>
        /// <example>
        /// <code>
        /// string validEncryptedData = "previously_encrypted_valid_string";
        /// try
        /// {
        ///     List[Permission] permissions = _permissionService.Decrypt(validEncryptedData);
        ///     // Use permissions
        /// }
        /// catch (Exception ex)
        /// {
        ///     // Log decryption/deserialization error
        ///     _logger.LogError(ex, "Decryption failed.");
        /// }
        /// </code>
        /// </example>
        List<Permission> Decrypt(string encrypted);
    }
}
