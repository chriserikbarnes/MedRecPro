using System;
using System.ComponentModel.DataAnnotations.Schema;
using Newtonsoft.Json;
using MedRecPro.Helpers;
using System.ComponentModel;
using Microsoft.AspNetCore.Identity; // Assuming this is where StringCipher is defined

namespace MedRecPro.Models // Or your preferred namespace
{
    #region NewUser Class
    /// <summary>
    /// Represents a new user being created in the system.
    /// </summary>
    public class NewUser
    {
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
        /// <summary>  
        /// Converts the current <see cref="NewUser"/> instance to a <see cref="User"/> instance.  
        /// </summary>  
        /// <returns>A new <see cref="User"/> instance with properties copied from the current <see cref="NewUser"/> instance.</returns>  
        public User ToUser()
        {
            #region implementation  
            // Create a new User instance  
            var user = new User
            {
                // Copy properties from the base NewUser class (this instance)  
                CanonicalUsername = this.CanonicalUsername?.ToLowerInvariant(), // Ensure canonical is lowercase  
                PhoneNumber = this.PhoneNumber,
                DisplayName = this.DisplayName,
                PrimaryEmail = this.PrimaryEmail, // Consider lowercasing email too for consistency  
                MfaEnabled = this.MfaEnabled,
                // Hash the password immediately if provided - ** REPLACE WITH SECURE HASHING **  
                PasswordHash = string.IsNullOrWhiteSpace(this.Password) ? null : _passwordHasher.HashPassword(null, this.Password), // Placeholder  
                Password = null, // Clear plaintext after potential hashing  
                UserRole = this.UserRole,
                UserPermissions = this.UserPermissions,
                Timezone = this.Timezone,
                Locale = this.Locale,
                NotificationSettings = this.NotificationSettings,
                UiTheme = this.UiTheme,
                TosVersionAccepted = this.TosVersionAccepted,
                TosAcceptedAt = this.TosAcceptedAt,
                TosMarketingOptIn = this.TosMarketingOptIn,
                TosEmailNotification = this.TosEmailNotification
            };

            // If a password was provided during creation, set the PasswordChangedAt timestamp  
            if (!string.IsNullOrWhiteSpace(user.PasswordHash)) // Check hash instead of plaintext  
            {
                user.PasswordChangedAt = DateTime.UtcNow;
            }
            // Set creation time   
            user.CreatedAt = DateTime.UtcNow;

            return user;
            #endregion
        }

        #endregion

        #endregion
    }
    #endregion

    #region User Class (inherits from NewUser)
    /// <summary>
    /// Represents an existing user in the system, inheriting from <see cref="NewUser"/>.
    /// </summary>
    public class User : NewUser
    {
        #region Private Fields
        private long _userId;
        // Store the encryption secret retrieved from configuration
        private static string? _pkSecret;
        // Static lock for thread-safe initialization of the secret
        private static readonly object _secretLock = new object();
        // Static configuration instance (injected or resolved)
        private static IConfiguration? _configuration;
        /// <summary>
        /// Initializes a new instance of the <see cref="User"/> class.
        /// </summary>
        public User()
        {
            SecurityStamp = Guid.NewGuid();

        }

        /**************************************************************/
        /// <summary>
        /// Sets the configuration source for retrieving the encryption secret.
        /// </summary>
        /// <param name="configuration">The IConfiguration instance.</param>
        /// <remarks>
        /// This method allows injecting the configuration, typically during application startup.
        /// It's essential for retrieving the "Security:DB:PKSecret".
        /// </remarks>
        public static void SetConfiguration(IConfiguration configuration)
        {
            #region implementation
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            // Optionally load the secret immediately, or do it lazily.
            #endregion
        }
        #endregion

        #region Properties (Added or specific to existing User)

        #region Primary Key & Identity (specific to existing User)

        /// <summary>
        /// Database Primary Key. Mapped by Dapper from the 'UserID' column.
        /// Should generally not be set directly after initial retrieval.
        /// </summary>
        [Dapper.Contrib.Extensions.Key] // Tells Dapper.Contrib this is the PK (optional)
        [JsonIgnore]
        public long UserID { get; set; } // Dapper needs this public property to map the DB column

        /**************************************************************/
        /// <summary>
        /// Retrieves the Primary Key encryption secret from configuration.
        /// </summary>
        /// <returns>The encryption secret string.</returns>
        /// <exception cref="InvalidOperationException">Thrown if configuration is not set or the secret is missing.</exception>
        private static string getPkSecret()
        {
            #region implementation
            // Check if already loaded
            if (_pkSecret == null)
            {
                // Thread-safe initialization
                lock (_secretLock)
                {
                    // Double-check lock pattern
                    if (_pkSecret == null)
                    {
                        if (_configuration == null)
                        {
                            throw new InvalidOperationException("IConfiguration has not been set for the User class. Call User.SetConfiguration() first.");
                        }

                        // Retrieve the secret from configuration
                        string? secret = _configuration["Security:DB:PKSecret"]; // Or "Security__DB__PKSecret" depending on config provider

                        if (string.IsNullOrWhiteSpace(secret))
                        {
                            // Handle missing secret - throw exception or use a default (not recommended for secrets)
                            throw new InvalidOperationException("Required configuration key 'Security:DB:PKSecret' is missing or empty.");
                        }
                        _pkSecret = secret;
                    }
                }
            }
            return _pkSecret;
            #endregion
        }

        #endregion

        #region Primary Key & Identity

        /// <summary>
        /// Gets or sets the encrypted string representation of the UserID.
        /// This property is used for serialization and external exposure.
        /// </summary>
        /// <remarks>
        /// Getting this property encrypts the internal _userId.
        /// Setting this property decrypts the value and stores it in the internal _userId.
        /// Throws exceptions if decryption fails (e.g., invalid format, wrong key).
        /// Marked with [JsonProperty] for Newtonsoft.Json serialization.
        /// </remarks>
        [JsonProperty("userId")] // Serialize as "userId" in JSON
        public string EncryptedUserId
        {
            #region implementation
            get
            {
                // Only encrypt if the ID is set (greater than 0)
                if (_userId > 0)
                {
                    // Retrieve the secret (lazily)
                    string secret = getPkSecret();
                    // Encrypt the UserID using the helper class
                    return StringCipher.Encrypt(_userId.ToString(), secret);
                }
                // Return null or empty if ID is not set
                return string.Empty;
            }
            set
            {
                // Only attempt decryption if the value is not empty
                if (!string.IsNullOrWhiteSpace(value))
                {
                    try
                    {
                        // Retrieve the secret (lazily)
                        string secret = getPkSecret();
                        // Decrypt the incoming string to get the UserID
                        // Need access to Decrypt - assuming User class is in MedRecPro.Helpers
                        // or StringCipher.Decrypt is made public/internal.
                        // For this example, we'll create an instance of StringCipher
                        // if Decrypt remains protected internal and User is outside Helpers.
                        // If User is IN MedRecPro.Helpers, a static call might work if Decrypt were static,
                        // but it's not. So an instance or modification is needed.
                        // Let's assume we create an instance:
                        var cipher = new StringCipher(); // Requires public constructor
                        string decryptedId = cipher.Decrypt(value, secret);

                        // Parse the decrypted string back to a long
                        if (long.TryParse(decryptedId, out long id))
                        {
                            _userId = id;
                        }
                        else
                        {
                            // Handle parsing failure
                            throw new FormatException("Decrypted UserID is not a valid long integer.");
                        }
                    }
                    catch (FormatException) // Catch specific parse error
                    {
                        // Re-throw or handle invalid format after decryption
                        throw;
                    }
                    catch (Exception ex) // Catch decryption errors (e.g., CryptographicException)
                    {
                        // Log the exception securely
                        // Throw a generic error to avoid leaking crypto details
                        throw new InvalidOperationException("Failed to decrypt UserID. The provided identifier may be invalid or corrupted.", ex);
                    }
                }
                else
                {
                    // If the input is null/empty, reset the internal ID
                    _userId = 0;
                }
            }
            #endregion
        }

        /// <summary>
        /// Gets the internal UserID. Intended for internal use or database mapping only.
        /// </summary>
        /// <remarks>
        /// Marked with [JsonIgnore] to prevent serialization.
        /// Database mapping tools (like EF Core) might map directly to the private `_userId` field
        /// or use this internal property if needed, but it should generally be avoided in DTOs.
        /// For EF Core, you might configure the backing field directly:
        /// modelBuilder.Entity<User>().Property(u => u.EncryptedUserId).HasField("_userId")...
        /// Or map this property and ignore EncryptedUserId for DB purposes.
        /// Using [NotMapped] if this property itself shouldn't be mapped by convention.
        /// </remarks>
        [JsonIgnore]       // Prevent serialization by Newtonsoft.Json
        [NotMapped]        // Prevent mapping by EF Core by convention (if EncryptedUserId is mapped)
        internal long UserIdInternal => _userId; // Internal getter for controlled access

        /// <summary>
        /// Sets the internal UserID directly. Use with caution, primarily during object hydration from DB.
        /// Also updates the public UserID property for consistency.
        /// </summary>
        internal void SetUserIdInternal(long userId)
        {
            #region implementation
            _userId = userId;
            this.UserID = userId; // Keep public UserID in sync
            // You might set EncryptedUserId here too if using encryption
            // EncryptedUserId = YourStringCipher.EncryptId(userId);
            #endregion
        }

        /// <summary>
        /// Gets the internal UserID from the encrypted string. Use with caution.
        /// </summary>
        /// <returns>The decrypted UserID, or 0 if decryption fails or EncryptedUserId is null/empty.</returns>
        [JsonIgnore]
        [NotMapped] // Important if using EF Core
        internal long DecryptedUserId
        {
            get
            {
                // Assuming StringCipher.DecryptId exists and returns 0 on failure
                // return StringCipher.DecryptId(EncryptedUserId);
                return long.TryParse(EncryptedUserId, out long id) ? id : 0; // Placeholder
            }
        }

        /// <summary>
        /// Timestamp when PrimaryEmail was verified; NULL until verification succeeds.
        /// </summary>
        [JsonProperty("emailVerifiedAt")]
        public DateTime? EmailVerifiedAt { get; set; }
        #endregion

        #region Authentication & security (state for existing User)
        /// <summary>
        /// Password hash produced by PBKDF2/bcrypt/argon2id; plaintext is never stored.
        /// </summary>
        [JsonIgnore] // Exclude from standard JSON serialization
        public string? PasswordHash { get; set; }

        /// <summary>
        /// UTC timestamp of the most recent password change/reset.
        /// </summary>
        [JsonProperty("passwordChangedAt")]
        public DateTime? PasswordChangedAt { get; set; }

        /// <summary>
        /// Consecutive failed login attempts since last success; reset on successful login.
        /// </summary>
        [JsonProperty("failedLoginCount")]
        public int FailedLoginCount { get; set; } // Defaults to 0

        /// <summary>
        /// Account locked until this UTC time due to repeated failed logins.
        /// </summary>
        [JsonProperty("lockoutUntil")]
        public DateTime? LockoutUntil { get; set; }

        /// <summary>
        /// Encrypted TOTP seed or WebAuthn credential ID used to validate MFA challenges.
        /// </summary>
        [JsonIgnore] // Exclude from standard JSON serialization
        public string? MfaSecret { get; set; }

        /// <summary>
        /// Random GUID rotated on password/MFA changes; invalidates cached sessions. Generated on creation.
        /// </summary>
        [JsonProperty("securityStamp")]
        public Guid SecurityStamp { get; set; } // Initialized in constructor
        #endregion

        #region Authorization & role‑based access (state for existing User)
        /// <summary>
        /// JSON blob of objects this user follows (e.g., for social features). Managed after creation.
        /// </summary>
        [JsonProperty("userFollowing")]
        public string? UserFollowing { get; set; } // Stored as JSON string
        #endregion

        #region Lifecycle & auditing (specific to existing User)
        /// <summary>
        /// Record creation timestamp (UTC). Set by the system on creation.
        /// </summary>
        [JsonProperty("createdAt")]
        public DateTime CreatedAt { get; set; } // Should be set on save or by DB default

        /// <summary>
        /// FK to Users.UserID or system user who created the record. Set by the system on creation.
        /// Needs encryption/decryption handling if exposed externally.
        /// </summary>
        [JsonProperty("createdById")] // Raw ID, handle externally
        public long? CreatedByID { get; set; }

        /// <summary>
        /// UTC timestamp of the most recent profile update. Set by the system on update.
        /// </summary>
        [JsonProperty("updatedAt")]
        public DateTime? UpdatedAt { get; set; }

        /// <summary>
        /// UserID or system process that performed the most recent update. Set by the system on update.
        /// Needs encryption/decryption handling if exposed externally.
        /// </summary>
        [JsonProperty("updatedBy")] // Raw ID, handle externally
        public long? UpdatedBy { get; set; }

        /// <summary>
        /// Soft‑delete marker (GDPR “right to forget”). Set by the system on deletion.
        /// </summary>
        [JsonProperty("deletedAt")]
        public DateTime? DeletedAt { get; set; }

        /// <summary>
        /// Timestamp when the account was administratively suspended.
        /// </summary>
        [JsonProperty("suspendedAt")]
        public DateTime? SuspendedAt { get; set; }

        /// <summary>
        /// Reason provided for suspension (policy violation, fraud, etc.).
        /// </summary>
        [JsonProperty("suspensionReason")]
        public string? SuspensionReason { get; set; }

        /// <summary>
        /// UTC timestamp of the last successful login.
        /// </summary>
        [JsonProperty("lastLoginAt")]
        public DateTime? LastLoginAt { get; set; }

        /// <summary>
        /// UTC timestamp of the user's most recent API or UI activity.
        /// </summary>
        [JsonProperty("lastActivityAt")]
        public DateTime? LastActivityAt { get; set; }

        /// <summary>
        /// IP address (IPv4/IPv6) recorded at last login/activity; can be anonymized per policy.
        /// </summary>
        [JsonProperty("lastIpAddress")]
        public string? LastIpAddress { get; set; }
        #endregion
 
        #endregion
    }
  #endregion
}
