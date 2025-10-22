using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using MedRecPro.Helpers;
using static MedRecPro.Models.Constant;

namespace MedRecPro.Models
{
    /// <summary>
    /// Represents a fine-grained permission assignment for a user or role within the system.
    /// <para>
    /// <b>Distinctions:</b>
    /// <list type="bullet">
    ///   <item>
    ///     <description>
    ///       <b>Role</b>: A coarse-grained classification that typically determines a user's broad standing or administrative access in the system
    ///       (e.g., "Admin", "User", "UserAdmin"). Roles are not directly referenced here but are used for system-level access control.
    ///     </description>
    ///   </item>
    ///   <item>
    ///     <description>
    ///       <b>Actor</b>: Describes the context-specific identity or capacity in which the user is operating for a given resource or action
    ///       (e.g., Patient, Clinician, Aggregator). The <see cref="Actor"/> property specifies this contextual role.
    ///     </description>
    ///   </item>
    ///   <item>
    ///     <description>
    ///       <b>Permission</b>: Defines the type of operation that is allowed on a resource (e.g., Read, Write, Own, Delete).
    ///       The <see cref="Type"/> property captures this action.
    ///     </description>
    ///   </item>
    /// </list>
    /// </para>
    /// <para>
    /// The <see cref="Permission"/> class allows precise control over what actions an actor can perform on specific resources,
    /// supporting robust and auditable access control in healthcare and research environments.
    /// </para>
    /// </summary>
    public class Permission
    {
        #region Properties
        // DI fields for encryption and logging
        private static IConfiguration? _configuration;
        private static ILogger? _logger;
        private static string? _encryptionKey;
        private static StringCipher? _stringCipher;

        /// <summary>
        /// The contextual identity (actor type) for which this permission applies (e.g., Patient, Clinician, Aggregator).
        /// </summary>
        public ActorType Actor { get; set; }

        /// <summary>
        /// The resource to which this permission applies (e.g., "PatientRecord", "LabResult").
        /// </summary>
        public string? Resource { get; set; }

        /// <summary>
        /// The type of permission granted (e.g., Read, Write, Own, Delete).
        /// </summary>
        public PermissionType Type { get; set; }

        /// <summary>
        /// Indicates whether the permission applies only to de-identified (masked) personally identifiable information (PII).
        /// Set to <c>true</c> for masked data, or <c>false</c> for full data access.
        /// </summary>
        public bool MaskedPII { get; set; } = true;
        #endregion

        #region Methods
        /**************************************************************/
        /// <summary>
        /// Creates a new permission instance.
        /// </summary>
        public static Permission New(ActorType actor, string resource, PermissionType type, bool maskedPII = true)
            => new Permission
            {
                Actor = actor,
                Resource = resource,
                Type = type,
                MaskedPII = maskedPII
            };      
        #endregion
    }
}
