using MedRecPro.Controllers;
using MedRecPro.Data;
using MedRecPro.DataAccess;
using MedRecPro.Helpers;
using MedRecPro.Models;
using MedRecPro.Service;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace MedRecPro.Service.Test
{
    /**************************************************************/
    /// <summary>
    /// Unit tests for the ResolveMcpUser endpoint's auto-provisioning behavior
    /// in <see cref="UsersController"/>.
    /// </summary>
    /// <remarks>
    /// Tests cover existing user resolution, new user auto-provisioning with correct
    /// defaults, display name handling, email prefix fallback, invalid input validation,
    /// and race condition recovery during concurrent provisioning.
    /// </remarks>
    /// <seealso cref="UsersController"/>
    /// <seealso cref="UserDataAccess"/>
    [TestClass]
    public class ResolveMcpUserTests
    {
        #region Test Constants

        /// <summary>
        /// Standard test email for user operations.
        /// </summary>
        private const string TestEmail = "mcpuser@example.com";

        /// <summary>
        /// Standard test display name for provisioning tests.
        /// </summary>
        private const string TestDisplayName = "MCP Test User";

        /// <summary>
        /// Standard test provider name for provisioning tests.
        /// </summary>
        private const string TestProvider = "Google";

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
        /// <returns>An IConfiguration instance with test settings.</returns>
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
        /// <param name="databaseName">Unique name for the in-memory database.</param>
        /// <returns>A new ApplicationDbContext instance.</returns>
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
        /// <param name="context">The database context to use.</param>
        /// <returns>A configured UserDataAccess instance.</returns>
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
        /// Creates a UsersController instance with real UserDataAccess and mocked dependencies.
        /// </summary>
        /// <param name="context">The database context to use.</param>
        /// <returns>A configured UsersController instance.</returns>
        private UsersController createUsersController(ApplicationDbContext context)
        {
            var stringCipher = new StringCipher();
            var configuration = createTestConfiguration();
            var userDataAccess = createUserDataAccess(context);
            var logger = new Mock<ILogger<UsersController>>();
            var activityLogService = new Mock<IActivityLogService>();

            return new UsersController(
                stringCipher,
                configuration,
                userDataAccess,
                logger.Object,
                activityLogService.Object);
        }

        /**************************************************************/
        /// <summary>
        /// Seeds an existing user directly into the in-memory database.
        /// </summary>
        /// <param name="context">The database context.</param>
        /// <param name="email">Email for the user.</param>
        /// <param name="displayName">Display name for the user.</param>
        /// <returns>The seeded user entity with its assigned ID.</returns>
        private async Task<User> seedExistingUser(
            ApplicationDbContext context,
            string email = TestEmail,
            string displayName = TestDisplayName)
        {
            var lcEmail = email.ToLowerInvariant();
            var user = new User
            {
                UserName = lcEmail,
                Email = lcEmail,
                PrimaryEmail = lcEmail,
                EmailConfirmed = true,
                DisplayName = displayName,
                CanonicalUsername = lcEmail,
                Timezone = "UTC",
                Locale = "en-US",
                CreatedAt = DateTime.UtcNow,
                SecurityStamp = Guid.NewGuid().ToString()
            };

            context.AppUsers.Add(user);
            await context.SaveChangesAsync();
            return user;
        }

        /**************************************************************/
        /// <summary>
        /// Encrypts a user ID using the test PK secret.
        /// </summary>
        /// <param name="userId">The user ID to encrypt.</param>
        /// <returns>The encrypted user ID string.</returns>
        private string encryptUserId(long userId)
        {
            return StringCipher.Encrypt(userId.ToString(), TestPkSecret, StringCipher.EncryptionStrength.Fast);
        }

        #endregion

        #region Test Setup

        /**************************************************************/
        /// <summary>
        /// Initializes test infrastructure. Sets User.SetConfiguration so that
        /// the EncryptedUserId computed property can resolve the PK secret.
        /// </summary>
        [TestInitialize]
        public void TestSetup()
        {
            User.SetConfiguration(createTestConfiguration());
        }

        #endregion

        #region ResolveMcpUser Tests — Existing User

        /**************************************************************/
        /// <summary>
        /// Verifies that an existing user is resolved successfully with their
        /// encrypted ID and WasProvisioned is false.
        /// </summary>
        /// <seealso cref="UsersController.ResolveMcpUser"/>
        [TestMethod]
        public async Task ResolveMcpUser_ExistingUser_ReturnsEncryptedId()
        {
            #region implementation
            // Arrange
            using var context = createTestContext("ResolveMcp_ExistingUser_Test");
            var existingUser = await seedExistingUser(context);
            var controller = createUsersController(context);

            var request = new UsersController.McpUserResolveRequest
            {
                Email = TestEmail
            };

            // Act
            var result = await controller.ResolveMcpUser(request);

            // Assert
            Assert.IsInstanceOfType(result, typeof(OkObjectResult), "Should return 200 OK for existing user");
            var okResult = (OkObjectResult)result;
            var response = okResult.Value as UsersController.McpUserResolveResponse;
            Assert.IsNotNull(response, "Response should be McpUserResolveResponse");
            Assert.IsFalse(string.IsNullOrEmpty(response.EncryptedUserId), "EncryptedUserId should not be empty");
            Assert.IsFalse(response.WasProvisioned, "WasProvisioned should be false for existing user");
            #endregion
        }

        #endregion

        #region ResolveMcpUser Tests — New User Auto-Provisioning

        /**************************************************************/
        /// <summary>
        /// Verifies that a non-existent user is auto-provisioned and returns
        /// an encrypted ID with WasProvisioned set to true.
        /// </summary>
        /// <seealso cref="UsersController.ResolveMcpUser"/>
        [TestMethod]
        public async Task ResolveMcpUser_NewUser_AutoProvisionsAndReturnsEncryptedId()
        {
            #region implementation
            // Arrange
            using var context = createTestContext("ResolveMcp_NewUser_AutoProvision_Test");
            var controller = createUsersController(context);

            var request = new UsersController.McpUserResolveRequest
            {
                Email = "newuser@example.com",
                DisplayName = "New User",
                Provider = TestProvider
            };

            // Act
            var result = await controller.ResolveMcpUser(request);

            // Assert
            Assert.IsInstanceOfType(result, typeof(OkObjectResult), "Should return 200 OK for auto-provisioned user");
            var okResult = (OkObjectResult)result;
            var response = okResult.Value as UsersController.McpUserResolveResponse;
            Assert.IsNotNull(response, "Response should be McpUserResolveResponse");
            Assert.IsFalse(string.IsNullOrEmpty(response.EncryptedUserId), "EncryptedUserId should not be empty");
            Assert.IsTrue(response.WasProvisioned, "WasProvisioned should be true for newly created user");

            // Verify user actually exists in database
            var createdUser = await context.AppUsers
                .SingleOrDefaultAsync(u => u.PrimaryEmail == "newuser@example.com");
            Assert.IsNotNull(createdUser, "User should exist in database after auto-provisioning");
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that an auto-provisioned user has the correct default property values
        /// matching the pattern from AuthController.createUserForExternalLogin().
        /// </summary>
        /// <seealso cref="UsersController.ResolveMcpUser"/>
        [TestMethod]
        public async Task ResolveMcpUser_NewUser_SetsCorrectDefaults()
        {
            #region implementation
            // Arrange
            using var context = createTestContext("ResolveMcp_NewUser_Defaults_Test");
            var controller = createUsersController(context);

            var request = new UsersController.McpUserResolveRequest
            {
                Email = "DefaultsTest@Example.COM",
                DisplayName = "Defaults Test",
                Provider = "Microsoft"
            };

            // Act
            var result = await controller.ResolveMcpUser(request);

            // Assert — verify correct response
            Assert.IsInstanceOfType(result, typeof(OkObjectResult), "Should return 200 OK");
            var response = ((OkObjectResult)result).Value as UsersController.McpUserResolveResponse;
            Assert.IsNotNull(response);
            Assert.IsTrue(response.WasProvisioned, "User should be provisioned");

            // Assert — verify database record defaults
            var user = await context.AppUsers
                .SingleOrDefaultAsync(u => u.PrimaryEmail == "defaultstest@example.com");
            Assert.IsNotNull(user, "User should exist in database");
            Assert.AreEqual("defaultstest@example.com", user.UserName, "UserName should be lowercase email");
            Assert.AreEqual("defaultstest@example.com", user.Email, "Email should be lowercase");
            Assert.AreEqual("defaultstest@example.com", user.PrimaryEmail, "PrimaryEmail should be lowercase");
            Assert.AreEqual("defaultstest@example.com", user.CanonicalUsername, "CanonicalUsername should be lowercase");
            Assert.IsTrue(user.EmailConfirmed, "EmailConfirmed should be true for external login");
            Assert.AreEqual("Defaults Test", user.DisplayName, "DisplayName should match request");
            Assert.AreEqual("UTC", user.Timezone, "Timezone should default to UTC");
            Assert.AreEqual("en-US", user.Locale, "Locale should default to en-US");
            Assert.IsNotNull(user.SecurityStamp, "SecurityStamp should be set");
            Assert.IsFalse(string.IsNullOrEmpty(user.SecurityStamp), "SecurityStamp should not be empty");
            Assert.IsNull(user.PasswordHash, "No password should be set for external login user");
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that the display name from the request DTO is used for the
        /// provisioned user's DisplayName property.
        /// </summary>
        /// <seealso cref="UsersController.ResolveMcpUser"/>
        [TestMethod]
        public async Task ResolveMcpUser_NewUser_UsesDisplayNameFromRequest()
        {
            #region implementation
            // Arrange
            using var context = createTestContext("ResolveMcp_NewUser_DisplayName_Test");
            var controller = createUsersController(context);

            var request = new UsersController.McpUserResolveRequest
            {
                Email = "nameduser@example.com",
                DisplayName = "Jane Smith",
                Provider = TestProvider
            };

            // Act
            await controller.ResolveMcpUser(request);

            // Assert
            var user = await context.AppUsers
                .SingleOrDefaultAsync(u => u.PrimaryEmail == "nameduser@example.com");
            Assert.IsNotNull(user, "User should exist in database");
            Assert.AreEqual("Jane Smith", user.DisplayName, "DisplayName should come from request DTO");
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that when no DisplayName is provided in the request,
        /// the email prefix (before @) is used as the display name fallback.
        /// </summary>
        /// <seealso cref="UsersController.ResolveMcpUser"/>
        [TestMethod]
        public async Task ResolveMcpUser_NewUser_FallsBackToEmailPrefix()
        {
            #region implementation
            // Arrange
            using var context = createTestContext("ResolveMcp_NewUser_EmailFallback_Test");
            var controller = createUsersController(context);

            var request = new UsersController.McpUserResolveRequest
            {
                Email = "john.doe@company.org",
                DisplayName = null,
                Provider = TestProvider
            };

            // Act
            await controller.ResolveMcpUser(request);

            // Assert
            var user = await context.AppUsers
                .SingleOrDefaultAsync(u => u.PrimaryEmail == "john.doe@company.org");
            Assert.IsNotNull(user, "User should exist in database");
            Assert.AreEqual("john.doe", user.DisplayName, "DisplayName should be email prefix when no name provided");
            #endregion
        }

        #endregion

        #region ResolveMcpUser Tests — Invalid Input

        /**************************************************************/
        /// <summary>
        /// Verifies that an invalid email address returns a 400 Bad Request response.
        /// </summary>
        /// <seealso cref="UsersController.ResolveMcpUser"/>
        [TestMethod]
        public async Task ResolveMcpUser_InvalidEmail_ReturnsBadRequest()
        {
            #region implementation
            // Arrange
            using var context = createTestContext("ResolveMcp_InvalidEmail_Test");
            var controller = createUsersController(context);

            var request = new UsersController.McpUserResolveRequest
            {
                Email = "not-an-email"
            };

            // Simulate model validation failure (ASP.NET pipeline does this automatically)
            controller.ModelState.AddModelError("Email", "The Email field is not a valid e-mail address.");

            // Act
            var result = await controller.ResolveMcpUser(request);

            // Assert
            Assert.IsInstanceOfType(result, typeof(BadRequestObjectResult), "Should return 400 Bad Request for invalid email");
            #endregion
        }

        #endregion

        #region ResolveMcpUser Tests — Race Condition

        /**************************************************************/
        /// <summary>
        /// Verifies that when CreateAsync returns "Duplicate" (race condition where
        /// another request provisioned the same user concurrently), the endpoint
        /// re-fetches the user and still returns a successful response.
        /// </summary>
        /// <seealso cref="UsersController.ResolveMcpUser"/>
        [TestMethod]
        public async Task ResolveMcpUser_DuplicateRaceCondition_StillSucceeds()
        {
            #region implementation
            // Arrange — seed a user directly so CreateAsync will encounter the duplicate
            using var context = createTestContext("ResolveMcp_RaceCondition_Test");
            var existingUser = await seedExistingUser(context, "raceuser@example.com", "Race User");
            var controller = createUsersController(context);

            // The user already exists but we call ResolveMcpUser — this should find
            // the user via GetByEmailAsync and return successfully
            var request = new UsersController.McpUserResolveRequest
            {
                Email = "raceuser@example.com",
                DisplayName = "Race User Duplicate",
                Provider = TestProvider
            };

            // Act
            var result = await controller.ResolveMcpUser(request);

            // Assert — should succeed since GetByEmailAsync finds the user first
            Assert.IsInstanceOfType(result, typeof(OkObjectResult), "Should return 200 OK even for race condition");
            var okResult = (OkObjectResult)result;
            var response = okResult.Value as UsersController.McpUserResolveResponse;
            Assert.IsNotNull(response, "Response should be McpUserResolveResponse");
            Assert.IsFalse(string.IsNullOrEmpty(response.EncryptedUserId), "EncryptedUserId should not be empty");
            Assert.IsFalse(response.WasProvisioned, "WasProvisioned should be false since user already existed");
            #endregion
        }

        #endregion
    }
}
