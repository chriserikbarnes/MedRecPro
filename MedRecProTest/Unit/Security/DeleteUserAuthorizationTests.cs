using MedRecPro.Configuration;
using MedRecPro.Controllers;
using MedRecPro.Data;
using MedRecPro.DataAccess;
using MedRecPro.Helpers;
using MedRecPro.Models;
using MedRecPro.Service;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System.Security.Claims;

namespace MedRecProTest.Unit.Security
{
    /**************************************************************/
    /// <summary>
    /// Unit tests for the DeleteUser endpoint's admin-or-self authorization
    /// enforcement in <see cref="UsersController"/>.
    /// </summary>
    /// <remarks>
    /// Guards against the authorization fall-through defect where the 403 result
    /// for a non-admin, non-self caller was computed but not returned, allowing
    /// the soft-delete to proceed. Tests cover: forbidden callers receive 403 and
    /// the target user is NOT deleted; admin, user-admin, and self callers can
    /// still soft-delete successfully.
    /// </remarks>
    /// <seealso cref="UsersController"/>
    /// <seealso cref="UserDataAccess"/>
    /// <seealso cref="ClaimHelper"/>
    [TestClass]
    [TestCategory("Unit")]
    public class DeleteUserAuthorizationTests
    {
        #region Test Constants

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
            #region implementation
            var inMemorySettings = new Dictionary<string, string?>
            {
                { "Security:DB:PKSecret", TestPkSecret }
            };

            return new ConfigurationBuilder()
                .AddInMemoryCollection(inMemorySettings)
                .Build();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates a new in-memory database context for testing.
        /// </summary>
        /// <param name="databaseName">Unique name for the in-memory database.</param>
        /// <returns>A new ApplicationDbContext instance.</returns>
        private ApplicationDbContext createTestContext(string databaseName)
        {
            #region implementation
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: databaseName)
                .Options;

            return new ApplicationDbContext(options);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates a UserDataAccess instance with mocked dependencies.
        /// </summary>
        /// <param name="context">The database context to use.</param>
        /// <returns>A configured UserDataAccess instance.</returns>
        /// <seealso cref="PrimaryKeyCipher"/>
        private UserDataAccess createUserDataAccess(ApplicationDbContext context)
        {
            #region implementation
            var logger = new Mock<ILogger<UserDataAccess>>();
            var passwordHasher = new PasswordHasher<User>();

            return new UserDataAccess(
                context,
                passwordHasher,
                logger.Object,
                new PrimaryKeyCipher(Options.Create(new DatabaseSecurityOptions
                {
                    PKSecret = TestPkSecret
                })));
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates a UsersController instance with real UserDataAccess and mocked
        /// dependencies, authenticated as the specified caller.
        /// </summary>
        /// <param name="context">The database context to use.</param>
        /// <param name="callerUserId">The numeric ID placed in the caller's NameIdentifier claim.</param>
        /// <returns>A configured UsersController instance with an authenticated HttpContext.</returns>
        /// <remarks>
        /// DeleteUser resolves the acting user via <see cref="ClaimHelper.GetEncryptedUserIdOrThrow"/>,
        /// which reads the NameIdentifier claim, so the controller context must carry
        /// an authenticated ClaimsPrincipal for the caller.
        /// </remarks>
        /// <seealso cref="ClaimHelper"/>
        private UsersController createUsersController(ApplicationDbContext context, long callerUserId)
        {
            #region implementation
            var stringCipher = new StringCipher();
            var configuration = createTestConfiguration();
            var userDataAccess = createUserDataAccess(context);
            var logger = new Mock<ILogger<UsersController>>();
            var activityLogService = new Mock<IActivityLogService>();

            var controller = new UsersController(
                stringCipher,
                configuration,
                userDataAccess,
                logger.Object,
                activityLogService.Object);

            // Authenticate the caller: getEncryptedIdFromClaim() reads NameIdentifier
            // from the request's ClaimsPrincipal.
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                    {
                        new Claim(ClaimTypes.NameIdentifier, callerUserId.ToString())
                    }, authenticationType: "TestAuth"))
                }
            };

            return controller;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Seeds a user directly into the in-memory database.
        /// </summary>
        /// <param name="context">The database context.</param>
        /// <param name="email">Email for the user.</param>
        /// <param name="userRole">Coarse-grained role for the user (e.g., "User", "Admin").</param>
        /// <returns>The seeded user entity with its assigned ID.</returns>
        /// <seealso cref="MedRecPro.Models.UserRole"/>
        private async Task<User> seedUser(
            ApplicationDbContext context,
            string email,
            string userRole = MedRecPro.Models.UserRole.RegularUser)
        {
            #region implementation
            var lcEmail = email.ToLowerInvariant();
            var user = new User
            {
                UserName = lcEmail,
                Email = lcEmail,
                PrimaryEmail = lcEmail,
                EmailConfirmed = true,
                DisplayName = lcEmail,
                CanonicalUsername = lcEmail,
                UserRole = userRole,
                Timezone = "UTC",
                Locale = "en-US",
                CreatedAt = DateTime.UtcNow,
                SecurityStamp = Guid.NewGuid().ToString()
            };

            context.AppUsers.Add(user);
            await context.SaveChangesAsync();
            return user;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Encrypts a user ID using the test PK secret, matching the wire format
        /// produced by <see cref="PrimaryKeyCipher"/> and <see cref="ClaimHelper"/>.
        /// </summary>
        /// <param name="userId">The user ID to encrypt.</param>
        /// <returns>The encrypted user ID string.</returns>
        private string encryptUserId(long userId)
        {
            #region implementation
            return StringCipher.Encrypt(userId.ToString(), TestPkSecret, StringCipher.EncryptionStrength.Fast);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Reloads a user row from the database and returns whether it has been soft-deleted.
        /// </summary>
        /// <param name="context">The database context.</param>
        /// <param name="userId">The numeric ID of the user to inspect.</param>
        /// <returns>True when DeletedAt is populated; false when the user remains active.</returns>
        private async Task<bool> isSoftDeleted(ApplicationDbContext context, long userId)
        {
            #region implementation
            var user = await context.AppUsers.SingleAsync(u => u.Id == userId);

            // Reload to observe committed state rather than any stale tracked snapshot.
            await context.Entry(user).ReloadAsync();

            return user.DeletedAt != null;
            #endregion
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
            #region implementation
            User.SetConfiguration(createTestConfiguration());
            #endregion
        }

        #endregion

        #region DeleteUser Tests — Forbidden Caller

        /**************************************************************/
        /// <summary>
        /// Verifies that a caller who is neither an admin nor the target user
        /// receives 403 Forbidden and the target user is NOT soft-deleted.
        /// </summary>
        /// <remarks>
        /// Regression test for the fall-through defect where the 403 result was
        /// computed but not returned, allowing the delete to proceed.
        /// </remarks>
        /// <seealso cref="UsersController.DeleteUser"/>
        [TestMethod]
        public async Task DeleteUser_NonAdminNonSelfCaller_ReturnsForbiddenAndDoesNotDelete()
        {
            #region implementation
            // Arrange — a regular user attempts to delete a different user
            using var context = createTestContext("DeleteUser_Forbidden_Test");
            var targetUser = await seedUser(context, "target@example.com");
            var caller = await seedUser(context, "regular-caller@example.com");
            var controller = createUsersController(context, caller.Id);

            // Act
            var result = await controller.DeleteUser(encryptUserId(targetUser.Id));

            // Assert — 403 returned to the caller
            Assert.IsInstanceOfType(result, typeof(ObjectResult), "Should return an ObjectResult carrying the 403 status");
            var objectResult = (ObjectResult)result;
            Assert.AreEqual(StatusCodes.Status403Forbidden, objectResult.StatusCode,
                "Non-admin, non-self caller must receive 403 Forbidden");

            // Assert — target user must remain active in the database
            Assert.IsFalse(await isSoftDeleted(context, targetUser.Id),
                "Target user must NOT be soft-deleted when the caller is unauthorized");
            #endregion
        }

        #endregion

        #region DeleteUser Tests — Authorized Callers

        /**************************************************************/
        /// <summary>
        /// Verifies that an Admin caller can still soft-delete another user.
        /// </summary>
        /// <seealso cref="UsersController.DeleteUser"/>
        [TestMethod]
        public async Task DeleteUser_AdminCaller_SoftDeletesTargetUser()
        {
            #region implementation
            // Arrange — an Admin deletes a different user
            using var context = createTestContext("DeleteUser_Admin_Test");
            var targetUser = await seedUser(context, "target@example.com");
            var admin = await seedUser(context, "admin@example.com", MedRecPro.Models.UserRole.Admin);
            var controller = createUsersController(context, admin.Id);

            // Act
            var result = await controller.DeleteUser(encryptUserId(targetUser.Id));

            // Assert — 204 returned and the target user is soft-deleted
            Assert.IsInstanceOfType(result, typeof(NoContentResult), "Admin delete should return 204 No Content");
            Assert.IsTrue(await isSoftDeleted(context, targetUser.Id),
                "Target user should be soft-deleted when the caller is an Admin");
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that a User Admin caller can still soft-delete another user,
        /// covering the second role accepted by <see cref="User.IsUserAdmin"/>.
        /// </summary>
        /// <seealso cref="UsersController.DeleteUser"/>
        [TestMethod]
        public async Task DeleteUser_UserAdminCaller_SoftDeletesTargetUser()
        {
            #region implementation
            // Arrange — a User Admin deletes a different user
            using var context = createTestContext("DeleteUser_UserAdmin_Test");
            var targetUser = await seedUser(context, "target@example.com");
            var userAdmin = await seedUser(context, "useradmin@example.com", MedRecPro.Models.UserRole.UserAdmin);
            var controller = createUsersController(context, userAdmin.Id);

            // Act
            var result = await controller.DeleteUser(encryptUserId(targetUser.Id));

            // Assert — 204 returned and the target user is soft-deleted
            Assert.IsInstanceOfType(result, typeof(NoContentResult), "User Admin delete should return 204 No Content");
            Assert.IsTrue(await isSoftDeleted(context, targetUser.Id),
                "Target user should be soft-deleted when the caller is a User Admin");
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that a user can still soft-delete their own account.
        /// </summary>
        /// <seealso cref="UsersController.DeleteUser"/>
        [TestMethod]
        public async Task DeleteUser_SelfCaller_SoftDeletesOwnAccount()
        {
            #region implementation
            // Arrange — a regular user deletes themselves
            using var context = createTestContext("DeleteUser_Self_Test");
            var selfUser = await seedUser(context, "selfdelete@example.com");
            var controller = createUsersController(context, selfUser.Id);

            // Act
            var result = await controller.DeleteUser(encryptUserId(selfUser.Id));

            // Assert — 204 returned and the account is soft-deleted
            Assert.IsInstanceOfType(result, typeof(NoContentResult), "Self delete should return 204 No Content");
            Assert.IsTrue(await isSoftDeleted(context, selfUser.Id),
                "User should be able to soft-delete their own account");
            #endregion
        }

        #endregion
    }
}
