using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Dapper;
using MedRecPro.Models;
using MedRecPro.Helpers; // For proper hashing helper
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.DataProtection;

namespace MedRecPro.DataAccess
{
    #region dtos
    /// <summary>
    /// DTO for fields a user may update on their own profile.
    /// </summary>
    public class UserProfileUpdate
    {
        public long UserId { get; set; }
        public string PhoneNumber { get; set; }
        public string DisplayName { get; set; }
        public string Timezone { get; set; }
        public string Locale { get; set; }
        public string NotificationSettings { get; set; }
        public string UiTheme { get; set; }
    }

    /// <summary>
    /// DTO for fields only an admin may update.
    /// </summary>
    public class AdminUserUpdate
    {
        public long UserId { get; set; }
        public string UserRole { get; set; }
        public int FailedLoginCount { get; set; }
        public DateTime? LockoutUntil { get; set; }
        public bool MfaEnabled { get; set; }
        public string MfaSecret { get; set; }
        public DateTime? SuspendedAt { get; set; }
        public string SuspensionReason { get; set; }
        public DateTime? DeletedAt { get; set; }
    }

    /// <summary>
    /// DTO for self-service user signup.
    /// </summary>
    public class UserSignUpRequest
    {
        public string Username { get; set; }           // will become CanonicalUsername
        public string DisplayName { get; set; }
        public string Email { get; set; }
        public string Password { get; set; }
        public string ConfirmPassword { get; set; }
        public string? PhoneNumber { get; set; }
        public string? Timezone { get; set; }
        public string? Locale { get; set; }
    }
    #endregion dtos

    /// <summary>
    /// Interface defining data access operations for User entities.
    /// </summary>
    public interface IUserDataAccess
    {
        #region user_crud_operations
        Task<string?> CreateAsync(User user, string? encryptedCreatorUserId);
        Task<User?> GetByIdAsync(long userId);
        Task<User?> GetByEmailAsync(string email);
        Task<IEnumerable<User>> GetAllAsync(bool includeDeleted = false, int skip = 0, int take = 100);

        // General update (rarely used—prefers the specific methods below)
        Task<bool> UpdateAsync(User user, long updaterUserId);

        // Soft-delete
        Task<bool> DeleteAsync(long userId, long deleterUserId);
        #endregion user_crud_operations

        #region specialized_operations
        // Last-login tracking
        Task<bool> UpdateLastLoginAsync(long userId, DateTime loginTime, string? ipAddress);

        // Self-service profile updates
        Task<bool> UpdateProfileAsync(UserProfileUpdate profile, long updaterUserId);

        // Admin-only updates
        Task<bool> UpdateAdminAsync(AdminUserUpdate adminUpdate, long updaterUserId);

        // Rotate a user's password
        Task<bool> RotatePasswordAsync(long userId, string newPlainPassword, long updaterUserId);

        /// <summary>
        /// Creates a brand-new user via self-signup.
        /// </summary>
        /// <param name="request">All fields the user supplies at signup.</param>
        /// <returns>New UserID, or 0 if signup failed (e.g. duplicate email).</returns>
        Task<string?> SignUpAsync(UserSignUpRequest request);
        #endregion specialized_operations
    }

    /// <summary>
    /// Data Access Layer implementation for User entities using Dapper.
    /// </summary>
    public class UserDataAccess : IUserDataAccess
    {
        #region implementation
        private readonly string _connectionString;
        private readonly IPasswordHasher<User> _passwordHasher;
        private readonly ILogger<UserDataAccess> _logger;
        private readonly IConfiguration _configuration;

        // Store the encryption secret retrieved from configuration
        private static string? _pkSecret;
        // Static lock for thread-safe initialization of the secret
        private static readonly object _secretLock = new object();

        public UserDataAccess(
            IConfiguration configuration,
            IPasswordHasher<User> passwordHasher,
            ILogger<UserDataAccess> logger
            )
        {
            _configuration = configuration
                ?? throw new ArgumentNullException(nameof(configuration));
            _connectionString = configuration
                .GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is missing.");
            _passwordHasher = passwordHasher
                ?? throw new ArgumentNullException(nameof(passwordHasher));
            _logger = logger
                ?? throw new ArgumentNullException(nameof(logger));
        }

        /**************************************************************/
        /// <summary>
        /// Retrieves the Primary Key encryption secret from configuration.
        /// </summary>
        /// <returns>The encryption secret string.</returns>
        /// <exception cref="InvalidOperationException">Thrown if configuration is not set or the secret is missing.</exception>
        private string getPkSecret()
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

                        // Retrieve the secret from configuration
                        string? secret = _configuration.GetSection("Security:DB:PKSecret").Value; // Or "Security__DB__PKSecret" depending on config provider

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

        /// <summary>
        /// Creates a new database connection using the configured connection string.
        /// </summary>
        /// <returns>An open database connection ready for use.</returns>
        private IDbConnection createConnection()
        {
            var conn = new SqlConnection(_connectionString);
            // Optionally: conn.Open();
            return conn;
        }
        #endregion implementation

        #region Create
        /**************************************************************/
        /// <summary>
        /// Creates a new user in the database.
        /// </summary>
        /// <param name="user">The user entity to create. Must have a valid PrimaryEmail.</param>
        /// <param name="encryptedCreatorUserId">Optional ID of the user who is creating this account. Null for self-signup.</param>
        /// <returns>The new UserID if successful, 0 if a duplicate email was detected.</returns>
        /// <example>
        /// var newUser = new User { PrimaryEmail = "user@example.com", ... };
        /// var newId = await userDataAccess.CreateAsync(newUser, adminUserId);
        /// </example>
        /// <remarks>
        /// This method handles password hashing, email normalization, and security stamp generation.
        /// If creation fails due to a unique constraint violation (e.g., email already exists),
        /// it returns 0 instead of throwing an exception.
        /// </remarks>
        public async Task<string?> CreateAsync(User user, string? encryptedCreatorUserId)
        {
            #region implementation
      
            if (user == null) throw new ArgumentNullException(nameof(user));
            if (string.IsNullOrWhiteSpace(user.PrimaryEmail))
                throw new ArgumentException("PrimaryEmail is required.", nameof(user.PrimaryEmail));

            user.CreatedAt = DateTime.UtcNow;
           long createdByID = 0;
            user.SecurityStamp = Guid.NewGuid();
            user.FailedLoginCount = 0;

            Int64.TryParse(new StringCipher().Decrypt(encryptedCreatorUserId, getPkSecret()), out createdByID);

            user.CreatedByID = createdByID;

            // Normalize
            user.CanonicalUsername = user.CanonicalUsername?.ToLowerInvariant();
            user.PrimaryEmail = user.PrimaryEmail.ToLowerInvariant();

            // Hash password if passed in plaintext
            if (!string.IsNullOrWhiteSpace(user.Password))
            {
                user.PasswordHash = _passwordHasher.HashPassword(user, user.Password);
                user.PasswordChangedAt = user.CreatedAt;
                user.Password = null; // Clear plaintext password after hashing
            }

            const string sql = @"
                INSERT INTO dbo.Users (
                    CanonicalUsername, PhoneNumber, DisplayName, PrimaryEmail, EmailVerifiedAt,
                    PasswordHash, PasswordChangedAt, FailedLoginCount, LockoutUntil, MfaEnabled, MfaSecret, SecurityStamp,
                    UserRole, UserPermissions, UserFollowing,
                    Timezone, Locale, NotificationSettings, UiTheme,
                    TosVersionAccepted, TosAcceptedAt, TosMarketingOptIn, TosEmailNotification,
                    CreatedAt, CreatedByID
                )
                OUTPUT INSERTED.UserID
                VALUES (
                    @CanonicalUsername, @PhoneNumber, @DisplayName, @PrimaryEmail, @EmailVerifiedAt,
                    @PasswordHash, @PasswordChangedAt, @FailedLoginCount, @LockoutUntil, @MfaEnabled, @MfaSecret, @SecurityStamp,
                    @UserRole, @UserPermissions, @UserFollowing,
                    @Timezone, @Locale, @NotificationSettings, @UiTheme,
                    @TosVersionAccepted, @TosAcceptedAt, @TosMarketingOptIn, @TosEmailNotification,
                    @CreatedAt, @CreatedByID
                );";

            try
            {
                using (var conn = createConnection())
                {
                    var newId = await conn.ExecuteScalarAsync<long>(sql, user);
                    user.SetUserIdInternal(newId); // Update the in-memory object with the new ID
                    return StringCipher.Encrypt(newId.ToString(), getPkSecret());
                }
            }
            catch (SqlException ex) when (ex.Number == 2601 || ex.Number == 2627)
            {
                // These error codes indicate unique constraint violations
                _logger.LogError(ex, "Unique constraint violation creating user ({Email}).", user.PrimaryEmail);
                return $"{ex.Number}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error creating user ({Email}).", user.PrimaryEmail);
                throw;
            }
            #endregion implementation
        }
        #endregion

        #region Read
        #region implementation
        // Common column selection SQL fragment used in all SELECT queries
        private const string _baseSelect = @"
            UserID, CanonicalUsername, PhoneNumber, DisplayName, PrimaryEmail, EmailVerifiedAt,
            PasswordHash, PasswordChangedAt, FailedLoginCount, LockoutUntil, MfaEnabled, MfaSecret, SecurityStamp,
            UserRole, UserPermissions, UserFollowing,
            Timezone, Locale, NotificationSettings, UiTheme,
            TosVersionAccepted, TosAcceptedAt, TosMarketingOptIn, TosEmailNotification,
            CreatedAt, CreatedByID, UpdatedAt, UpdatedBy, DeletedAt,
            SuspendedAt, SuspensionReason, LastLoginAt, LastActivityAt, LastIpAddress";
        #endregion implementation

        /**************************************************************/
        /// <summary>
        /// Retrieves a user by their unique identifier.
        /// </summary>
        /// <param name="userId">The user ID to look up.</param>
        /// <returns>User object if found and not deleted; null otherwise.</returns>
        /// <remarks>
        /// This method only returns non-deleted users. To retrieve deleted users,
        /// use the GetAllAsync method with includeDeleted=true.
        /// </remarks>
        public async Task<User?> GetByIdAsync(long userId)
        {
            #region implementation
            if (userId <= 0) return null;

            var sql = $@"
                SELECT {_baseSelect}
                FROM dbo.Users
                WHERE UserID = @UserId AND DeletedAt IS NULL;";

            try
            {
                using (var conn = createConnection())
                {
                    var u = await conn.QuerySingleOrDefaultAsync<User>(sql, new { UserId = userId });
                    if (u != null) u.SetUserIdInternal(u.UserID); // Ensure ID is properly set in object
                    return u;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching user by ID {UserId}.", userId);
                throw;
            }
            #endregion implementation
        }

        /**************************************************************/
        /// <summary>
        /// Retrieves a user by their email address.
        /// </summary>
        /// <param name="email">The email address to look up.</param>
        /// <returns>User object if found and not deleted; null otherwise.</returns>
        /// <remarks>
        /// Email lookup is case-insensitive. The provided email will be normalized
        /// to lowercase before querying the database.
        /// </remarks>
        public async Task<User?> GetByEmailAsync(string email)
        {
            #region implementation
            if (string.IsNullOrWhiteSpace(email)) return null;
            var normalized = email.ToLowerInvariant(); // Ensure case-insensitive matching

            var sql = $@"
                SELECT {_baseSelect}
                FROM dbo.Users
                WHERE PrimaryEmail = @Email AND DeletedAt IS NULL;";

            try
            {
                using (var conn = createConnection())
                {
                    var u = await conn.QuerySingleOrDefaultAsync<User>(sql, new { Email = normalized });
                    if (u != null) u.SetUserIdInternal(u.UserID); // Ensure ID is properly set in object
                    return u;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching user by email {Email}.", email);
                throw;
            }
            #endregion implementation
        }

        /**************************************************************/
        /// <summary>
        /// Retrieves a paginated list of all users.
        /// </summary>
        /// <param name="includeDeleted">When true, includes soft-deleted users in the results.</param>
        /// <param name="skip">Number of users to skip for pagination.</param>
        /// <param name="take">Maximum number of users to return (capped at 1000).</param>
        /// <returns>A collection of User objects.</returns>
        /// <example>
        /// // Get first page of active users (100 per page)
        /// var page1 = await userDataAccess.GetAllAsync(false, 0, 100);
        /// 
        /// // Get second page of active users
        /// var page2 = await userDataAccess.GetAllAsync(false, 100, 100);
        /// </example>
        public async Task<IEnumerable<User>> GetAllAsync(bool includeDeleted = false, int skip = 0, int take = 100)
        {
            #region implementation
            skip = Math.Max(0, skip); // Ensure non-negative skip
            take = Math.Min(Math.Max(1, take), 1000); // Ensure take is between 1 and 1000

            var where = includeDeleted ? "" : "WHERE DeletedAt IS NULL";
            var sql = $@"
                SELECT {_baseSelect}
                FROM dbo.Users
                {where}
                ORDER BY UserID
                OFFSET @Skip ROWS FETCH NEXT @Take ROWS ONLY;";

            try
            {
                using (var conn = createConnection())
                {
                    var list = await conn.QueryAsync<User>(sql, new { Skip = skip, Take = take });
                    foreach (var u in list) u.SetUserIdInternal(u.UserID); // Ensure IDs are properly set
                    return list;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching all users.");
                throw;
            }
            #endregion implementation
        }
        #endregion

        #region Update
        /**************************************************************/
        /// <summary>
        /// Updates all allowed fields of a user entity.
        /// </summary>
        /// <param name="user">The user entity with updated values.</param>
        /// <param name="updaterUserId">ID of the user making the update.</param>
        /// <returns>True if the user was found and updated; false otherwise.</returns>
        /// <remarks>
        /// This is a catch-all update method. For specific updates, prefer using
        /// UpdateProfileAsync, UpdateAdminAsync, or RotatePasswordAsync instead.
        /// </remarks>
        public async Task<bool> UpdateAsync(User user, long updaterUserId)
        {
            #region implementation
            // Catch-all method—prefer using the two specific ones below.
            user.UpdatedAt = DateTime.UtcNow;
            user.UpdatedBy = updaterUserId;
            user.CanonicalUsername = user.CanonicalUsername?.ToLowerInvariant(); // Ensure normalized

            const string sql = @"
                UPDATE dbo.Users SET
                    CanonicalUsername=@CanonicalUsername,
                    PhoneNumber=@PhoneNumber,
                    DisplayName=@DisplayName,
                    PrimaryEmail=@PrimaryEmail,
                    EmailVerifiedAt=@EmailVerifiedAt,
                    PasswordChangedAt=@PasswordChangedAt,
                    FailedLoginCount=@FailedLoginCount,
                    LockoutUntil=@LockoutUntil,
                    MfaEnabled=@MfaEnabled,
                    MfaSecret=@MfaSecret,
                    UserRole=@UserRole,
                    UserPermissions=@UserPermissions,
                    UserFollowing=@UserFollowing,
                    Timezone=@Timezone,
                    Locale=@Locale,
                    NotificationSettings=@NotificationSettings,
                    UiTheme=@UiTheme,
                    TosVersionAccepted=@TosVersionAccepted,
                    TosAcceptedAt=@TosAcceptedAt,
                    TosMarketingOptIn=@TosMarketingOptIn,
                    TosEmailNotification=@TosEmailNotification,
                    SuspendedAt=@SuspendedAt,
                    SuspensionReason=@SuspensionReason,
                    UpdatedAt=@UpdatedAt,
                    UpdatedBy=@UpdatedBy
                WHERE UserID=@UserID AND DeletedAt IS NULL;";

            try
            {
                using (var conn = createConnection())
                {
                    var rows = await conn.ExecuteAsync(sql, user);
                    return rows > 0; // True if a user was updated
                }
            }
            catch (SqlException ex) when (ex.Number == 2601 || ex.Number == 2627)
            {
                // These error codes indicate unique constraint violations
                _logger.LogError(ex, "Unique constraint violation updating user {UserId}.", user.UserID);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating user {UserId}.", user.UserID);
                throw;
            }
            #endregion implementation
        }

        /**************************************************************/
        /// <summary>
        /// Updates user-editable profile fields.
        /// </summary>
        /// <param name="profile">User profile data to update.</param>
        /// <param name="updaterUserId">ID of the user making the update (for audit).</param>
        /// <returns>True if the user was found and updated; false otherwise.</returns>
        /// <remarks>
        /// This method is designed for self-service profile updates by users.
        /// Only non-sensitive fields like display name and phone number can be updated.
        /// </remarks>
        public async Task<bool> UpdateProfileAsync(UserProfileUpdate profile, long updaterUserId)
        {
            #region implementation
            const string sql = @"
                UPDATE dbo.Users SET
                    PhoneNumber = @PhoneNumber,
                    DisplayName = @DisplayName,
                    Timezone = @Timezone,
                    Locale = @Locale,
                    NotificationSettings = @NotificationSettings,
                    UiTheme = @UiTheme,
                    UpdatedAt = @Now,
                    UpdatedBy = @Updater
                WHERE UserID = @UserId AND DeletedAt IS NULL;";

            try
            {
                using (var conn = createConnection())
                {
                    var rows = await conn.ExecuteAsync(sql, new
                    {
                        profile.UserId,
                        profile.PhoneNumber,
                        profile.DisplayName,
                        profile.Timezone,
                        profile.Locale,
                        profile.NotificationSettings,
                        profile.UiTheme,
                        Now = DateTime.UtcNow,
                        Updater = updaterUserId
                    });
                    return rows > 0; // True if a user was updated
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating profile for user {UserId}.", profile.UserId);
                throw;
            }
            #endregion implementation
        }

        /**************************************************************/
        /// <summary>
        /// Updates admin-controlled user fields.
        /// </summary>
        /// <param name="admin">Admin-editable user data to update.</param>
        /// <param name="updaterUserId">ID of the admin making the update (for audit).</param>
        /// <returns>True if the user was found and updated; false otherwise.</returns>
        /// <remarks>
        /// This method is designed for administrators to manage user accounts.
        /// It can modify security-sensitive fields like roles, suspension status, and MFA settings.
        /// Unlike other update methods, this one can update deleted users too.
        /// </remarks>
        public async Task<bool> UpdateAdminAsync(AdminUserUpdate admin, long updaterUserId)
        {
            #region implementation
            const string sql = @"
                UPDATE dbo.Users SET
                    UserRole = @UserRole,
                    FailedLoginCount = @FailedLoginCount,
                    LockoutUntil = @LockoutUntil,
                    MfaEnabled = @MfaEnabled,
                    MfaSecret = @MfaSecret,
                    SuspendedAt = @SuspendedAt,
                    SuspensionReason = @SuspensionReason,
                    DeletedAt = @DeletedAt,
                    UpdatedAt = @Now,
                    UpdatedBy = @Updater
                WHERE UserID = @UserId;";

            try
            {
                using (var conn = createConnection())
                {
                    var rows = await conn.ExecuteAsync(sql, new
                    {
                        admin.UserId,
                        admin.UserRole,
                        admin.FailedLoginCount,
                        admin.LockoutUntil,
                        admin.MfaEnabled,
                        admin.MfaSecret,
                        admin.SuspendedAt,
                        admin.SuspensionReason,
                        admin.DeletedAt,
                        Now = DateTime.UtcNow,
                        Updater = updaterUserId
                    });
                    return rows > 0; // True if a user was updated
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error performing admin update for user {UserId}.", admin.UserId);
                throw;
            }
            #endregion implementation
        }

        /**************************************************************/
        /// <summary>
        /// Updates a user's password and security stamp.
        /// </summary>
        /// <param name="userId">ID of the user whose password to change.</param>
        /// <param name="newPlainPassword">New plaintext password to hash and store.</param>
        /// <param name="updaterUserId">ID of the user making the update (for audit).</param>
        /// <returns>True if the user was found and password updated; false otherwise.</returns>
        /// <remarks>
        /// This method:
        /// 1. Hashes the plaintext password with ASP.NET Identity's password hasher
        /// 2. Generates a new security stamp (invalidating existing tokens)
        /// 3. Records when the password was changed
        /// </remarks>
        public async Task<bool> RotatePasswordAsync(long userId, string newPlainPassword, long updaterUserId)
        {
            #region implementation
            var newHash = _passwordHasher.HashPassword(null, newPlainPassword);
            var newStamp = Guid.NewGuid();
            var now = DateTime.UtcNow;

            const string sql = @"
                UPDATE dbo.Users SET
                    PasswordHash = @Hash,
                    PasswordChangedAt = @Now,
                    SecurityStamp = @Stamp,
                    UpdatedAt = @Now,
                    UpdatedBy = @Updater
                WHERE UserID = @UserId AND DeletedAt IS NULL;";

            try
            {
                using (var conn = createConnection())
                {
                    var rows = await conn.ExecuteAsync(sql, new
                    {
                        Hash = newHash,
                        Stamp = newStamp,
                        Now = now,
                        Updater = updaterUserId,
                        UserId = userId
                    });
                    return rows > 0; // True if a user was updated
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error rotating password for user {UserId}.", userId);
                throw;
            }
            #endregion implementation
        }
        #endregion

        #region Delete & Login Tracking
        /**************************************************************/
        /// <summary>
        /// Soft-deletes a user by setting their DeletedAt timestamp.
        /// </summary>
        /// <param name="userId">ID of the user to delete.</param>
        /// <param name="deleterUserId">ID of the user performing the deletion (for audit).</param>
        /// <returns>True if the user was found and soft-deleted; false otherwise.</returns>
        /// <remarks>
        /// This is a soft delete that preserves the user record but makes it 
        /// inaccessible to normal operations. To permanently delete a user,
        /// you would need to perform a direct database operation outside this API.
        /// </remarks>
        public async Task<bool> DeleteAsync(long userId, long deleterUserId)
        {
            #region implementation
            const string sql = @"
                UPDATE dbo.Users SET
                    DeletedAt = @Now,
                    UpdatedAt = @Now,
                    UpdatedBy = @Deleter
                WHERE UserID = @UserId AND DeletedAt IS NULL;";

            try
            {
                using (var conn = createConnection())
                {
                    var rows = await conn.ExecuteAsync(sql, new
                    {
                        Now = DateTime.UtcNow,
                        Deleter = deleterUserId,
                        UserId = userId
                    });
                    return rows > 0; // True if a user was updated
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error soft-deleting user {UserId}.", userId);
                throw;
            }
            #endregion implementation
        }

        /**************************************************************/
        /// <summary>
        /// Records a successful login and updates related security fields.
        /// </summary>
        /// <param name="userId">ID of the user who logged in.</param>
        /// <param name="loginTime">Timestamp of the login attempt.</param>
        /// <param name="ipAddress">Optional IP address of the login request.</param>
        /// <returns>True if the user was found and updated; false otherwise.</returns>
        /// <remarks>
        /// This method also:
        /// 1. Resets the failed login counter to zero
        /// 2. Clears any lockout that might have been in place
        /// 3. Updates the last activity timestamp
        /// </remarks>
        public async Task<bool> UpdateLastLoginAsync(long userId, DateTime loginTime, string? ipAddress)
        {
            #region implementation
            const string sql = @"
                UPDATE dbo.Users SET
                    LastLoginAt = @LoginTime,
                    LastActivityAt = @LoginTime,
                    LastIpAddress = @Ip,
                    FailedLoginCount = 0,
                    LockoutUntil = NULL
                WHERE UserID = @UserId;";

            try
            {
                using (var conn = createConnection())
                {
                    var rows = await conn.ExecuteAsync(sql, new
                    {
                        LoginTime = loginTime,
                        Ip = ipAddress,
                        UserId = userId
                    });
                    return rows > 0; // True if a user was updated
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating last login for user {UserId}.", userId);
                throw;
            }
            #endregion implementation
        }

        /**************************************************************/
        /// <summary>
        /// Creates a new user account via self-registration.
        /// </summary>
        /// <param name="request">User-provided registration information.</param>
        /// <returns>New UserID if successful, 0 if signup failed (e.g., duplicate email).</returns>
        /// <example>
        /// var signupRequest = new UserSignUpRequest {
        ///     Username = "johndoe",
        ///     DisplayName = "John Doe",
        ///     Email = "john@example.com",
        ///     Password = "SecureP@ssw0rd",
        ///     ConfirmPassword = "SecureP@ssw0rd"
        /// };
        /// var newUserId = await userDataAccess.SignUpAsync(signupRequest);
        /// </example>
        /// <remarks>
        /// This method:
        /// 1. Validates basic input requirements 
        /// 2. Converts the UserSignUpRequest to a User entity
        /// 3. Assigns default values for new accounts
        /// 4. Calls CreateAsync with no creator (self-signup)
        /// </remarks>
        public async Task<string?> SignUpAsync(UserSignUpRequest request)
        {
            #region implementation
            if (request == null)
                throw new ArgumentNullException(nameof(request));
            if (string.IsNullOrWhiteSpace(request.Email))
                throw new ArgumentException("Email is required.", nameof(request.Email));
            if (string.IsNullOrWhiteSpace(request.Password))
                throw new ArgumentException("Password is required.", nameof(request.Password));
            if (request.Password != request.ConfirmPassword)
                throw new ArgumentException("Passwords do not match.", nameof(request.ConfirmPassword));

            // Build the User entity
            var user = new User
            {
                CanonicalUsername = request.Username?.ToLowerInvariant(),
                DisplayName = request.DisplayName,
                PrimaryEmail = request.Email.ToLowerInvariant(),
                Password = request.Password,        // plaintext will be hashed in CreateAsync
                PhoneNumber = request.PhoneNumber,
                Timezone = request.Timezone,
                Locale = request.Locale,
                NotificationSettings = "default",             // or your app's default
                UiTheme = "light",                 // or whatever defaults you choose
                UserRole = "User"                   // assign the standard "user" role
            };

            // encryptedCreatorUserId = null, since this is a self-signup
            var newUserId = await CreateAsync(user, encryptedCreatorUserId: null);
            return newUserId;
            #endregion implementation
        }
        #endregion
    }
}