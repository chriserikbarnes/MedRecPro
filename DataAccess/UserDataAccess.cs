using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MedRecPro.Models;
using MedRecPro.Data;
using MedRecPro.Helpers;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.ComponentModel.DataAnnotations;

namespace MedRecPro.DataAccess
{
    /**************************************************************/
    /// <summary>
    /// Repository implementation for User entities using Entity Framework Core.
    /// </summary>
    /// <remarks>
    /// This class provides data access operations for User entities using Entity Framework Core.
    /// It handles user CRUD operations, profile updates, authentication tracking, and security operations.
    /// </remarks>
    public class UserDataAccess
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly IPasswordHasher<User> _passwordHasher;
        private readonly ILogger<UserDataAccess> _logger;
        private readonly IConfiguration _configuration;

        // Store the encryption secret retrieved from configuration
        private static string? _pkSecret;
        // Static lock for thread-safe initialization of the secret
        private static readonly object _secretLock = new object();

        #region Classes
        /**************************************************************/
        /// <summary>
        /// Data transfer object for admin-controlled user updates.
        /// </summary>
        /// <remarks>
        /// This class contains fields that only administrators should be able to modify,
        /// such as user roles, security settings, and account status.
        /// The EncryptedUserId within this DTO refers to the encrypted ID of the user being modified.
        /// </remarks>
        public class AdminUserUpdate
        {
            /// <summary>
            /// The encrypted unique identifier of the user to be updated.
            /// This is used to find the user in the database. The ID is encrypted for security.
            /// </summary>
            public string EncryptedUserId { get; set; } = string.Empty;

            /// <summary>
            /// The new role to assign to the user.
            /// </summary>
            public string UserRole { get; set; }

            /// <summary>
            /// The number of failed login attempts for the user.
            /// </summary>
            public int FailedLoginCount { get; set; }

            /// <summary>
            /// The date and time until which the user's account is locked out.
            /// </summary>
            public DateTime? LockoutUntil { get; set; }

            /// <summary>
            /// Indicates whether Multi-Factor Authentication (MFA) is enabled for the user.
            /// </summary>
            public bool MfaEnabled { get; set; }

            /// <summary>
            /// The secret key used for Multi-Factor Authentication (MFA).
            /// </summary>
            public string MfaSecret { get; set; }

            /// <summary>
            /// The date and time when the user's account was suspended.
            /// </summary>
            public DateTime? SuspendedAt { get; set; }

            /// <summary>
            /// The reason for the user's account suspension.
            /// </summary>
            public string SuspensionReason { get; set; }

            /// <summary>
            /// The date and time when the user's account was marked as deleted.
            /// </summary>
            public DateTime? DeletedAt { get; set; }
        }

        /// <summary>
        /// Represents the request object for user sign-up.
        /// </summary>
        public class UserSignUpRequest
        {
            /// <summary>
            /// Gets or sets the user's desired username.
            /// </summary>
            public string? Username { get; set; }

            /// <summary>
            /// Gets or sets the user's display name.
            /// </summary>
            public string? DisplayName { get; set; }

            /// <summary>
            /// Gets or sets the user's email address. This field is required.
            /// </summary>
            [Required(ErrorMessage = "Email is required.")]
            [EmailAddress(ErrorMessage = "Invalid email address.")]
            public string Email { get; set; } = string.Empty;

            /// <summary>
            /// Gets or sets the user's chosen password. This field is required.
            /// </summary>
            [Required(ErrorMessage = "Password is required.")]
            [MinLength(8, ErrorMessage = "Password must be at least 8 characters long.")]
            public string Password { get; set; } = string.Empty;

            /// <summary>
            /// Gets or sets the confirmation of the user's chosen password. This field is required and must match the Password.
            /// </summary>
            [Required(ErrorMessage = "Confirm Password is required.")]
            [Compare("Password", ErrorMessage = "Passwords do not match.")]
            public string ConfirmPassword { get; set; } = string.Empty;

            /// <summary>
            /// Gets or sets the user's phone number.
            /// </summary>
            [Phone(ErrorMessage = "Invalid phone number.")]
            public string? PhoneNumber { get; set; }

            /// <summary>
            /// Gets or sets the user's preferred timezone. Defaults to "UTC".
            /// </summary>
            public string? Timezone { get; set; }

            /// <summary>
            /// Gets or sets the user's preferred locale. Defaults to "en-US".
            /// </summary>
            public string? Locale { get; set; }
        }
        #endregion

        #region Initialization
        /// <summary>
        /// Initializes a new instance of the <see cref="UserDataAccess"/> class.
        /// </summary>
        /// <param name="dbContext">The database context.</param>
        /// <param name="passwordHasher">The password hasher.</param>
        /// <param name="logger">The logger.</param>
        /// <param name="configuration">The configuration.</param>
        public UserDataAccess(
            ApplicationDbContext dbContext,
            IPasswordHasher<User> passwordHasher,
            ILogger<UserDataAccess> logger,
            IConfiguration configuration)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _passwordHasher = passwordHasher ?? throw new ArgumentNullException(nameof(passwordHasher));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }
        #endregion

        #region Private
        /// <summary>
        /// Retrieves the Primary Key encryption secret from configuration.
        /// </summary>
        /// <returns>The encryption secret string.</returns>
        /// <exception cref="InvalidOperationException">Thrown if configuration is not set or the secret is missing.</exception>
        private string getPkSecret()
        {
            if (_pkSecret == null)
            {
                lock (_secretLock)
                {
                    if (_pkSecret == null)
                    {
                        string? secret = _configuration.GetSection("Security:DB:PKSecret").Value;
                        if (string.IsNullOrWhiteSpace(secret))
                        {
                            _logger.LogCritical("Required configuration key 'Security:DB:PKSecret' is missing or empty.");
                            throw new InvalidOperationException("Required configuration key 'Security:DB:PKSecret' is missing or empty.");
                        }
                        _pkSecret = secret;
                    }
                }
            }
            return _pkSecret;
        }

        /// <summary>
        /// Helper method to decrypt a user ID string.
        /// </summary>
        /// <param name="encryptedId">The encrypted ID string.</param>
        /// <param name="parameterName">Name of the parameter being decrypted (for logging).</param>
        /// <param name="decryptedId">The decrypted long ID, if successful.</param>
        /// <returns>True if decryption and parsing were successful and ID is positive; false otherwise.</returns>
        private bool tryDecryptId(string? encryptedId, string parameterName, out long decryptedId)
        {
            decryptedId = 0;
            if (string.IsNullOrWhiteSpace(encryptedId))
            {
                _logger.LogWarning("{ParameterName} is null or whitespace.", parameterName);
                return false;
            }

            try
            {
                string decryptedString = new StringCipher().Decrypt(encryptedId, getPkSecret());
                if (long.TryParse(decryptedString, out long id) && id > 0)
                {
                    decryptedId = id;
                    return true;
                }
                _logger.LogWarning("Invalid or non-positive ID after decrypting {ParameterName}. Encrypted value: {EncryptedValue}, Decrypted string: {DecryptedString}", parameterName, encryptedId, decryptedString);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error decrypting {ParameterName}. Encrypted value: {EncryptedValue}", parameterName, encryptedId);
                return false;
            }
        }
        #endregion

        #region Authentication
        /**************************************************************/
        /// <summary>
        /// Authenticates a user based on their email and password.
        /// </summary>
        /// <param name="email">The user's email address.</param>
        /// <param name="password">The user's plaintext password.</param>
        /// <returns>The User object if authentication is successful; otherwise, null.</returns>
        /// <remarks>
        /// This method retrieves the user by email, verifies the password hash,
        /// and updates last login information upon successful authentication.
        /// </remarks>
        public async Task<User?> AuthenticateAsync(string email, string password)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                _logger.LogWarning("AuthenticateAsync: Email or password was null or whitespace.");
                return null;
            }

            var user = await GetByEmailAsync(email); // GetByEmailAsync already normalizes email

            if (user == null)
            {
                _logger.LogWarning("AuthenticateAsync: User not found for email: {Email}", email);
                return null; // User not found
            }

            if (user.DeletedAt != null)
            {
                _logger.LogWarning("AuthenticateAsync: Attempt to authenticate a deleted user: {Email}", email);
                return null; // Do not authenticate deleted users
            }

            if (user.LockoutUntil.HasValue && user.LockoutUntil.Value > DateTime.UtcNow)
            {
                _logger.LogWarning("AuthenticateAsync: User account locked for email: {Email}. Lockout until: {LockoutUntil}", email, user.LockoutUntil.Value);
                return null; // Account is locked
            }

            if (string.IsNullOrWhiteSpace(user.PasswordHash))
            {
                _logger.LogWarning("AuthenticateAsync: User {Email} has no password hash set.", email);
                return null; // No password set for the user
            }

            var passwordVerificationResult = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password);

            if (passwordVerificationResult == PasswordVerificationResult.Success || passwordVerificationResult == PasswordVerificationResult.SuccessRehashNeeded)
            {
                // Password matches. Update last login information.
                // Note: The IP address is not available here directly.
                // If IP is crucial for UpdateLastLoginAsync, it should be passed to this method,
                // or UpdateLastLoginAsync should be called from the handler where HttpContext is available.
                // For now, passing null for IP.
                try
                {
                    // Ensure EncryptedUserId is populated for UpdateLastLoginAsync
                    if (string.IsNullOrWhiteSpace(user.EncryptedUserId))
                    {
                        user.EncryptedUserId = StringCipher.Encrypt(user.UserID.ToString(), getPkSecret());
                    }
                    await UpdateLastLoginAsync(user.EncryptedUserId, DateTime.UtcNow, null /* IP address */);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "AuthenticateAsync: Failed to update last login for user {Email}", email);
                    // Continue with successful authentication despite logging update failure.
                }
                return user; // Authentication successful
            }
            else
            {
                // Password does not match. Increment failed login count.
                user.FailedLoginCount++;
                // Implement lockout logic if desired (e.g., after X failed attempts)
                // if (user.FailedLoginCount >= MAX_FAILED_ATTEMPTS) {
                //    user.LockoutUntil = DateTime.UtcNow.AddMinutes(LOCKOUT_DURATION_MINUTES);
                // }
                try
                {
                    _dbContext.AppUsers.Update(user);
                    await _dbContext.SaveChangesAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "AuthenticateAsync: Failed to update failed login count for user {Email}", email);
                }
                _logger.LogWarning("AuthenticateAsync: Invalid password for user: {Email}", email);
                return null; // Password mismatch
            }
        }
        #endregion

        #region Create
        /**************************************************************/
        /// <summary>
        /// Creates a new user in the database using Entity Framework Core.
        /// </summary>
        /// <param name="user">The user entity to create.</param>
        /// <param name="encryptedCreatorUserId">Optional encrypted ID of the user who is creating this account.</param>
        /// <returns>The encrypted ID of the newly created user, or "Duplicate" if email exists, or null on error.</returns>
        /// <remarks>
        /// This method handles setting creation metadata, normalizing user data, hashing passwords,
        /// checking for duplicate emails, and encrypting the user ID for return.
        /// </remarks>
        public async Task<string?> CreateAsync(User user, string? encryptedCreatorUserId)
        {
            if (user == null) throw new ArgumentNullException(nameof(user));
            if (string.IsNullOrWhiteSpace(user.PrimaryEmail))
                throw new ArgumentException("PrimaryEmail is required.", nameof(user.PrimaryEmail));

            try
            {
                user.CreatedAt = DateTime.UtcNow;
                user.SecurityStamp = Guid.NewGuid().ToString();
                user.FailedLoginCount = 0;

                if (!string.IsNullOrEmpty(encryptedCreatorUserId))
                {
                    if (tryDecryptId(encryptedCreatorUserId, nameof(encryptedCreatorUserId), out long creatorId))
                    {
                        user.CreatedByID = creatorId;
                    }
                    // If decryption fails, warning is logged by TryDecryptId. Decide if creation should proceed.
                    // Current: proceeds without CreatedByID.
                }

                user.CanonicalUsername = user.CanonicalUsername?.ToLowerInvariant();
                user.PrimaryEmail = user.PrimaryEmail.ToLowerInvariant();

                if (!string.IsNullOrWhiteSpace(user.Password))
                {
                    user.PasswordHash = _passwordHasher.HashPassword(user, user.Password);
                    user.PasswordChangedAt = user.CreatedAt;
                    user.Password = null;
                }

                bool emailExists = await _dbContext.AppUsers
                    .AnyAsync(u => u.PrimaryEmail == user.PrimaryEmail && u.DeletedAt == null);

                if (emailExists)
                {
                    _logger.LogWarning("Attempt to create user with existing email: {Email}", user.PrimaryEmail);
                    return "Duplicate";
                }

                _dbContext.AppUsers.Add(user);
                await _dbContext.SaveChangesAsync();

                return StringCipher.Encrypt(user.UserID.ToString(), getPkSecret());
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Database error creating user {Email}.", user.PrimaryEmail);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error creating user {Email}.", user.PrimaryEmail);
                throw;
            }
        }
        #endregion

        #region Read
        /**************************************************************/
        /// <summary>
        /// Retrieves a user by their encrypted unique identifier.
        /// </summary>
        /// <param name="encryptedUserId">The encrypted user ID to look up.</param>
        /// <returns>User object if found and not deleted; null otherwise.</returns>
        public async Task<User?> GetByIdAsync(string? encryptedUserId)
        {
            if (!tryDecryptId(encryptedUserId, nameof(encryptedUserId), out long userId))
            {
                return null; // Logging handled by TryDecryptId
            }

            try
            {
                var user = await _dbContext.AppUsers
                    .SingleOrDefaultAsync(u => u.UserID == userId && u.DeletedAt == null);

                if (user != null)
                {
                    user.SetUserIdInternal(user.UserID);
                    user.EncryptedUserId = StringCipher.Encrypt(user.UserID.ToString(), getPkSecret()); // Ensure outgoing DTO has it
                }
                return user;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching user by decrypted ID {UserId} (Encrypted: {EncryptedUserId}).", userId, encryptedUserId);
                throw;
            }
        }

        /**************************************************************/
        /// <summary>
        /// Retrieves a user by their email address.
        /// </summary>
        /// <param name="email">The email address to look up.</param>
        /// <returns>User object if found and not deleted; null otherwise.</returns>
        public async Task<User?> GetByEmailAsync(string email)
        {
            if (string.IsNullOrWhiteSpace(email)) return null;
            var normalizedEmail = email.ToLowerInvariant();

            try
            {
                var user = await _dbContext.AppUsers
                    .SingleOrDefaultAsync(u => u.PrimaryEmail == normalizedEmail && u.DeletedAt == null);

                if (user != null)
                {
                    user.SetUserIdInternal(user.UserID);
                    user.EncryptedUserId = StringCipher.Encrypt(user.UserID.ToString(), getPkSecret());
                }
                return user;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching user by email {Email}.", email);
                throw;
            }
        }

        /**************************************************************/
        /// <summary>
        /// Retrieves a paginated list of all users.
        /// </summary>
        /// <param name="includeDeleted">When true, includes soft-deleted users in the results.</param>
        /// <param name="skip">Number of users to skip for pagination.</param>
        /// <param name="take">Maximum number of users to return (capped at 1000).</param>
        /// <returns>A collection of User objects.</returns>
        public async Task<IEnumerable<User>> GetAllAsync(bool includeDeleted = false, int skip = 0, int take = 100)
        {
            skip = Math.Max(0, skip);
            take = Math.Min(Math.Max(1, take), 1000);

            try
            {
                var query = _dbContext.AppUsers.AsQueryable();
                if (!includeDeleted)
                {
                    query = query.Where(u => u.DeletedAt == null);
                }

                var users = await query
                    .OrderBy(u => u.UserID)
                    .Skip(skip)
                    .Take(take)
                    .ToListAsync();

                foreach (var user in users)
                {
                    user.SetUserIdInternal(user.UserID);
                    user.EncryptedUserId = StringCipher.Encrypt(user.UserID.ToString(), getPkSecret());
                }
                return users;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching all users.");
                throw;
            }
        }
        #endregion

        #region Update
        /**************************************************************/
        /// <summary>
        /// Updates allowed fields of a user entity. The user to update is identified by `user.EncryptedUserId`.
        /// </summary>
        /// <param name="user">The user entity with updated values. Must include `EncryptedUserId`.</param>
        /// <param name="encryptedUpdaterUserId">Encrypted ID of the user making the update.</param>
        /// <returns>True if the user was found and updated; false otherwise.</returns>
        public async Task<bool> UpdateAsync(User user, string? encryptedUpdaterUserId)
        {
            if (user == null) throw new ArgumentNullException(nameof(user));

            if (!tryDecryptId(user.EncryptedUserId, $"{nameof(user)}.{nameof(user.EncryptedUserId)}", out long userIdToUpdate))
            {
                _logger.LogWarning("UpdateAsync: User to update has invalid or missing EncryptedUserId.");
                return false;
            }
            if (!tryDecryptId(encryptedUpdaterUserId, nameof(encryptedUpdaterUserId), out long updaterUserId))
            {
                _logger.LogWarning("UpdateAsync: Updater user ID is invalid or missing.");
                return false;
            }

            try
            {
                var existingUser = await _dbContext.AppUsers
                    .SingleOrDefaultAsync(u => u.UserID == userIdToUpdate && u.DeletedAt == null);

                if (existingUser == null)
                {
                    _logger.LogWarning("User with decrypted ID {UserIdToUpdate} not found or deleted, cannot update.", userIdToUpdate);
                    return false;
                }

                // Preserve critical fields from existingUser if not meant to be updated by 'user' DTO
                user.UserID = existingUser.UserID; // Ensure UserID is correct
                user.CreatedAt = existingUser.CreatedAt;
                user.CreatedByID = existingUser.CreatedByID;
                user.PasswordHash = existingUser.PasswordHash; // Password changes via RotatePasswordAsync
                user.SecurityStamp = existingUser.SecurityStamp; // SecurityStamp changes via RotatePasswordAsync
                // etc. for fields not typically part of a general update DTO

                // Update audit fields from parameters/current state
                user.UpdatedAt = DateTime.UtcNow;
                user.UpdatedBy = updaterUserId;
                user.CanonicalUsername = user.CanonicalUsername?.ToLowerInvariant();

                _dbContext.Entry(existingUser).CurrentValues.SetValues(user);
                // If 'user' is a partial DTO, explicitly map fields:
                // existingUser.DisplayName = user.DisplayName;
                // existingUser.PhoneNumber = user.PhoneNumber; ...

                await _dbContext.SaveChangesAsync();
                return true;
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogError(ex, "Concurrency error updating user {UserIdToUpdate}.", userIdToUpdate);
                return false;
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Database error updating user {UserIdToUpdate}.", userIdToUpdate);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating user {UserIdToUpdate}.", userIdToUpdate);
                throw;
            }
        }

        /**************************************************************/
        /// <summary>
        /// Updates user-editable profile fields. User to update is identified by `profile.EncryptedUserId`.
        /// </summary>
        /// <param name="profile">User profile data to update. Must contain `EncryptedUserId`.</param>
        /// <param name="encryptedUpdaterUserId">Encrypted ID of the user making the update.</param>
        /// <returns>True if the user was found and updated; false otherwise.</returns>
        public async Task<bool> UpdateProfileAsync(User profile, string? encryptedUpdaterUserId)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));

            if (!tryDecryptId(profile.EncryptedUserId, $"{nameof(profile)}.{nameof(profile.EncryptedUserId)}", out long userIdToUpdate))
            {
                _logger.LogWarning("UpdateProfileAsync: User to update has invalid or missing EncryptedUserId.");
                return false;
            }
            if (!tryDecryptId(encryptedUpdaterUserId, nameof(encryptedUpdaterUserId), out long updaterUserId))
            {
                _logger.LogWarning("UpdateProfileAsync: Updater user ID is invalid or missing.");
                return false;
            }

            // Security check: Ensure the updater is the user themselves or an authorized admin.
            // This example assumes self-update or admin has separate authorization.
            // if (userIdToUpdate != updaterUserId && !IsAdmin(updaterUserId)) return false;


            try
            {
                var user = await _dbContext.AppUsers
                    .SingleOrDefaultAsync(u => u.UserID == userIdToUpdate && u.DeletedAt == null);

                if (user == null)
                {
                    _logger.LogWarning("User with decrypted ID {UserIdToUpdate} not found for profile update.", userIdToUpdate);
                    return false;
                }

                user.PhoneNumber = profile.PhoneNumber;
                user.DisplayName = profile.DisplayName;
                user.Timezone = profile.Timezone;
                user.Locale = profile.Locale;
                user.NotificationSettings = profile.NotificationSettings;
                user.UiTheme = profile.UiTheme;

                user.UpdatedAt = DateTime.UtcNow;
                user.UpdatedBy = updaterUserId;

                await _dbContext.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating profile for user (decrypted ID) {UserIdToUpdate}.", userIdToUpdate);
                throw;
            }
        }

        /**************************************************************/
        /// <summary>
        /// Updates admin-controlled user fields. The user to modify is identified by `adminUpdateData.EncryptedUserId`.
        /// </summary>
        /// <param name="adminUpdateData">Admin-editable user data. `EncryptedUserId` property within this object identifies the target user.</param>
        /// <param name="encryptedUpdaterAdminId">Encrypted ID of the admin making the update.</param>
        /// <returns>True if the user was found and updated; false otherwise.</returns>
        public async Task<bool> UpdateAdminAsync(AdminUserUpdate adminUpdateData, string? encryptedUpdaterAdminId)
        {
            if (adminUpdateData == null) throw new ArgumentNullException(nameof(adminUpdateData));
            if (string.IsNullOrWhiteSpace(adminUpdateData.EncryptedUserId))
                throw new ArgumentException("Valid encrypted target user ID is required in adminUpdateData.", nameof(adminUpdateData.EncryptedUserId));

            if (!tryDecryptId(encryptedUpdaterAdminId, nameof(encryptedUpdaterAdminId), out long updaterAdminId))
            {
                _logger.LogWarning("UpdateAdminAsync: Updater admin ID is invalid or missing.");
                return false;
            }

            if (!tryDecryptId(adminUpdateData.EncryptedUserId, nameof(adminUpdateData.EncryptedUserId), out long targetUserId))
            {
                _logger.LogWarning("UpdateAdminAsync: Target user ID is invalid or missing.");
                return false;
            }

            // Authorization check: Ensure updaterAdminId corresponds to an actual admin role.
            // This logic would typically be in a service layer or use ASP.NET Core authorization.
            // For example: if (!await IsUserAdminAsync(updaterAdminId)) { _logger.LogWarning(...); return false; }


            try
            {
                // targetUserId is the decrypted long ID of the target user
                var user = await _dbContext.AppUsers
                    .SingleOrDefaultAsync(u => u.UserID == targetUserId); // Can update deleted users

                if (user == null)
                {
                    _logger.LogWarning("User with ID {TargetUserId} not found for admin update.", targetUserId);
                    return false;
                }

                user.UserRole = adminUpdateData.UserRole;
                user.FailedLoginCount = adminUpdateData.FailedLoginCount;
                user.LockoutUntil = adminUpdateData.LockoutUntil;
                user.MfaEnabled = adminUpdateData.MfaEnabled;
                user.MfaSecret = adminUpdateData.MfaSecret; // Be cautious with direct secret updates
                user.SuspendedAt = adminUpdateData.SuspendedAt;
                user.SuspensionReason = adminUpdateData.SuspensionReason;
                user.DeletedAt = adminUpdateData.DeletedAt;

                user.UpdatedAt = DateTime.UtcNow;
                user.UpdatedBy = updaterAdminId;

                await _dbContext.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error performing admin update for target user {TargetUserId}.", targetUserId);
                throw;
            }
        }

        /**************************************************************/
        /// <summary>
        /// Updates a user's password and security stamp.
        /// </summary>
        /// <param name="encryptedTargetUserId">Encrypted ID of the user whose password to change.</param>
        /// <param name="newPlainPassword">New plaintext password to hash and store.</param>
        /// <param name="encryptedUpdaterUserId">Encrypted ID of the user making the update.</param>
        /// <returns>True if the user was found and password updated; false otherwise.</returns>
        public async Task<bool> RotatePasswordAsync(string? encryptedTargetUserId, string newPlainPassword, string? encryptedUpdaterUserId)
        {
            if (string.IsNullOrWhiteSpace(newPlainPassword))
                throw new ArgumentException("Password cannot be empty.", nameof(newPlainPassword));

            if (!tryDecryptId(encryptedTargetUserId, nameof(encryptedTargetUserId), out long targetUserId))
            {
                return false;
            }
            if (!tryDecryptId(encryptedUpdaterUserId, nameof(encryptedUpdaterUserId), out long updaterUserId))
            {
                return false;
            }

            // Authorization: e.g., targetUserId == updaterUserId (self-change) OR IsAdmin(updaterUserId)
            // if (targetUserId != updaterUserId && !await IsUserAdminAsync(updaterUserId)) { _logger.LogWarning(...); return false; }

            try
            {
                var user = await _dbContext.AppUsers
                    .SingleOrDefaultAsync(u => u.UserID == targetUserId && u.DeletedAt == null);

                if (user == null)
                {
                    _logger.LogWarning("User for password rotation not found or deleted. Decrypted Target ID: {TargetUserId}", targetUserId);
                    return false;
                }

                user.PasswordHash = _passwordHasher.HashPassword(user, newPlainPassword);
                user.PasswordChangedAt = DateTime.UtcNow;
                user.SecurityStamp = Guid.NewGuid().ToString();

                user.UpdatedAt = DateTime.UtcNow;
                user.UpdatedBy = updaterUserId;

                await _dbContext.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error rotating password for target user (decrypted ID) {TargetUserId}.", targetUserId);
                throw;
            }
        }
        #endregion

        #region Delete & Login Tracking
        /**************************************************************/
        /// <summary>
        /// Soft-deletes a user by setting their DeletedAt timestamp.
        /// </summary>
        /// <param name="encryptedTargetUserId">Encrypted ID of the user to delete.</param>
        /// <param name="encryptedDeleterUserId">Encrypted ID of the user performing the deletion.</param>
        /// <returns>True if the user was found and soft-deleted; false otherwise.</returns>
        public async Task<bool> DeleteAsync(string? encryptedTargetUserId, string? encryptedDeleterUserId)
        {
            if (!tryDecryptId(encryptedTargetUserId, nameof(encryptedTargetUserId), out long targetUserId))
            {
                return false;
            }
            if (!tryDecryptId(encryptedDeleterUserId, nameof(encryptedDeleterUserId), out long deleterUserId))
            {
                return false;
            }

            // Authorization: e.g., IsAdmin(deleterUserId)
            // if (!await IsUserAdminAsync(deleterUserId)) { _logger.LogWarning(...); return false; }

            try
            {
                var user = await _dbContext.AppUsers
                    .SingleOrDefaultAsync(u => u.UserID == targetUserId && u.DeletedAt == null);

                if (user == null)
                {
                    _logger.LogWarning("User for deletion not found or already deleted. Decrypted Target ID: {TargetUserId}", targetUserId);
                    return false;
                }

                user.DeletedAt = DateTime.UtcNow;
                user.UpdatedAt = DateTime.UtcNow;
                user.UpdatedBy = deleterUserId;

                await _dbContext.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error soft-deleting user (decrypted ID) {TargetUserId}.", targetUserId);
                throw;
            }
        }

        /**************************************************************/
        /// <summary>
        /// Records a successful login and updates related security fields.
        /// </summary>
        /// <param name="encryptedUserId">Encrypted ID of the user who logged in.</param>
        /// <param name="loginTime">Timestamp of the login attempt.</param>
        /// <param name="ipAddress">Optional IP address of the login request.</param>
        /// <returns>True if the user was found and updated; false otherwise.</returns>
        public async Task<bool> UpdateLastLoginAsync(string? encryptedUserId, DateTime loginTime, string? ipAddress)
        {
            if (!tryDecryptId(encryptedUserId, nameof(encryptedUserId), out long userId))
            {
                return false;
            }

            try
            {
                var user = await _dbContext.AppUsers
                    .SingleOrDefaultAsync(u => u.UserID == userId); // Find user even if deleted for login tracking? Or add u.DeletedAt == null

                if (user == null)
                {
                    _logger.LogWarning("User for login update not found. Decrypted ID: {UserId}", userId);
                    return false;
                }

                // Business rule: Should a deleted user's login be tracked or prevented?
                if (user.DeletedAt != null)
                {
                    _logger.LogWarning("Attempt to update last login for a deleted user. Decrypted ID: {UserId}", userId);
                    // Depending on policy, might return false or proceed.
                    // For basic auth, we might allow it if AuthenticateAsync allows it, then the handler decides.
                    // However, AuthenticateAsync now blocks deleted users.
                }

                user.LastLoginAt = loginTime;
                user.LastActivityAt = loginTime;
                user.LastIpAddress = ipAddress;
                user.FailedLoginCount = 0;
                user.LockoutUntil = null;

                await _dbContext.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating last login for user (decrypted ID) {UserId}.", userId);
                throw;
            }
        }

        /**************************************************************/
        /// <summary>
        /// Creates a new user account via self-registration.
        /// </summary>
        /// <param name="request">User-provided registration information.</param>
        /// <returns>Encrypted ID of the new user if successful; "Duplicate" if email exists, or null on error.</returns>
        public async Task<string?> SignUpAsync(UserSignUpRequest request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));
            if (string.IsNullOrWhiteSpace(request.Email))
                throw new ArgumentException("Email is required.", nameof(request.Email));
            if (string.IsNullOrWhiteSpace(request.Password))
                throw new ArgumentException("Password is required.", nameof(request.Password));
            if (request.Password != request.ConfirmPassword)
                throw new ArgumentException("Passwords do not match.", nameof(request.ConfirmPassword));

            var user = new User
            {
                CanonicalUsername = request.Username?.ToLowerInvariant(),
                DisplayName = request.DisplayName,
                PrimaryEmail = request.Email.ToLowerInvariant(),
                Password = request.Password,
                PhoneNumber = request.PhoneNumber,
                Timezone = request.Timezone ?? "UTC",
                Locale = request.Locale ?? "en-US",
                NotificationSettings = "default",
                UiTheme = "light",
                UserRole = "User"
            };

            return await CreateAsync(user, encryptedCreatorUserId: null);
        }
        #endregion
    }
}
