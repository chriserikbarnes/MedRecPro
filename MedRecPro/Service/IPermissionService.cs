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
        /// Checks if any of the specified actor types exist in the permissions list.
        /// </summary>
        /// <param name="permissions">The list of <see cref="Permission"/> objects to search within.</param>
        /// <param name="actorTypes">The actor types to check for (OR logic - any match returns true).</param>
        /// <returns>
        /// <c>true</c> if any permission with one of the specified actor types exists in the list;
        /// otherwise, <c>false</c>. Returns <c>false</c> if the input list is null or empty.
        /// </returns>
        /// <remarks>
        /// This method is particularly useful for authorization filters that need to check
        /// if a user has any of several allowed actor types. The check uses OR logic,
        /// meaning the user only needs ONE matching actor type.
        /// 
        /// If the user has <see cref="ActorType.SystemAdmin"/> in their permissions,
        /// this method returns <c>true</c> regardless of the specified actor types.
        /// </remarks>
        /// <example>
        /// <code>
        /// var userPermissions = new List[Permission]
        /// {
        ///     Permission.New(ActorType.LabelAdmin, "Labels/All", PermissionType.Write),
        ///     Permission.New(ActorType.Reviewer, "Labels/123", PermissionType.Read)
        /// };
        ///
        /// // Check if user can manage labels (requires LabelAdmin, LabelManager, or SystemAdmin)
        /// bool canManageLabels = _permissionService.HasAnyActorType(
        ///     userPermissions, 
        ///     ActorType.LabelAdmin, 
        ///     ActorType.LabelManager, 
        ///     ActorType.SystemAdmin);
        /// // canManageLabels will be true (has LabelAdmin)
        /// 
        /// // Check for unassigned actor types
        /// bool canApprove = _permissionService.HasAnyActorType(userPermissions, ActorType.Approver);
        /// // canApprove will be false
        /// </code>
        /// </example>
        /// <seealso cref="ActorType"/>
        /// <seealso cref="MedRecPro.Filters.RequireActorAttribute"/>
        bool HasAnyActorType(List<Permission> permissions, params ActorType[] actorTypes);

        /**************************************************************/
        /// <summary>
        /// Validates that a user has one of the required roles.
        /// </summary>
        /// <param name="user">The user to validate.</param>
        /// <param name="allowedRoles">The roles that are permitted (OR logic - any match returns true).</param>
        /// <returns>
        /// <c>true</c> if the user's role matches any of the allowed roles (case-insensitive);
        /// otherwise, <c>false</c>. Returns <c>false</c> if the user is null or has no role.
        /// </returns>
        /// <remarks>
        /// Role comparison is case-insensitive. Use constants from <see cref="MedRecPro.Models.UserRole"/>
        /// for consistency.
        /// </remarks>
        /// <example>
        /// <code>
        /// // Check if user is an administrator
        /// bool isAdmin = _permissionService.ValidateUserRole(user, UserRole.Admin, UserRole.UserAdmin);
        /// 
        /// // Check for specific role
        /// bool isRegularUser = _permissionService.ValidateUserRole(user, UserRole.RegularUser);
        /// </code>
        /// </example>
        /// <seealso cref="MedRecPro.Models.User.UserRole"/>
        /// <seealso cref="MedRecPro.Models.UserRole"/>
        /// <seealso cref="MedRecPro.Filters.RequireUserRoleAttribute"/>
        bool ValidateUserRole(User user, params string[] allowedRoles);

        /**************************************************************/
        /// <summary>
        /// Gets all distinct actor types present in a permissions list.
        /// </summary>
        /// <param name="permissions">The list of <see cref="Permission"/> objects to analyze.</param>
        /// <returns>
        /// A list of distinct <see cref="ActorType"/> values found in the permissions.
        /// Returns an empty list if permissions is null or empty.
        /// </returns>
        /// <remarks>
        /// Useful for displaying or logging a user's actor types, or for checking
        /// the complete set of actor types a user has been granted.
        /// </remarks>
        /// <example>
        /// <code>
        /// var actorTypes = _permissionService.GetActorTypes(userPermissions);
        /// _logger.LogInformation("User has actor types: {ActorTypes}", string.Join(", ", actorTypes));
        /// </code>
        /// </example>
        /// <seealso cref="ActorType"/>
        List<ActorType> GetActorTypes(List<Permission> permissions);

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
        List<Permission> Remove(List<Permission> permissions, Permission toRemove);

        /**************************************************************/
        /// <summary>
        /// Updates a permission in the list. If not found, adds it.
        /// </summary>
        /// <param name="permissions">The list of permissions to update.</param>
        /// <param name="updated">The permission with updated values.</param>
        /// <returns>The updated list of permissions.</returns>
        List<Permission> Update(List<Permission> permissions, Permission updated);

        /**************************************************************/
        /// <summary>
        /// Converts the permission list to a dictionary for fast lookup by resource.
        /// </summary>
        /// <param name="permissions">The list of permissions to convert.</param>
        /// <returns>A dictionary keyed by resource string.</returns>
        Dictionary<string, List<Permission>> ToDictionary(List<Permission> permissions);

        /**************************************************************/
        /// <summary>
        /// Returns a deep copy of the permissions list.
        /// </summary>
        /// <param name="permissions">The list to clone.</param>
        /// <returns>A new list with cloned permission objects.</returns>
        List<Permission> Clone(List<Permission> permissions);

        /**************************************************************/
        /// <summary>
        /// Serializes a list of permissions into a JSON string.
        /// </summary>
        /// <param name="permissions">The list of <see cref="Permission"/> objects to serialize. If null, an empty JSON array "[]" is returned.</param>
        /// <returns>A JSON string representation of the list. Returns "[]" if the input list is null.</returns>
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
        List<Permission> FromJson(string json);

        /**************************************************************/
        /// <summary>
        /// Encrypts a list of permissions by first serializing it to JSON and then encrypting the JSON string.
        /// </summary>
        /// <param name="permissions">The list of <see cref="Permission"/> objects to encrypt.</param>
        /// <returns>An encrypted string representing the permissions list.</returns>
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
        bool TryDecrypt(string encrypted, out List<Permission> result);

        /**************************************************************/
        /// <summary>
        /// Decrypts an encrypted string and deserializes it into a list of permissions.
        /// </summary>
        /// <param name="encrypted">The encrypted string to decrypt.</param>
        /// <returns>A <see cref="List{T}"/> of <see cref="Permission"/> objects if successful.</returns>
        List<Permission> Decrypt(string encrypted);
    }
}