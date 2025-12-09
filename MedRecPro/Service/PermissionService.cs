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
    #region Permission Service Interface
    /**************************************************************/
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
    #endregion

    #region Permission Service Implementation

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
    #endregion
} 
