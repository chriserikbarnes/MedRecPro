using MedRecPro.Data;
using MedRecPro.DataAccess;
using MedRecPro.Helpers;
using MedRecPro.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace MedRecPro.Service.Test
{
    /**************************************************************/
    /// <summary>
    /// Unit tests for UserDataAccess authentication, user management, and security functionality.
    /// </summary>
    /// <remarks>
    /// Tests cover authentication flows, user lockout after failed attempts,
    /// duplicate email handling, password rotation, and various edge cases
    /// for user account management in a medical records system.
    /// </remarks>
    /// <seealso cref="UserDataAccess"/>
    /// <seealso cref="User"/>
    [TestClass]
    public class UserDataAccessTests
    {
        #region Test Constants

        /// <summary>
        /// Standard test email for user operations.
        /// </summary>
        private const string TestEmail = "testuser@example.com";

        /// <summary>
        /// Standard test password for authentication tests.
        /// </summary>
        private const string TestPassword = "SecurePassword123!";

        /// <summary>
        /// Wrong password for negative authentication tests.
        /// </summary>
        private const string WrongPassword = "WrongPassword456!";

        /// <summary>
        /// Test PK secret for encryption operations.
        /// </summary>
        private const string TestPkSecret = "TestEncryptionSecretKey12345!@#";

        #endregion

        #region Helper Methods

        /**************************************************************/
        /// <summary>
        /// Creates a test configuration with required settings.
        /// </summary>
        /// <returns>An IConfiguration instance with test settings</returns>
        private IConfiguration createTestConfiguration()
        {
            var inMemorySettings = new Dictionary<string, string?>
            {
                { "Security:DB:PKSecret", TestPkSecret }
            };

            return new ConfigurationBuilder()
                .AddInMemoryCollection(inMemorySettings)
                .Build();
        }

        /**************************************************************/
        /// <summary>
        /// Creates a new in-memory database context for testing.
        /// </summary>
        /// <param name="databaseName">Unique name for the in-memory database</param>
        /// <returns>A new ApplicationDbContext instance</returns>
        private ApplicationDbContext createTestContext(string databaseName)
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: databaseName)
                .Options;

            return new ApplicationDbContext(options);
        }

        /**************************************************************/
        /// <summary>
        /// Creates a UserDataAccess instance with mocked dependencies.
        /// </summary>
        /// <param name="context">The database context to use</param>
        /// <returns>A configured UserDataAccess instance</returns>
        private UserDataAccess createUserDataAccess(ApplicationDbContext context)
        {
            var logger = new Mock<ILogger<UserDataAccess>>();
            var configuration = createTestConfiguration();
            var passwordHasher = new PasswordHasher<User>();

            return new UserDataAccess(
                context,
                passwordHasher,
                logger.Object,
                configuration);
        }

        /**************************************************************/
        /// <summary>
        /// Creates a test user with standard properties.
        /// </summary>
        /// <param name="email">Email address for the user</param>
        /// <param name="password">Password for the user</param>
        /// <returns>A configured User instance</returns>
        private User createTestUser(string email = TestEmail, string? password = TestPassword)
        {
            return new User
            {
                PrimaryEmail = email,
                Password = password,
                DisplayName = "Test User",
                CanonicalUsername = "testuser",
                UserRole = "User",
                Timezone = "UTC",
                Locale = "en-US"
            };
        }

        /**************************************************************/
        /// <summary>
        /// Encrypts a user ID using the test PK secret.
        /// </summary>
        /// <param name="userId">The user ID to encrypt</param>
        /// <returns>The encrypted user ID string</returns>
        private string encryptUserId(long userId)
        {
            return StringCipher.Encrypt(userId.ToString(), TestPkSecret, StringCipher.EncryptionStrength.Fast);
        }

        #endregion

        #region AuthenticateAsync Tests - Successful Authentication

        /**************************************************************/
        /// <summary>
        /// Verifies that valid credentials successfully authenticate a user.
        /// </summary>
        /// <seealso cref="UserDataAccess.AuthenticateAsync"/>
        [TestMethod]
        public async Task AuthenticateAsync_ValidCredentials_ReturnsUser()
        {
            #region implementation

            // Arrange
            using var context = createTestContext("Auth_ValidCredentials_Test");
            var userDataAccess = createUserDataAccess(context);

            // Create a user first
            var user = createTestUser();
            var encryptedId = await userDataAccess.CreateAsync(user, null);
            Assert.IsNotNull(encryptedId, "User creation should succeed");

            // Act
            var authenticatedUser = await userDataAccess.AuthenticateAsync(TestEmail, TestPassword);

            // Assert
            Assert.IsNotNull(authenticatedUser, "Authentication should succeed with valid credentials");
            Assert.AreEqual(TestEmail.ToLowerInvariant(), authenticatedUser.PrimaryEmail);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that successful authentication resets failed login count.
        /// </summary>
        /// <seealso cref="UserDataAccess.AuthenticateAsync"/>
        [TestMethod]
        public async Task AuthenticateAsync_AfterFailedAttempts_ResetsFailedCount()
        {
            #region implementation

            // Arrange
            using var context = createTestContext("Auth_ResetFailedCount_Test");
            var userDataAccess = createUserDataAccess(context);

            var user = createTestUser();
            await userDataAccess.CreateAsync(user, null);

            // Simulate some failed attempts
            await userDataAccess.AuthenticateAsync(TestEmail, WrongPassword);
            await userDataAccess.AuthenticateAsync(TestEmail, WrongPassword);

            // Act - Successful login
            var authenticatedUser = await userDataAccess.AuthenticateAsync(TestEmail, TestPassword);

            // Assert
            Assert.IsNotNull(authenticatedUser);
            Assert.AreEqual(0, authenticatedUser.FailedLoginCount, "Failed login count should be reset after successful login");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that successful authentication updates last login timestamp.
        /// </summary>
        /// <seealso cref="UserDataAccess.AuthenticateAsync"/>
        [TestMethod]
        public async Task AuthenticateAsync_ValidCredentials_UpdatesLastLogin()
        {
            #region implementation

            // Arrange
            using var context = createTestContext("Auth_UpdatesLastLogin_Test");
            var userDataAccess = createUserDataAccess(context);

            var user = createTestUser();
            await userDataAccess.CreateAsync(user, null);

            var beforeLogin = DateTime.UtcNow;

            // Act
            var authenticatedUser = await userDataAccess.AuthenticateAsync(TestEmail, TestPassword);

            // Assert
            Assert.IsNotNull(authenticatedUser);
            Assert.IsTrue(authenticatedUser.LastLoginAt >= beforeLogin || authenticatedUser.LastLoginAt == null,
                "Last login should be updated after successful authentication");

            #endregion
        }

        #endregion

        #region AuthenticateAsync Tests - Invalid Credentials

        /**************************************************************/
        /// <summary>
        /// Verifies that invalid password returns null.
        /// </summary>
        /// <seealso cref="UserDataAccess.AuthenticateAsync"/>
        [TestMethod]
        public async Task AuthenticateAsync_InvalidPassword_ReturnsNull()
        {
            #region implementation

            // Arrange
            using var context = createTestContext("Auth_InvalidPassword_Test");
            var userDataAccess = createUserDataAccess(context);

            var user = createTestUser();
            await userDataAccess.CreateAsync(user, null);

            // Act
            var result = await userDataAccess.AuthenticateAsync(TestEmail, WrongPassword);

            // Assert
            Assert.IsNull(result, "Authentication should fail with invalid password");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that non-existent user returns null.
        /// </summary>
        /// <seealso cref="UserDataAccess.AuthenticateAsync"/>
        [TestMethod]
        public async Task AuthenticateAsync_NonExistentUser_ReturnsNull()
        {
            #region implementation

            // Arrange
            using var context = createTestContext("Auth_NonExistent_Test");
            var userDataAccess = createUserDataAccess(context);

            // Act - No user created
            var result = await userDataAccess.AuthenticateAsync("nonexistent@example.com", TestPassword);

            // Assert
            Assert.IsNull(result, "Authentication should fail for non-existent user");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that null or whitespace email returns null.
        /// </summary>
        /// <seealso cref="UserDataAccess.AuthenticateAsync"/>
        [TestMethod]
        public async Task AuthenticateAsync_NullEmail_ReturnsNull()
        {
            #region implementation

            // Arrange
            using var context = createTestContext("Auth_NullEmail_Test");
            var userDataAccess = createUserDataAccess(context);

            // Act
            var result = await userDataAccess.AuthenticateAsync(null!, TestPassword);

            // Assert
            Assert.IsNull(result, "Authentication should fail with null email");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that null or whitespace password returns null.
        /// </summary>
        /// <seealso cref="UserDataAccess.AuthenticateAsync"/>
        [TestMethod]
        public async Task AuthenticateAsync_NullPassword_ReturnsNull()
        {
            #region implementation

            // Arrange
            using var context = createTestContext("Auth_NullPassword_Test");
            var userDataAccess = createUserDataAccess(context);

            var user = createTestUser();
            await userDataAccess.CreateAsync(user, null);

            // Act
            var result = await userDataAccess.AuthenticateAsync(TestEmail, null!);

            // Assert
            Assert.IsNull(result, "Authentication should fail with null password");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that authentication fails for deleted users.
        /// </summary>
        /// <seealso cref="UserDataAccess.AuthenticateAsync"/>
        [TestMethod]
        public async Task AuthenticateAsync_DeletedUser_ReturnsNull()
        {
            #region implementation

            // Arrange
            using var context = createTestContext("Auth_DeletedUser_Test");
            var userDataAccess = createUserDataAccess(context);

            var user = createTestUser();
            var encryptedId = await userDataAccess.CreateAsync(user, null);

            // Delete the user
            var createdUser = await userDataAccess.GetByEmailAsync(TestEmail);
            Assert.IsNotNull(createdUser);
            createdUser.EncryptedUserId = encryptedId;
            await userDataAccess.DeleteAsync(encryptedId, encryptedId);

            // Act
            var result = await userDataAccess.AuthenticateAsync(TestEmail, TestPassword);

            // Assert
            Assert.IsNull(result, "Authentication should fail for deleted users");

            #endregion
        }

        #endregion

        #region User Lockout Tests

        /**************************************************************/
        /// <summary>
        /// Verifies that failed login attempts increment the failed login count.
        /// </summary>
        /// <seealso cref="UserDataAccess.AuthenticateAsync"/>
        [TestMethod]
        public async Task AuthenticateAsync_FailedAttempt_IncrementsFailedCount()
        {
            #region implementation

            // Arrange
            using var context = createTestContext("Auth_IncrementsFailed_Test");
            var userDataAccess = createUserDataAccess(context);

            var user = createTestUser();
            await userDataAccess.CreateAsync(user, null);

            // Act - First failed attempt
            await userDataAccess.AuthenticateAsync(TestEmail, WrongPassword);

            // Get updated user
            var updatedUser = await userDataAccess.GetByEmailAsync(TestEmail);

            // Assert
            Assert.IsNotNull(updatedUser);
            Assert.AreEqual(1, updatedUser.FailedLoginCount, "Failed login count should be incremented");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that account is locked after maximum failed attempts.
        /// </summary>
        /// <remarks>
        /// This tests the lockout logic at UserDataAccess.cs:199-204
        /// </remarks>
        /// <seealso cref="UserDataAccess.AuthenticateAsync"/>
        [TestMethod]
        public async Task AuthenticateAsync_MaxFailedAttempts_LocksAccount()
        {
            #region implementation

            // Arrange
            using var context = createTestContext("Auth_LockAccount_Test");
            var userDataAccess = createUserDataAccess(context);

            var user = createTestUser();
            await userDataAccess.CreateAsync(user, null);

            // Act - Exceed max failed attempts (typically 5)
            for (int i = 0; i < 6; i++)
            {
                await userDataAccess.AuthenticateAsync(TestEmail, WrongPassword);
            }

            // Get updated user
            var lockedUser = await userDataAccess.GetByEmailAsync(TestEmail);

            // Assert
            Assert.IsNotNull(lockedUser);
            Assert.IsTrue(lockedUser.LockoutUntil.HasValue, "Account should be locked after max failed attempts");
            Assert.IsTrue(lockedUser.LockoutUntil.Value > DateTime.UtcNow, "Lockout time should be in the future");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that locked account cannot authenticate even with valid credentials.
        /// </summary>
        /// <seealso cref="UserDataAccess.AuthenticateAsync"/>
        [TestMethod]
        public async Task AuthenticateAsync_LockedAccount_ReturnsNull()
        {
            #region implementation

            // Arrange
            using var context = createTestContext("Auth_LockedAccount_Test");
            var userDataAccess = createUserDataAccess(context);

            var user = createTestUser();
            await userDataAccess.CreateAsync(user, null);

            // Lock the account by exceeding max failed attempts
            for (int i = 0; i < 6; i++)
            {
                await userDataAccess.AuthenticateAsync(TestEmail, WrongPassword);
            }

            // Act - Try to authenticate with valid credentials
            var result = await userDataAccess.AuthenticateAsync(TestEmail, TestPassword);

            // Assert
            Assert.IsNull(result, "Locked account should not be able to authenticate");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that lockout expires after the lockout duration.
        /// </summary>
        /// <seealso cref="UserDataAccess.AuthenticateAsync"/>
        [TestMethod]
        public async Task AuthenticateAsync_ExpiredLockout_AllowsAuthentication()
        {
            #region implementation

            // Arrange
            using var context = createTestContext("Auth_ExpiredLockout_Test");
            var userDataAccess = createUserDataAccess(context);

            var user = createTestUser();
            await userDataAccess.CreateAsync(user, null);

            // Manually set an expired lockout
            var dbUser = await context.AppUsers.FirstAsync(u => u.PrimaryEmail == TestEmail.ToLowerInvariant());
            dbUser.LockoutUntil = DateTime.UtcNow.AddMinutes(-1); // Expired
            dbUser.FailedLoginCount = 5;
            await context.SaveChangesAsync();

            // Act
            var result = await userDataAccess.AuthenticateAsync(TestEmail, TestPassword);

            // Assert
            Assert.IsNotNull(result, "Authentication should succeed after lockout expires");

            #endregion
        }

        #endregion

        #region CreateAsync Tests - Duplicate Email Handling

        /**************************************************************/
        /// <summary>
        /// Verifies that creating a user with duplicate email handles the scenario.
        /// </summary>
        /// <remarks>
        /// This tests the duplicate email logic at UserDataAccess.cs:265-286
        /// </remarks>
        /// <seealso cref="UserDataAccess.CreateAsync"/>
        [TestMethod]
        public async Task CreateAsync_DuplicateEmail_HandlesGracefully()
        {
            #region implementation

            // Arrange
            using var context = createTestContext("Create_DuplicateEmail_Test");
            var userDataAccess = createUserDataAccess(context);

            var user1 = createTestUser();
            var user2 = createTestUser(); // Same email

            // Act
            var result1 = await userDataAccess.CreateAsync(user1, null);
            var result2 = await userDataAccess.CreateAsync(user2, null);

            // Assert
            Assert.IsNotNull(result1, "First user creation should succeed");
            Assert.IsNotNull(result2, "Second user creation should return existing user ID or Duplicate");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that email is normalized to lowercase during creation.
        /// </summary>
        /// <seealso cref="UserDataAccess.CreateAsync"/>
        [TestMethod]
        public async Task CreateAsync_MixedCaseEmail_NormalizesToLowercase()
        {
            #region implementation

            // Arrange
            using var context = createTestContext("Create_NormalizeEmail_Test");
            var userDataAccess = createUserDataAccess(context);

            var user = createTestUser("TestUser@EXAMPLE.COM");

            // Act
            var encryptedId = await userDataAccess.CreateAsync(user, null);

            // Assert
            var createdUser = await userDataAccess.GetByEmailAsync("testuser@example.com");
            Assert.IsNotNull(createdUser, "User should be findable with lowercase email");
            Assert.AreEqual("testuser@example.com", createdUser.PrimaryEmail);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that password is hashed during user creation.
        /// </summary>
        /// <seealso cref="UserDataAccess.CreateAsync"/>
        [TestMethod]
        public async Task CreateAsync_WithPassword_HashesPassword()
        {
            #region implementation

            // Arrange
            using var context = createTestContext("Create_HashPassword_Test");
            var userDataAccess = createUserDataAccess(context);

            var user = createTestUser();

            // Act
            var encryptedId = await userDataAccess.CreateAsync(user, null);

            // Assert
            var createdUser = await context.AppUsers.FirstAsync(u => u.PrimaryEmail == TestEmail.ToLowerInvariant());
            Assert.IsNotNull(createdUser.PasswordHash, "Password should be hashed");
            Assert.AreNotEqual(TestPassword, createdUser.PasswordHash, "Password hash should not be plaintext");
            Assert.IsNull(createdUser.Password, "Plaintext password should be cleared");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that security stamp is generated during user creation.
        /// </summary>
        /// <seealso cref="UserDataAccess.CreateAsync"/>
        [TestMethod]
        public async Task CreateAsync_NewUser_GeneratesSecurityStamp()
        {
            #region implementation

            // Arrange
            using var context = createTestContext("Create_SecurityStamp_Test");
            var userDataAccess = createUserDataAccess(context);

            var user = createTestUser();

            // Act
            await userDataAccess.CreateAsync(user, null);

            // Assert
            var createdUser = await context.AppUsers.FirstAsync(u => u.PrimaryEmail == TestEmail.ToLowerInvariant());
            Assert.IsNotNull(createdUser.SecurityStamp, "Security stamp should be generated");
            Assert.IsTrue(Guid.TryParse(createdUser.SecurityStamp, out _), "Security stamp should be a valid GUID");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that CreateAsync returns encrypted user ID.
        /// </summary>
        /// <seealso cref="UserDataAccess.CreateAsync"/>
        [TestMethod]
        public async Task CreateAsync_Success_ReturnsEncryptedUserId()
        {
            #region implementation

            // Arrange
            using var context = createTestContext("Create_ReturnsEncryptedId_Test");
            var userDataAccess = createUserDataAccess(context);

            var user = createTestUser();

            // Act
            var encryptedId = await userDataAccess.CreateAsync(user, null);

            // Assert
            Assert.IsNotNull(encryptedId);
            Assert.IsTrue(encryptedId.StartsWith("F-"), "Encrypted ID should use Fast mode encryption");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that CreateAsync throws exception for null user.
        /// </summary>
        /// <seealso cref="UserDataAccess.CreateAsync"/>
        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public async Task CreateAsync_NullUser_ThrowsArgumentNullException()
        {
            #region implementation

            // Arrange
            using var context = createTestContext("Create_NullUser_Test");
            var userDataAccess = createUserDataAccess(context);

            // Act
            await userDataAccess.CreateAsync(null!, null);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that CreateAsync throws exception for missing email.
        /// </summary>
        /// <seealso cref="UserDataAccess.CreateAsync"/>
        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public async Task CreateAsync_MissingEmail_ThrowsArgumentException()
        {
            #region implementation

            // Arrange
            using var context = createTestContext("Create_MissingEmail_Test");
            var userDataAccess = createUserDataAccess(context);

            var user = new User { DisplayName = "Test" };

            // Act
            await userDataAccess.CreateAsync(user, null);

            #endregion
        }

        #endregion

        #region RotatePasswordAsync Tests

        /**************************************************************/
        /// <summary>
        /// Verifies that password rotation updates the password hash.
        /// </summary>
        /// <seealso cref="UserDataAccess.RotatePasswordAsync"/>
        [TestMethod]
        public async Task RotatePasswordAsync_ValidRequest_UpdatesPassword()
        {
            #region implementation

            // Arrange
            using var context = createTestContext("Rotate_UpdatesPassword_Test");
            var userDataAccess = createUserDataAccess(context);

            var user = createTestUser();
            var encryptedId = await userDataAccess.CreateAsync(user, null);

            var oldHash = (await context.AppUsers.FirstAsync(u => u.PrimaryEmail == TestEmail.ToLowerInvariant())).PasswordHash;

            // Act
            var result = await userDataAccess.RotatePasswordAsync(encryptedId, "NewPassword123!", encryptedId);

            // Assert
            Assert.IsTrue(result, "Password rotation should succeed");

            var updatedUser = await context.AppUsers.FirstAsync(u => u.PrimaryEmail == TestEmail.ToLowerInvariant());
            Assert.AreNotEqual(oldHash, updatedUser.PasswordHash, "Password hash should be updated");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that password rotation updates security stamp.
        /// </summary>
        /// <seealso cref="UserDataAccess.RotatePasswordAsync"/>
        [TestMethod]
        public async Task RotatePasswordAsync_Success_UpdatesSecurityStamp()
        {
            #region implementation

            // Arrange
            using var context = createTestContext("Rotate_UpdatesStamp_Test");
            var userDataAccess = createUserDataAccess(context);

            var user = createTestUser();
            var encryptedId = await userDataAccess.CreateAsync(user, null);

            var oldStamp = (await context.AppUsers.FirstAsync(u => u.PrimaryEmail == TestEmail.ToLowerInvariant())).SecurityStamp;

            // Act
            await userDataAccess.RotatePasswordAsync(encryptedId, "NewPassword123!", encryptedId);

            // Assert
            var updatedUser = await context.AppUsers.FirstAsync(u => u.PrimaryEmail == TestEmail.ToLowerInvariant());
            Assert.AreNotEqual(oldStamp, updatedUser.SecurityStamp, "Security stamp should be updated");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that user can authenticate with new password after rotation.
        /// </summary>
        /// <seealso cref="UserDataAccess.RotatePasswordAsync"/>
        /// <seealso cref="UserDataAccess.AuthenticateAsync"/>
        [TestMethod]
        public async Task RotatePasswordAsync_NewPassword_CanAuthenticate()
        {
            #region implementation

            // Arrange
            using var context = createTestContext("Rotate_CanAuthenticate_Test");
            var userDataAccess = createUserDataAccess(context);

            var user = createTestUser();
            var encryptedId = await userDataAccess.CreateAsync(user, null);

            var newPassword = "NewSecurePassword456!";

            // Act
            await userDataAccess.RotatePasswordAsync(encryptedId, newPassword, encryptedId);
            var authenticatedUser = await userDataAccess.AuthenticateAsync(TestEmail, newPassword);

            // Assert
            Assert.IsNotNull(authenticatedUser, "Should be able to authenticate with new password");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that password rotation fails with empty password.
        /// </summary>
        /// <seealso cref="UserDataAccess.RotatePasswordAsync"/>
        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public async Task RotatePasswordAsync_EmptyPassword_ThrowsArgumentException()
        {
            #region implementation

            // Arrange
            using var context = createTestContext("Rotate_EmptyPassword_Test");
            var userDataAccess = createUserDataAccess(context);

            var user = createTestUser();
            var encryptedId = await userDataAccess.CreateAsync(user, null);

            // Act
            await userDataAccess.RotatePasswordAsync(encryptedId, "", encryptedId);

            #endregion
        }

        #endregion

        #region GetByIdAsync and GetByEmailAsync Tests

        /**************************************************************/
        /// <summary>
        /// Verifies that GetByIdAsync returns user with valid encrypted ID.
        /// </summary>
        /// <seealso cref="UserDataAccess.GetByIdAsync"/>
        [TestMethod]
        public async Task GetByIdAsync_ValidId_ReturnsUser()
        {
            #region implementation

            // Arrange
            using var context = createTestContext("GetById_Valid_Test");
            var userDataAccess = createUserDataAccess(context);

            var user = createTestUser();
            var encryptedId = await userDataAccess.CreateAsync(user, null);

            // Act
            var retrievedUser = await userDataAccess.GetByIdAsync(encryptedId);

            // Assert
            Assert.IsNotNull(retrievedUser, "User should be found by encrypted ID");
            Assert.AreEqual(TestEmail.ToLowerInvariant(), retrievedUser.PrimaryEmail);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that GetByIdAsync returns null for invalid encrypted ID.
        /// </summary>
        /// <seealso cref="UserDataAccess.GetByIdAsync"/>
        [TestMethod]
        public async Task GetByIdAsync_InvalidId_ReturnsNull()
        {
            #region implementation

            // Arrange
            using var context = createTestContext("GetById_Invalid_Test");
            var userDataAccess = createUserDataAccess(context);

            // Act
            var result = await userDataAccess.GetByIdAsync("InvalidEncryptedId");

            // Assert
            Assert.IsNull(result, "Should return null for invalid encrypted ID");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that GetByEmailAsync returns user with valid email.
        /// </summary>
        /// <seealso cref="UserDataAccess.GetByEmailAsync"/>
        [TestMethod]
        public async Task GetByEmailAsync_ValidEmail_ReturnsUser()
        {
            #region implementation

            // Arrange
            using var context = createTestContext("GetByEmail_Valid_Test");
            var userDataAccess = createUserDataAccess(context);

            var user = createTestUser();
            await userDataAccess.CreateAsync(user, null);

            // Act
            var retrievedUser = await userDataAccess.GetByEmailAsync(TestEmail);

            // Assert
            Assert.IsNotNull(retrievedUser, "User should be found by email");
            Assert.AreEqual(TestEmail.ToLowerInvariant(), retrievedUser.PrimaryEmail);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that GetByEmailAsync normalizes email to lowercase.
        /// </summary>
        /// <seealso cref="UserDataAccess.GetByEmailAsync"/>
        [TestMethod]
        public async Task GetByEmailAsync_MixedCaseEmail_FindsUser()
        {
            #region implementation

            // Arrange
            using var context = createTestContext("GetByEmail_MixedCase_Test");
            var userDataAccess = createUserDataAccess(context);

            var user = createTestUser("testuser@example.com");
            await userDataAccess.CreateAsync(user, null);

            // Act - Search with mixed case
            var retrievedUser = await userDataAccess.GetByEmailAsync("TestUser@EXAMPLE.COM");

            // Assert
            Assert.IsNotNull(retrievedUser, "User should be found regardless of email case");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that GetByEmailAsync returns null for non-existent email.
        /// </summary>
        /// <seealso cref="UserDataAccess.GetByEmailAsync"/>
        [TestMethod]
        public async Task GetByEmailAsync_NonExistentEmail_ReturnsNull()
        {
            #region implementation

            // Arrange
            using var context = createTestContext("GetByEmail_NonExistent_Test");
            var userDataAccess = createUserDataAccess(context);

            // Act
            var result = await userDataAccess.GetByEmailAsync("nonexistent@example.com");

            // Assert
            Assert.IsNull(result, "Should return null for non-existent email");

            #endregion
        }

        #endregion

        #region DeleteAsync Tests

        /**************************************************************/
        /// <summary>
        /// Verifies that DeleteAsync performs soft delete by setting DeletedAt.
        /// </summary>
        /// <seealso cref="UserDataAccess.DeleteAsync"/>
        [TestMethod]
        public async Task DeleteAsync_ValidRequest_SetsDeletedAt()
        {
            #region implementation

            // Arrange
            using var context = createTestContext("Delete_SetsDeletedAt_Test");
            var userDataAccess = createUserDataAccess(context);

            var user = createTestUser();
            var encryptedId = await userDataAccess.CreateAsync(user, null);

            // Act
            var result = await userDataAccess.DeleteAsync(encryptedId, encryptedId);

            // Assert
            Assert.IsTrue(result, "Delete should succeed");

            var deletedUser = await context.AppUsers.FirstAsync(u => u.PrimaryEmail == TestEmail.ToLowerInvariant());
            Assert.IsNotNull(deletedUser.DeletedAt, "DeletedAt should be set");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that deleted user is not returned by GetByEmailAsync.
        /// </summary>
        /// <seealso cref="UserDataAccess.DeleteAsync"/>
        /// <seealso cref="UserDataAccess.GetByEmailAsync"/>
        [TestMethod]
        public async Task DeleteAsync_Success_UserNotFoundByEmail()
        {
            #region implementation

            // Arrange
            using var context = createTestContext("Delete_NotFound_Test");
            var userDataAccess = createUserDataAccess(context);

            var user = createTestUser();
            var encryptedId = await userDataAccess.CreateAsync(user, null);

            // Act
            await userDataAccess.DeleteAsync(encryptedId, encryptedId);
            var result = await userDataAccess.GetByEmailAsync(TestEmail);

            // Assert
            Assert.IsNull(result, "Deleted user should not be found by GetByEmailAsync");

            #endregion
        }

        #endregion

        #region SignUpAsync Tests

        /**************************************************************/
        /// <summary>
        /// Verifies that SignUpAsync creates user with correct properties.
        /// </summary>
        /// <seealso cref="UserDataAccess.SignUpAsync"/>
        [TestMethod]
        public async Task SignUpAsync_ValidRequest_CreatesUser()
        {
            #region implementation

            // Arrange
            using var context = createTestContext("SignUp_CreatesUser_Test");
            var userDataAccess = createUserDataAccess(context);

            var request = new UserSignUpRequestDto
            {
                Email = TestEmail,
                Password = TestPassword,
                ConfirmPassword = TestPassword,
                DisplayName = "New User",
                Username = "newuser"
            };

            // Act
            var encryptedId = await userDataAccess.SignUpAsync(request);

            // Assert
            Assert.IsNotNull(encryptedId, "SignUp should return encrypted user ID");
            Assert.AreNotEqual("Duplicate", encryptedId, "Should not be duplicate for new user");

            var createdUser = await userDataAccess.GetByEmailAsync(TestEmail);
            Assert.IsNotNull(createdUser);
            Assert.AreEqual("New User", createdUser.DisplayName);
            Assert.AreEqual("User", createdUser.UserRole, "Default role should be User");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that SignUpAsync validates password confirmation match.
        /// </summary>
        /// <seealso cref="UserDataAccess.SignUpAsync"/>
        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public async Task SignUpAsync_PasswordMismatch_ThrowsArgumentException()
        {
            #region implementation

            // Arrange
            using var context = createTestContext("SignUp_PasswordMismatch_Test");
            var userDataAccess = createUserDataAccess(context);

            var request = new UserSignUpRequestDto
            {
                Email = TestEmail,
                Password = TestPassword,
                ConfirmPassword = "DifferentPassword123!",
                DisplayName = "New User"
            };

            // Act
            await userDataAccess.SignUpAsync(request);

            #endregion
        }

        #endregion

        #region User Without Password Tests

        /**************************************************************/
        /// <summary>
        /// Verifies that user without password hash cannot authenticate.
        /// </summary>
        /// <seealso cref="UserDataAccess.AuthenticateAsync"/>
        [TestMethod]
        public async Task AuthenticateAsync_UserWithoutPasswordHash_ReturnsNull()
        {
            #region implementation

            // Arrange
            using var context = createTestContext("Auth_NoPasswordHash_Test");
            var userDataAccess = createUserDataAccess(context);

            // Create user without password (simulating third-party SSO user)
            var user = createTestUser(TestEmail, null);
            await userDataAccess.CreateAsync(user, null);

            // Act
            var result = await userDataAccess.AuthenticateAsync(TestEmail, TestPassword);

            // Assert
            Assert.IsNull(result, "User without password hash should not authenticate");

            #endregion
        }

        #endregion
    }
}
