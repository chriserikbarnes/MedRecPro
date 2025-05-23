using System;
using System.ComponentModel.DataAnnotations.Schema;
using Newtonsoft.Json;
using MedRecPro.Helpers;
using System.ComponentModel;
using Microsoft.AspNetCore.Identity; // For IdentityUser<TKey>
using Microsoft.Extensions.Configuration; // For IConfiguration

namespace MedRecPro.Models // Or your preferred namespace
{
    #region NewUser Class
    /// <summary>
    /// Represents a new user being created in the system.
    /// This class is a DTO and does not inherit from IdentityUser.
    /// </summary>
    public class NewUser
    {
        // PasswordHasher here is for the DTO, if needed before creating the actual User entity.
        // However, password hashing should ideally happen when converting ToUser() or in UserDataAccess.
        private readonly IPasswordHasher<NewUser> _passwordHasher = new PasswordHasher<NewUser>();
        #region Properties

        #region Identity & contact info (provided during creation)
        /// <summary>
        /// Case‑folded username for unique checks. May be derived or provided.
        /// </summary>
        [JsonProperty("canonicalUsername")]
        public string? CanonicalUsername { get; set; }

        /// <summary>
        /// Optional phone number for 2FA / recovery.
        /// </summary>
        [JsonProperty("phoneNumber")]
        public string? PhoneNumber { get; set; }

        /// <summary>
        /// Friendly name shown in the UI.
        /// </summary>
        [JsonProperty("displayName")]
        public string? DisplayName { get; set; }

        /// <summary>
        /// Primary email address (RFC 5322 max email length). Required for new user.
        /// </summary>
        [JsonProperty("primaryEmail")]
        public string PrimaryEmail { get; set; } = null!; // Required
        #endregion

        #region Authentication & security (initial settings)
        /// <summary>
        /// Plaintext password provided during signup. Not stored directly.
        /// </summary>
        [JsonIgnore]
        [PasswordPropertyText(true)] // Hides in UI, if applicable
        public string? Password { get; set; } // Plaintext password, hashed before saving

        /// <summary>
        /// Boolean flag indicating if multi‑factor authentication should be enabled initially.
        /// Defaults based on system policy, can be overridden during creation.
        /// </summary>
        [JsonProperty("mfaEnabled")]
        public bool MfaEnabled { get; set; } // Default might be false
        #endregion

        #region Authorization & role‑based access (initial settings)
        /// <summary>
        /// Initial coarse‑grained role (e.g., User, Admin). Defaults to 'User'.
        /// </summary>
        [JsonProperty("userRole")]
        public string UserRole { get; set; } = "User"; // Matches DB default

        /// <summary>
        /// Initial JSON blob of permissions, if applicable during creation.
        /// </summary>
        [JsonProperty("userPermissions")]
        public string? UserPermissions { get; set; } // Stored as JSON string
        #endregion

        #region Preferences & locale (initial settings)
        /// <summary>
        /// Initial IANA timezone identifier. Defaults to 'UTC'.
        /// </summary>
        [JsonProperty("timezone")]
        public string Timezone { get; set; } = "UTC"; // Matches DB default

        /// <summary>
        /// Initial locale/region. Defaults to 'en-US'.
        /// </summary>
        [JsonProperty("locale")]
        public string Locale { get; set; } = "en-US"; // Matches DB default

        /// <summary>
        /// Initial JSON blob of notification toggles, if applicable during creation.
        /// </summary>
        [JsonProperty("notificationSettings")]
        public string? NotificationSettings { get; set; } // Stored as JSON string

        /// <summary>
        /// Initial preferred UI theme.
        /// </summary>
        [JsonProperty("uiTheme")]
        public string? UiTheme { get; set; }
        #endregion

        #region Terms & compliance (set during creation)
        /// <summary>
        /// Terms of Service version the user accepted during signup.
        /// </summary>
        [JsonProperty("tosVersionAccepted")]
        public string? TosVersionAccepted { get; set; }

        /// <summary>
        /// UTC timestamp when the user accepted the Terms of Service during signup.
        /// </summary>
        [JsonProperty("tosAcceptedAt")]
        public DateTime? TosAcceptedAt { get; set; }

        /// <summary>
        /// GDPR opt-in for marketing emails, set during signup.
        /// </summary>
        [JsonProperty("tosMarketingOptIn")]
        public bool TosMarketingOptIn { get; set; } // Default might be false

        /// <summary>
        /// User agreement to receive email notifications, set during signup.
        /// </summary>
        [JsonProperty("tosEmailNotification")]
        public bool TosEmailNotification { get; set; } // Default might be false

        #endregion

        #endregion

        /// <summary>  
        /// Converts the current <see cref="NewUser"/> instance to a <see cref="User"/> instance.  
        /// </summary>  
        /// <returns>A new <see cref="User"/> instance with properties copied from the current <see cref="NewUser"/> instance.</returns>  
        public User ToUser(IPasswordHasher<User> appUserPasswordHasher) // Pass the correct hasher
        {
            var user = new User
            {
                // Properties from NewUser
                UserName = this.PrimaryEmail, // IdentityUser uses UserName, often set to email
                NormalizedUserName = this.PrimaryEmail.ToUpperInvariant(), // Identity requires NormalizedUserName
                Email = this.PrimaryEmail, // IdentityUser uses Email
                NormalizedEmail = this.PrimaryEmail.ToUpperInvariant(), // Identity requires NormalizedEmail
                PhoneNumber = this.PhoneNumber,
                EmailConfirmed = false, // Typically false until confirmed
                PhoneNumberConfirmed = false, // Typically false until confirmed

                // Custom properties from NewUser / MedRecPro.Models.User
                CanonicalUsername = this.CanonicalUsername?.ToLowerInvariant(),
                DisplayName = this.DisplayName,
                PrimaryEmail = this.PrimaryEmail, // Retain for your custom logic if needed
                MfaEnabled = this.MfaEnabled, // Custom property, IdentityUser has TwoFactorEnabled
                TwoFactorEnabled = this.MfaEnabled, // Align with IdentityUser property

                UserRole = this.UserRole,
                UserPermissions = this.UserPermissions,
                Timezone = this.Timezone,
                Locale = this.Locale,
                NotificationSettings = this.NotificationSettings,
                UiTheme = this.UiTheme,
                TosVersionAccepted = this.TosVersionAccepted,
                TosAcceptedAt = this.TosAcceptedAt,
                TosMarketingOptIn = this.TosMarketingOptIn,
                TosEmailNotification = this.TosEmailNotification,
                CreatedAt = DateTime.UtcNow, // Set creation time
                SecurityStamp = Guid.NewGuid().ToString() // IdentityUser requires SecurityStamp as string
            };

            if (!string.IsNullOrWhiteSpace(this.Password))
            {
                user.PasswordHash = appUserPasswordHasher.HashPassword(user, this.Password); // Use the passed hasher
                user.PasswordChangedAt = DateTime.UtcNow;
            }

            // Clear plaintext password from the DTO if desired, though it's typically done after this method call
            // this.Password = null; 

            return user;
        }

    }
    #endregion

    #region User Class (inherits from IdentityUser<long>)
    /// <summary>
    /// Represents an existing user in the system, inheriting from <see cref="IdentityUser{TKey}"/>.
    /// </summary>
    public class User : IdentityUser<long> // Inherit from IdentityUser<long>
    {
        #region Private Fields for PK Secret (if still used for EncryptedUserId property)
        private static string? _pkSecret;
        private static readonly object _secretLock = new object();
        private static IConfiguration? _configuration; // Static configuration instance

        #endregion

        /// <summary>
        /// Initializes a new instance of the <see cref="User"/> class.
        /// </summary>
        public User()
        {
            // SecurityStamp is inherited from IdentityUser and is a string.
            // It will be initialized by Identity services or can be set:
            this.SecurityStamp = Guid.NewGuid().ToString();
        }

        #region Custom Properties (Not in IdentityUser by default)

        private string? _encryptedUserId = null; // Backing field for EncryptedUserId property

        /// <summary>
        /// Case‑folded username for unique checks. May be derived or provided.
        /// IdentityUser has UserName and NormalizedUserName. This can be an additional field if semantics differ.
        /// </summary>
        [PersonalData]
        public string? CanonicalUsername { get; set; }

        /// <summary>
        /// Friendly name shown in the UI.
        /// </summary>
        [PersonalData]
        public string? DisplayName { get; set; }

        /// <summary>
        /// Primary email address. IdentityUser has Email and NormalizedEmail.
        /// This can be redundant or used if your app has a different concept of "PrimaryEmail".
        /// For Identity integration, ensure `this.Email` is set.
        /// </summary>
        [ProtectedPersonalData]
        public string PrimaryEmail { get; set; } = null!;


        /// <summary>
        /// Boolean flag indicating if multi‑factor authentication is enabled.
        /// IdentityUser has `TwoFactorEnabled`. This can be a custom flag or map to it.
        /// </summary>
        public bool MfaEnabled { get; set; } // Custom property, distinct from IdentityUser.TwoFactorEnabled if needed

        // PasswordHash is inherited from IdentityUser
        // SecurityStamp is inherited from IdentityUser (it's a string)

        /// <summary>
        /// UTC timestamp of the most recent password change/reset.
        /// </summary>
        public DateTime? PasswordChangedAt { get; set; }

        /// <summary>
        /// Consecutive failed login attempts since last success; reset on successful login.
        /// IdentityUser has `AccessFailedCount`.
        /// </summary>
        public int FailedLoginCount { get; set; } // Custom, or map to IdentityUser.AccessFailedCount

        /// <summary>
        /// Account locked until this UTC time due to repeated failed logins.
        /// IdentityUser has `LockoutEnd` (DateTimeOffset?).
        /// </summary>
        public DateTime? LockoutUntil { get; set; } // Custom, or map to IdentityUser.LockoutEnd

        /// <summary>
        /// Encrypted TOTP seed or WebAuthn credential ID used to validate MFA challenges.
        /// This should be handled by Identity's mechanisms if using its 2FA.
        /// </summary>
        [JsonIgnore]
        public string? MfaSecret { get; set; }


        /// <summary>
        /// Coarse‑grained role (e.g., User, Admin).
        /// Roles are typically managed via `UserManager.AddToRoleAsync`.
        /// </summary>
        public string UserRole { get; set; } = "User";

        /// <summary>
        /// JSON blob of permissions, if applicable.
        /// </summary>
        public string? UserPermissions { get; set; }


        /// <summary>
        /// IANA timezone identifier.
        /// </summary>
        [PersonalData]
        public string Timezone { get; set; } = "UTC";

        /// <summary>
        /// Locale/region.
        /// </summary>
        [PersonalData]
        public string Locale { get; set; } = "en-US";

        /// <summary>
        /// JSON blob of notification toggles.
        /// </summary>
        public string? NotificationSettings { get; set; }

        /// <summary>
        /// Preferred UI theme.
        /// </summary>
        public string? UiTheme { get; set; }


        /// <summary>
        /// Terms of Service version the user accepted.
        /// </summary>
        public string? TosVersionAccepted { get; set; }

        /// <summary>
        /// UTC timestamp when the user accepted the Terms of Service.
        /// </summary>
        public DateTime? TosAcceptedAt { get; set; }

        /// <summary>
        /// GDPR opt-in for marketing emails.
        /// </summary>
        public bool TosMarketingOptIn { get; set; }

        /// <summary>
        /// User agreement to receive email notifications.
        /// </summary>
        public bool TosEmailNotification { get; set; }

        /// <summary>
        /// JSON blob of objects this user follows.
        /// </summary>
        public string? UserFollowing { get; set; }


        /// <summary>
        /// Record creation timestamp (UTC).
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// FK to Users.UserID or system user who created the record.
        /// </summary>
        public long? CreatedByID { get; set; } // This would be a long

        /// <summary>
        /// UTC timestamp of the most recent profile update.
        /// </summary>
        public DateTime? UpdatedAt { get; set; }

        /// <summary>
        /// UserID or system process that performed the most recent update.
        /// </summary>
        public long? UpdatedBy { get; set; } // This would be a long

        /// <summary>
        /// Soft‑delete marker.
        /// </summary>
        public DateTime? DeletedAt { get; set; }

        /// <summary>
        /// Timestamp when the account was administratively suspended.
        /// </summary>
        public DateTime? SuspendedAt { get; set; }

        /// <summary>
        /// Reason provided for suspension.
        /// </summary>
        public string? SuspensionReason { get; set; }

        /// <summary>
        /// UTC timestamp of the last successful login.
        /// </summary>
        public DateTime? LastLoginAt { get; set; }

        /// <summary>
        /// UTC timestamp of the user's most recent API or UI activity.
        /// </summary>
        public DateTime? LastActivityAt { get; set; }

        /// <summary>
        /// IP address recorded at last login/activity.
        /// </summary>
        public string? LastIpAddress { get; set; }

        /// <summary>
        /// Internal method to set the UserID (which is 'Id').
        /// This is primarily for UserDataAccess to set the ID after retrieval if necessary,
        /// though EF Core typically handles PK hydration.
        /// </summary>
        internal void SetUserIdInternal(long userId)
        {
            this.Id = userId;
        }

        [JsonIgnore]
        [NotMapped]
        internal long UserIdInternal => this.Id;

        [JsonIgnore]
        [NotMapped]
        internal string? Password { get; set; } // For password hashing, if needed
        #endregion

        /// <summary>
        /// Sets the configuration source for retrieving the encryption secret (if EncryptedUserId is used).
        /// </summary>
        public static void SetConfiguration(IConfiguration configuration)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        private static string getPkSecret()
        {
            if (_pkSecret == null)
            {
                lock (_secretLock)
                {
                    if (_pkSecret == null)
                    {
                        if (_configuration == null)
                        {
                            throw new InvalidOperationException("IConfiguration has not been set for the User class. Call User.SetConfiguration() first for EncryptedUserId feature.");
                        }
                        string? secret = _configuration["Security:DB:PKSecret"];
                        if (string.IsNullOrWhiteSpace(secret))
                        {
                            throw new InvalidOperationException("Required configuration key 'Security:DB:PKSecret' is missing or empty for EncryptedUserId feature.");
                        }
                        _pkSecret = secret;
                    }
                }
            }
            return _pkSecret;
        }

        public bool IsUserAdmin()
        {
            // Check for null role to avoid NullReferenceException
            if(string.IsNullOrEmpty(this.UserRole))
            {
                return false;
            }

            return this.UserRole.Equals(MedRecPro.Models.UserRole.Admin, StringComparison.OrdinalIgnoreCase)
            || this.UserRole.Equals(MedRecPro.Models.UserRole.UserAdmin, StringComparison.OrdinalIgnoreCase);
        }

        #region Encrypted User ID (Custom Property, separate from IdentityUser.Id)
        /// <summary>
        /// Gets or sets the encrypted string representation of the UserID (which is IdentityUser.Id).
        /// This property is for external exposure if you need an encrypted version of the long ID.
        /// The actual primary key is the inherited `Id` property of type `long`.
        /// </summary>
        [NotMapped] // This should not be mapped to the database directly by EF Core.
        [JsonProperty("userId")] // Serialize as "userId" in JSON
        public string EncryptedUserId
        {
            get
            {
                if (this.Id > 0) // Use 'this.Id' which is the PK from IdentityUser<long>
                {
                    string secret = getPkSecret();
                    return _encryptedUserId ?? StringCipher.Encrypt(this.Id.ToString(), secret);
                }
                return string.Empty;
            }
            set
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    _encryptedUserId = value;
                }
                // else { _userId = 0; } // If you had a backing field
            }
        }
        #endregion



    }

    #endregion

    #region User Data Transfer Class
    /// <summary>
    /// Data Transfer Object for user information.
    /// Contains a subset of user properties for client-facing operations.
    /// </summary>
    public class UserDto
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="UserDto"/> class.
        /// </summary>
        public UserDto() {; }

        /// <summary>
        /// Initializes a new instance of the <see cref="UserDto"/> class from a <see cref="User"/> entity.
        /// </summary>
        /// <param name="user">The user entity to map from.</param>
        public UserDto(User user)
        {
            CanonicalUsername = user.CanonicalUsername;
            DisplayName = user.DisplayName;
            PrimaryEmail = user.PrimaryEmail;
            MfaEnabled = user.MfaEnabled; // This is your custom MfaEnabled
            PasswordChangedAt = user.PasswordChangedAt;
            FailedLoginCount = user.FailedLoginCount; // Custom or user.AccessFailedCount
            LockoutUntil = user.LockoutUntil;         // Custom or user.LockoutEnd
            UserRole = user.UserRole;
            UserPermissions = user.UserPermissions;
            Timezone = user.Timezone;
            Locale = user.Locale;
            NotificationSettings = user.NotificationSettings;
            UiTheme = user.UiTheme;
            TosVersionAccepted = user.TosVersionAccepted;
            TosAcceptedAt = user.TosAcceptedAt;
            TosMarketingOptIn = user.TosMarketingOptIn;
            TosEmailNotification = user.TosEmailNotification;
            UserFollowing = user.UserFollowing;
            CreatedAt = user.CreatedAt;
            CreatedByID = user.CreatedByID;
            UpdatedAt = user.UpdatedAt;
            DeletedAt = user.DeletedAt;
            SuspendedAt = user.SuspendedAt;
            SuspensionReason = user.SuspensionReason;
            LastLoginAt = user.LastLoginAt;
            LastActivityAt = user.LastActivityAt;
            LastIpAddress = user.LastIpAddress;
            EncryptedUserId = user.EncryptedUserId;
            UserName = user.UserName;
            Email = user.Email;
            EmailConfirmed = user.EmailConfirmed;
            PhoneNumber = user.PhoneNumber;
            PhoneNumberConfirmed = user.PhoneNumberConfirmed;
            TwoFactorEnabled = user.TwoFactorEnabled;
            LockoutEnd = user.LockoutEnd;
            AccessFailedCount = user.AccessFailedCount;
        }

        #region Properties
        /// <summary>
        /// Gets or sets the canonical username, typically a lowercase or otherwise normalized version of the username.
        /// </summary>
        public string? CanonicalUsername { get; set; }

        /// <summary>
        /// Gets or sets the display name for the user.
        /// </summary>
        public string? DisplayName { get; set; }

        /// <summary>
        /// Gets or sets the primary email address of the user. This property is required.
        /// </summary>
        public string PrimaryEmail { get; set; } = null!;

        /// <summary>
        /// Gets or sets a value indicating whether Multi-Factor Authentication (MFA) is enabled for the user.
        /// This is a custom MfaEnabled flag.
        /// </summary>
        public bool MfaEnabled { get; set; }

        /// <summary>
        /// Gets or sets the date and time when the user's password was last changed.
        /// </summary>
        public DateTime? PasswordChangedAt { get; set; }

        /// <summary>
        /// Gets or sets the number of failed login attempts.
        /// This can be a custom count or map to IdentityUser's AccessFailedCount.
        /// </summary>
        public int FailedLoginCount { get; set; }

        /// <summary>
        /// Gets or sets the date and time until which the user's account is locked out.
        /// This can be a custom field or map to IdentityUser's LockoutEnd.
        /// </summary>
        public DateTime? LockoutUntil { get; set; }

        /// <summary>
        /// Gets or sets the role assigned to the user. Defaults to "User".
        /// </summary>
        public string UserRole { get; set; } = "User";

        /// <summary>
        /// Gets or sets a string (e.g., JSON) representing specific permissions assigned to the user.
        /// </summary>
        public string? UserPermissions { get; set; }

        /// <summary>
        /// Gets or sets the user's preferred timezone. Defaults to "UTC".
        /// </summary>
        public string Timezone { get; set; } = "UTC";

        /// <summary>
        /// Gets or sets the user's preferred locale. Defaults to "en-US".
        /// </summary>
        public string Locale { get; set; } = "en-US";

        /// <summary>
        /// Gets or sets a string (e.g., JSON) representing the user's notification settings.
        /// </summary>
        public string? NotificationSettings { get; set; }

        /// <summary>
        /// Gets or sets the user's preferred UI theme.
        /// </summary>
        public string? UiTheme { get; set; }

        /// <summary>
        /// Gets or sets the version of the Terms of Service accepted by the user.
        /// </summary>
        public string? TosVersionAccepted { get; set; }

        /// <summary>
        /// Gets or sets the date and time when the user accepted the Terms of Service.
        /// </summary>
        public DateTime? TosAcceptedAt { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the user opted in for marketing communications.
        /// </summary>
        public bool TosMarketingOptIn { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the user agreed to receive email notifications related to Terms of Service.
        /// </summary>
        public bool TosEmailNotification { get; set; }

        /// <summary>
        /// Gets or sets a string (e.g., JSON) representing entities or users that this user is following.
        /// </summary>
        public string? UserFollowing { get; set; }

        /// <summary>
        /// Gets or sets the date and time when the user account was created.
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Gets or sets the ID of the user or system process that created this user record.
        /// </summary>
        public long? CreatedByID { get; set; }

        /// <summary>
        /// Gets or sets the date and time when the user account was last updated.
        /// </summary>
        public DateTime? UpdatedAt { get; set; }

        /// <summary>
        /// Gets or sets the date and time when the user account was soft-deleted.
        /// </summary>
        public DateTime? DeletedAt { get; set; }

        /// <summary>
        /// Gets or sets the date and time when the user account was suspended.
        /// </summary>
        public DateTime? SuspendedAt { get; set; }

        /// <summary>
        /// Gets or sets the reason for the user's suspension.
        /// </summary>
        public string? SuspensionReason { get; set; }

        /// <summary>
        /// Gets or sets the date and time of the user's last login.
        /// </summary>
        public DateTime? LastLoginAt { get; set; }

        /// <summary>
        /// Gets or sets the date and time of the user's last activity.
        /// </summary>
        public DateTime? LastActivityAt { get; set; }

        /// <summary>
        /// Gets or sets the last IP address recorded for the user.
        /// </summary>
        public string? LastIpAddress { get; set; }

        /// <summary>
        /// Gets or sets the encrypted user ID.
        /// </summary>
        public string? EncryptedUserId { get; set; }

        /// <summary>
        /// Gets or sets the username (typically from ASP.NET Identity).
        /// </summary>
        public string? UserName { get; set; }

        /// <summary>
        /// Gets or sets the email address (typically from ASP.NET Identity).
        /// </summary>
        public string? Email { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the email address has been confirmed (typically from ASP.NET Identity).
        /// </summary>
        public bool EmailConfirmed { get; set; }

        /// <summary>
        /// Gets or sets the phone number (typically from ASP.NET Identity).
        /// </summary>
        public string? PhoneNumber { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the phone number has been confirmed (typically from ASP.NET Identity).
        /// </summary>
        public bool PhoneNumberConfirmed { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether two-factor authentication is enabled for the user (from ASP.NET IdentityUser).
        /// </summary>
        public bool TwoFactorEnabled { get; set; }

        /// <summary>
        /// Gets or sets the date and time, including offset, until which the user is locked out (from ASP.NET IdentityUser).
        /// </summary>
        public DateTimeOffset? LockoutEnd { get; set; }

        /// <summary>
        /// Gets or sets the number of failed access attempts for the current user (from ASP.NET IdentityUser).
        /// </summary>
        public int AccessFailedCount { get; set; }
        #endregion
    }
    #endregion

}
