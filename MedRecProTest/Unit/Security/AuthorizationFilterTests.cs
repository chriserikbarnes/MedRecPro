using MedRecPro.Data;
using MedRecPro.DataAccess;
using MedRecPro.Exceptions;
using MedRecPro.Filters;
using MedRecPro.Helpers;
using MedRecPro.Models;
using MedRecPro.Service;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System.Security.Claims;
using static MedRecPro.Models.Constant;

namespace MedRecProTest.Unit.Security
{
    /**************************************************************/
    /// <summary>
    /// Exercises the claim-driven authorization filters
    /// (<see cref="UserRoleAuthorizationFilter"/>, <see cref="ActorAuthorizationFilter"/>)
    /// and the <see cref="AuthorizationExceptionFilter"/> that translates their
    /// exceptions into HTTP responses.
    /// </summary>
    /// <remarks>
    /// <see cref="UserDataAccess"/> is a concrete class with non-virtual
    /// methods, so the filters are fed real instances over EF InMemory
    /// databases. The class runs without parallel neighbors and resets the
    /// process-wide <see cref="UserDataAccess"/> secret before each test so its
    /// dedicated PK configuration cannot leak across fixtures.
    /// </remarks>
    /// <seealso cref="UserRoleAuthorizationFilter"/>
    /// <seealso cref="ActorAuthorizationFilter"/>
    /// <seealso cref="AuthorizationExceptionFilter"/>
    /// <seealso cref="UserDataAccess"/>
    /// <seealso cref="PermissionService"/>
    [TestClass]
    [TestCategory("Unit")]
    [DoNotParallelize]
    public class AuthorizationFilterTests
    {
        #region implementation

        /// <summary>
        /// Assembly-shared PK secret; must match the value cached by
        /// <see cref="UserDataAccess"/> statics across the test run.
        /// </summary>
        private const string TestPkSecret = "TestEncryptionSecretKey12345!@#";

        /**************************************************************/
        /// <summary>
        /// Clears the legacy process-wide user encryption key before each isolated authorization test.
        /// </summary>
        /// <remarks>
        /// These fixtures intentionally supply their own PK secret. Isolation prevents a real-host or another
        /// data-access fixture from leaking a different cached secret into authorization behavior.
        /// </remarks>
        /// <seealso cref="UserDataAccess.resetPkSecretForTests"/>
        [TestInitialize]
        public void ResetUserDataAccessSecret()
        {
            #region implementation

            UserDataAccess.resetPkSecretForTests();

            #endregion
        }

        #region UserRoleAuthorizationFilter

        /**************************************************************/
        /// <summary>
        /// Verifies an unauthenticated request (no user-ID claim) is rejected
        /// with the 401 unauthenticated role exception.
        /// </summary>
        /// <seealso cref="UserRoleAuthorizationFilter.OnAuthorizationAsync"/>
        [TestMethod]
        public async Task UserRoleAuthorizationFilter_OnAuthorizationAsync_MissingClaim_ThrowsUserRoleAuthorizationException()
        {
            #region implementation
            // Arrange
            using var context = createContext();
            var filter = createUserRoleFilter(context, "Admin");
            var authContext = FilterContextTestHelper.CreateAuthorizationFilterContext(
                FilterContextTestHelper.CreateAnonymousHttpContext());

            // Act + Assert
            var exception = await Assert.ThrowsExceptionAsync<UserRoleAuthorizationException>(
                () => filter.OnAuthorizationAsync(authContext));

            Assert.AreEqual(401, exception.StatusCode);
            Assert.AreEqual("AUTH_ROLE_UNAUTHENTICATED", exception.ErrorCode);
            CollectionAssert.AreEqual(new[] { "Admin" }, exception.RequiredRoles);
            Assert.IsNull(exception.ActualRole);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies a valid claim pointing at a nonexistent user is rejected
        /// with the 401 unauthenticated role exception.
        /// </summary>
        /// <seealso cref="UserRoleAuthorizationFilter.OnAuthorizationAsync"/>
        [TestMethod]
        public async Task UserRoleAuthorizationFilter_OnAuthorizationAsync_UserNotFound_ThrowsUserRoleAuthorizationException()
        {
            #region implementation
            // Arrange - empty database, claim references user 424242.
            using var context = createContext();
            var filter = createUserRoleFilter(context, "Admin");
            var authContext = FilterContextTestHelper.CreateAuthorizationFilterContext(
                FilterContextTestHelper.CreateAuthenticatedHttpContext(424242));

            // Act + Assert
            var exception = await Assert.ThrowsExceptionAsync<UserRoleAuthorizationException>(
                () => filter.OnAuthorizationAsync(authContext));

            Assert.AreEqual(401, exception.StatusCode);
            Assert.AreEqual("AUTH_ROLE_UNAUTHENTICATED", exception.ErrorCode);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies a user whose role matches an allowed role passes without
        /// any exception or result mutation.
        /// </summary>
        /// <seealso cref="UserRoleAuthorizationFilter.OnAuthorizationAsync"/>
        [TestMethod]
        public async Task UserRoleAuthorizationFilter_OnAuthorizationAsync_MatchingRole_AllowsRequest()
        {
            #region implementation
            // Arrange
            using var context = createContext();
            var user = seedUser(context, role: "Admin");
            var filter = createUserRoleFilter(context, "Admin");
            var authContext = FilterContextTestHelper.CreateAuthorizationFilterContext(
                FilterContextTestHelper.CreateAuthenticatedHttpContext(user.Id));

            // Act
            await filter.OnAuthorizationAsync(authContext);

            // Assert - authorized requests leave the context untouched.
            Assert.IsNull(authContext.Result);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies role comparison is case-insensitive per the documented
        /// OrdinalIgnoreCase match.
        /// </summary>
        /// <seealso cref="UserRoleAuthorizationFilter.OnAuthorizationAsync"/>
        [TestMethod]
        public async Task UserRoleAuthorizationFilter_OnAuthorizationAsync_CaseInsensitiveRole_AllowsRequest()
        {
            #region implementation
            // Arrange - attribute lists lowercase, user carries canonical casing.
            using var context = createContext();
            var user = seedUser(context, role: "Admin");
            var filter = createUserRoleFilter(context, "admin");
            var authContext = FilterContextTestHelper.CreateAuthorizationFilterContext(
                FilterContextTestHelper.CreateAuthenticatedHttpContext(user.Id));

            // Act
            await filter.OnAuthorizationAsync(authContext);

            // Assert
            Assert.IsNull(authContext.Result);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies a role mismatch is rejected with the 403 insufficient-role
        /// exception carrying the user's actual role.
        /// </summary>
        /// <seealso cref="UserRoleAuthorizationFilter.OnAuthorizationAsync"/>
        [TestMethod]
        public async Task UserRoleAuthorizationFilter_OnAuthorizationAsync_WrongRole_ThrowsUserRoleAuthorizationException()
        {
            #region implementation
            // Arrange - "User" role requesting an Admin-only resource.
            using var context = createContext();
            var user = seedUser(context, role: "User");
            var filter = createUserRoleFilter(context, "Admin");
            var authContext = FilterContextTestHelper.CreateAuthorizationFilterContext(
                FilterContextTestHelper.CreateAuthenticatedHttpContext(user.Id));

            // Act + Assert
            var exception = await Assert.ThrowsExceptionAsync<UserRoleAuthorizationException>(
                () => filter.OnAuthorizationAsync(authContext));

            Assert.AreEqual(403, exception.StatusCode);
            Assert.AreEqual("AUTH_ROLE_INSUFFICIENT", exception.ErrorCode);
            Assert.AreEqual("User", exception.ActualRole);
            CollectionAssert.AreEqual(new[] { "Admin" }, exception.RequiredRoles);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies a data-access failure during user lookup surfaces as the
        /// base 500 authorization exception with the role error code.
        /// </summary>
        /// <remarks>
        /// Disposing the DbContext before the filter runs makes the user query
        /// throw, which the filter wraps as a 500 AUTH_ROLE_ERROR.
        /// </remarks>
        /// <seealso cref="UserRoleAuthorizationFilter.OnAuthorizationAsync"/>
        [TestMethod]
        public async Task UserRoleAuthorizationFilter_OnAuthorizationAsync_DataAccessFailure_ThrowsAuthorizationException()
        {
            #region implementation
            // Arrange - context disposed up front to force the query to throw.
            var context = createContext();
            var filter = createUserRoleFilter(context, "Admin");
            context.Dispose();

            var authContext = FilterContextTestHelper.CreateAuthorizationFilterContext(
                FilterContextTestHelper.CreateAuthenticatedHttpContext(1));

            // Act + Assert
            var exception = await Assert.ThrowsExceptionAsync<AuthorizationException>(
                () => filter.OnAuthorizationAsync(authContext));

            Assert.AreEqual(500, exception.StatusCode);
            Assert.AreEqual("AUTH_ROLE_ERROR", exception.ErrorCode);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies the filter constructor rejects a missing primary-key cipher.
        /// </summary>
        /// <seealso cref="UserRoleAuthorizationFilter"/>
        [TestMethod]
        public void UserRoleAuthorizationFilter_Constructor_MissingPrimaryKeyCipher_ThrowsArgumentNull()
        {
            #region implementation
            // Arrange - encryption configuration is validated by the shared cipher.
            using var context = createContext();

            // Act + Assert
            Assert.ThrowsException<ArgumentNullException>(() =>
                new UserRoleAuthorizationFilter(
                    new[] { "Admin" },
                    createUserDataAccess(context),
                    null!,
                    NullLogger<UserRoleAuthorizationFilter>.Instance));
            #endregion
        }

        #endregion

        #region ActorAuthorizationFilter

        /**************************************************************/
        /// <summary>
        /// Verifies a SystemAdmin actor bypasses the allowed-actor list
        /// entirely.
        /// </summary>
        /// <seealso cref="ActorAuthorizationFilter.OnAuthorizationAsync"/>
        [TestMethod]
        public async Task ActorAuthorizationFilter_OnAuthorizationAsync_SystemAdminActor_AllowsRequest()
        {
            #region implementation
            // Arrange - user holds SystemAdmin; attribute allows only LabelAdmin.
            using var context = createContext();
            var permissionService = createPermissionService();
            var user = seedUser(context, role: "Admin", permissions: permissionService.Encrypt(
                new List<Permission> { Permission.New(ActorType.SystemAdmin, "system", PermissionType.Own) }));

            var filter = createActorFilter(context, permissionService, "LabelAdmin");
            var authContext = FilterContextTestHelper.CreateAuthorizationFilterContext(
                FilterContextTestHelper.CreateAuthenticatedHttpContext(user.Id));

            // Act
            await filter.OnAuthorizationAsync(authContext);

            // Assert
            Assert.IsNull(authContext.Result);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies a user holding one of the allowed actor types is
        /// authorized (case-insensitive actor-name parsing).
        /// </summary>
        /// <seealso cref="ActorAuthorizationFilter.OnAuthorizationAsync"/>
        [TestMethod]
        public async Task ActorAuthorizationFilter_OnAuthorizationAsync_MatchingActor_AllowsRequest()
        {
            #region implementation
            // Arrange - LabelAdmin permission, attribute allows "labeladmin".
            using var context = createContext();
            var permissionService = createPermissionService();
            var user = seedUser(context, role: "User", permissions: permissionService.Encrypt(
                new List<Permission> { Permission.New(ActorType.LabelAdmin, "labels", PermissionType.Read) }));

            var filter = createActorFilter(context, permissionService, "labeladmin");
            var authContext = FilterContextTestHelper.CreateAuthorizationFilterContext(
                FilterContextTestHelper.CreateAuthenticatedHttpContext(user.Id));

            // Act
            await filter.OnAuthorizationAsync(authContext);

            // Assert
            Assert.IsNull(authContext.Result);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies the two rejection shapes: an unauthenticated request gets
        /// the 401 exception and a mismatched actor gets the 403 exception.
        /// </summary>
        /// <seealso cref="ActorAuthorizationFilter.OnAuthorizationAsync"/>
        [TestMethod]
        public async Task ActorAuthorizationFilter_OnAuthorizationAsync_MissingOrWrongActor_ThrowsActorAuthorizationException()
        {
            #region implementation
            // Arrange
            using var context = createContext();
            var permissionService = createPermissionService();
            var filter = createActorFilter(context, permissionService, "LabelAdmin");

            // Act + Assert - missing claim: 401 unauthenticated.
            var anonymousContext = FilterContextTestHelper.CreateAuthorizationFilterContext(
                FilterContextTestHelper.CreateAnonymousHttpContext());
            var unauthenticated = await Assert.ThrowsExceptionAsync<ActorAuthorizationException>(
                () => filter.OnAuthorizationAsync(anonymousContext));

            Assert.AreEqual(401, unauthenticated.StatusCode);
            Assert.AreEqual("AUTH_ACTOR_UNAUTHENTICATED", unauthenticated.ErrorCode);

            // Act + Assert - wrong actor type: 403 insufficient.
            var user = seedUser(context, role: "User", permissions: permissionService.Encrypt(
                new List<Permission> { Permission.New(ActorType.Consumer, "labels", PermissionType.Read) }));
            var wrongActorContext = FilterContextTestHelper.CreateAuthorizationFilterContext(
                FilterContextTestHelper.CreateAuthenticatedHttpContext(user.Id));
            var insufficient = await Assert.ThrowsExceptionAsync<ActorAuthorizationException>(
                () => filter.OnAuthorizationAsync(wrongActorContext));

            Assert.AreEqual(403, insufficient.StatusCode);
            Assert.AreEqual("AUTH_ACTOR_INSUFFICIENT", insufficient.ErrorCode);
            CollectionAssert.AreEqual(new[] { "LabelAdmin" }, insufficient.RequiredActors);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies a user pointing at a nonexistent record is rejected with
        /// the 401 unauthenticated actor exception.
        /// </summary>
        /// <seealso cref="ActorAuthorizationFilter.OnAuthorizationAsync"/>
        [TestMethod]
        public async Task ActorAuthorizationFilter_OnAuthorizationAsync_UserNotFound_ThrowsActorAuthorizationException()
        {
            #region implementation
            // Arrange
            using var context = createContext();
            var filter = createActorFilter(context, createPermissionService(), "LabelAdmin");
            var authContext = FilterContextTestHelper.CreateAuthorizationFilterContext(
                FilterContextTestHelper.CreateAuthenticatedHttpContext(424242));

            // Act + Assert
            var exception = await Assert.ThrowsExceptionAsync<ActorAuthorizationException>(
                () => filter.OnAuthorizationAsync(authContext));

            Assert.AreEqual(401, exception.StatusCode);
            Assert.AreEqual("AUTH_ACTOR_UNAUTHENTICATED", exception.ErrorCode);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies undecryptable permission payloads degrade to an empty
        /// permission list, which fails authorization with 403.
        /// </summary>
        /// <seealso cref="ActorAuthorizationFilter.OnAuthorizationAsync"/>
        [TestMethod]
        public async Task ActorAuthorizationFilter_OnAuthorizationAsync_UndecryptablePermissions_ThrowsInsufficient()
        {
            #region implementation
            // Arrange - garbage ciphertext makes TryDecrypt return false.
            using var context = createContext();
            var permissionService = createPermissionService();
            var user = seedUser(context, role: "User", permissions: "not-cipher-text");

            var filter = createActorFilter(context, permissionService, "LabelAdmin");
            var authContext = FilterContextTestHelper.CreateAuthorizationFilterContext(
                FilterContextTestHelper.CreateAuthenticatedHttpContext(user.Id));

            // Act + Assert
            var exception = await Assert.ThrowsExceptionAsync<ActorAuthorizationException>(
                () => filter.OnAuthorizationAsync(authContext));

            Assert.AreEqual(403, exception.StatusCode);
            Assert.AreEqual("AUTH_ACTOR_INSUFFICIENT", exception.ErrorCode);
            #endregion
        }

        #endregion

        #region AuthorizationExceptionFilter

        /**************************************************************/
        /// <summary>
        /// Verifies OnException maps a role exception to an ObjectResult that
        /// mirrors the exception's status, code, roles, and actual role.
        /// </summary>
        /// <seealso cref="AuthorizationExceptionFilter.OnException"/>
        [TestMethod]
        public void AuthorizationExceptionFilter_OnException_UserRoleException_ReturnsExpectedObjectResult()
        {
            #region implementation
            // Arrange - two-arg ctor: 403 insufficient with actual role.
            var filter = new AuthorizationExceptionFilter(NullLogger<AuthorizationExceptionFilter>.Instance);
            var exception = new UserRoleAuthorizationException(new[] { "Admin", "User Admin" }, "Viewer");
            var exceptionContext = FilterContextTestHelper.CreateExceptionContext(
                FilterContextTestHelper.CreateHttpContext(), exception);

            // Act
            filter.OnException(exceptionContext);

            // Assert
            var result = exceptionContext.Result as ObjectResult;
            Assert.IsNotNull(result);
            Assert.AreEqual(403, result!.StatusCode);
            Assert.IsTrue(exceptionContext.ExceptionHandled);

            Assert.AreEqual("AUTH_ROLE_INSUFFICIENT",
                FilterContextTestHelper.GetAnonymousPropertyValue(result.Value!, "errorCode"));
            Assert.AreEqual(403,
                FilterContextTestHelper.GetAnonymousPropertyValue(result.Value!, "statusCode"));
            Assert.AreEqual("Viewer",
                FilterContextTestHelper.GetAnonymousPropertyValue(result.Value!, "actualRole"));
            CollectionAssert.AreEqual(new[] { "Admin", "User Admin" },
                (string[])FilterContextTestHelper.GetAnonymousPropertyValue(result.Value!, "requiredRoles")!);
            Assert.AreEqual(exception.Message,
                FilterContextTestHelper.GetAnonymousPropertyValue(result.Value!, "error"));
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies OnException maps an actor exception to an ObjectResult
        /// carrying the required actors.
        /// </summary>
        /// <seealso cref="AuthorizationExceptionFilter.OnException"/>
        [TestMethod]
        public void AuthorizationExceptionFilter_OnException_ActorException_ReturnsExpectedObjectResult()
        {
            #region implementation
            // Arrange - single-arg ctor: 403 insufficient actors.
            var filter = new AuthorizationExceptionFilter(NullLogger<AuthorizationExceptionFilter>.Instance);
            var exception = new ActorAuthorizationException(new[] { "LabelAdmin" });
            var exceptionContext = FilterContextTestHelper.CreateExceptionContext(
                FilterContextTestHelper.CreateHttpContext(), exception);

            // Act
            filter.OnException(exceptionContext);

            // Assert
            var result = exceptionContext.Result as ObjectResult;
            Assert.IsNotNull(result);
            Assert.AreEqual(403, result!.StatusCode);
            Assert.IsTrue(exceptionContext.ExceptionHandled);

            Assert.AreEqual("AUTH_ACTOR_INSUFFICIENT",
                FilterContextTestHelper.GetAnonymousPropertyValue(result.Value!, "errorCode"));
            CollectionAssert.AreEqual(new[] { "LabelAdmin" },
                (string[])FilterContextTestHelper.GetAnonymousPropertyValue(result.Value!, "requiredActors")!);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies OnException maps a base authorization exception (custom
        /// status and error code) to a matching ObjectResult without the
        /// role/actor extras.
        /// </summary>
        /// <seealso cref="AuthorizationExceptionFilter.OnException"/>
        [TestMethod]
        public void AuthorizationExceptionFilter_OnException_BaseAuthorizationException_ReturnsExpectedObjectResult()
        {
            #region implementation
            // Arrange - the 500 wrap the auth filters produce on lookup errors.
            var filter = new AuthorizationExceptionFilter(NullLogger<AuthorizationExceptionFilter>.Instance);
            var exception = new AuthorizationException(
                "An error occurred while validating user authorization.",
                new InvalidOperationException("inner"),
                statusCode: 500,
                errorCode: "AUTH_ACTOR_ERROR");
            var exceptionContext = FilterContextTestHelper.CreateExceptionContext(
                FilterContextTestHelper.CreateHttpContext(), exception);

            // Act
            filter.OnException(exceptionContext);

            // Assert
            var result = exceptionContext.Result as ObjectResult;
            Assert.IsNotNull(result);
            Assert.AreEqual(500, result!.StatusCode);
            Assert.IsTrue(exceptionContext.ExceptionHandled);

            Assert.AreEqual("AUTH_ACTOR_ERROR",
                FilterContextTestHelper.GetAnonymousPropertyValue(result.Value!, "errorCode"));
            Assert.AreEqual(500,
                FilterContextTestHelper.GetAnonymousPropertyValue(result.Value!, "statusCode"));

            // Base branch omits the role/actor fields entirely.
            Assert.IsNull(result.Value!.GetType().GetProperty("requiredRoles"));
            Assert.IsNull(result.Value!.GetType().GetProperty("requiredActors"));
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies non-authorization exceptions pass through unhandled so the
        /// framework's regular error handling applies.
        /// </summary>
        /// <seealso cref="AuthorizationExceptionFilter.OnException"/>
        [TestMethod]
        public void AuthorizationExceptionFilter_OnException_NonAuthorizationException_LeavesUnhandled()
        {
            #region implementation
            // Arrange
            var filter = new AuthorizationExceptionFilter(NullLogger<AuthorizationExceptionFilter>.Instance);
            var exceptionContext = FilterContextTestHelper.CreateExceptionContext(
                FilterContextTestHelper.CreateHttpContext(), new InvalidOperationException("unrelated"));

            // Act
            filter.OnException(exceptionContext);

            // Assert - untouched context lets the exception propagate.
            Assert.IsNull(exceptionContext.Result);
            Assert.IsFalse(exceptionContext.ExceptionHandled);
            #endregion
        }

        #endregion

        #region Harness helpers

        /**************************************************************/
        /// <summary>
        /// Builds the in-memory configuration carrying the shared PK secret.
        /// </summary>
        /// <returns>Configuration with Security:DB:PKSecret set.</returns>
        /// <seealso cref="UserDataAccess"/>
        private static IConfiguration createConfiguration()
        {
            #region implementation
            return new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Security:DB:PKSecret"] = TestPkSecret
                })
                .Build();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates a uniquely named EF InMemory <see cref="ApplicationDbContext"/>.
        /// </summary>
        /// <returns>A fresh context isolated from other tests.</returns>
        private static ApplicationDbContext createContext()
        {
            #region implementation
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: $"AuthorizationFilterTests_{Guid.NewGuid():N}")
                .Options;

            return new ApplicationDbContext(options);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates a real <see cref="UserDataAccess"/> over the supplied
        /// context using the shared secret configuration.
        /// </summary>
        /// <param name="context">The backing context.</param>
        /// <returns>A functional user data-access instance.</returns>
        /// <seealso cref="UserDataAccess.GetByIdAsync"/>
        private static UserDataAccess createUserDataAccess(ApplicationDbContext context)
        {
            #region implementation
            return new UserDataAccess(
                context,
                new PasswordHasher<User>(),
                new Mock<Microsoft.Extensions.Logging.ILogger<UserDataAccess>>().Object,
                createConfiguration());
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates a real <see cref="PermissionService"/> bound to the shared
        /// secret so encrypted payloads round-trip inside the filters.
        /// </summary>
        /// <returns>A functional permission service.</returns>
        /// <seealso cref="PermissionService.Encrypt"/>
        private static PermissionService createPermissionService()
        {
            #region implementation
            return new PermissionService(
                createConfiguration(),
                NullLogger<PermissionService>.Instance,
                new StringCipher());
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates a <see cref="UserRoleAuthorizationFilter"/> over a real
        /// user data-access instance.
        /// </summary>
        /// <param name="context">Backing context for user lookups.</param>
        /// <param name="allowedRoles">Roles granted access.</param>
        /// <returns>The configured filter.</returns>
        private static UserRoleAuthorizationFilter createUserRoleFilter(
            ApplicationDbContext context, params string[] allowedRoles)
        {
            #region implementation
            return new UserRoleAuthorizationFilter(
                allowedRoles,
                createUserDataAccess(context),
                createPrimaryKeyCipher(),
                NullLogger<UserRoleAuthorizationFilter>.Instance);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates an <see cref="ActorAuthorizationFilter"/> over a real user
        /// data-access instance and the supplied permission service.
        /// </summary>
        /// <param name="context">Backing context for user lookups.</param>
        /// <param name="permissionService">Permission service shared with seeding.</param>
        /// <param name="allowedActors">Actor types granted access.</param>
        /// <returns>The configured filter.</returns>
        private static ActorAuthorizationFilter createActorFilter(
            ApplicationDbContext context, IPermissionService permissionService, params string[] allowedActors)
        {
            #region implementation
            return new ActorAuthorizationFilter(
                allowedActors,
                createUserDataAccess(context),
                permissionService,
                createPrimaryKeyCipher(),
                NullLogger<ActorAuthorizationFilter>.Instance);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates the primary-key cipher shared by the authorization filters.
        /// </summary>
        /// <returns>A cipher configured with the test database-security secret.</returns>
        /// <seealso cref="IPrimaryKeyCipher"/>
        private static IPrimaryKeyCipher createPrimaryKeyCipher()
        {
            #region implementation
            return new PrimaryKeyCipher(Microsoft.Extensions.Options.Options.Create(
                new MedRecPro.Configuration.DatabaseSecurityOptions
                {
                    PKSecret = TestPkSecret
                }));
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Seeds a user row with the supplied role and optional encrypted
        /// permission payload.
        /// </summary>
        /// <param name="context">Context to insert into.</param>
        /// <param name="role">UserRole value (e.g. "Admin", "User").</param>
        /// <param name="permissions">Encrypted permission JSON or null.</param>
        /// <returns>The saved user with a populated Id.</returns>
        /// <seealso cref="UserDataAccess.GetByIdAsync"/>
        private static User seedUser(ApplicationDbContext context, string role, string? permissions = null)
        {
            #region implementation
            var unique = Guid.NewGuid().ToString("N");
            var user = new User
            {
                PrimaryEmail = $"filter.tests.{unique}@example.com",
                DisplayName = $"Filter Test {unique}",
                CanonicalUsername = $"filter.tests.{unique}",
                UserRole = role,
                Timezone = "UTC",
                Locale = "en-US",
                CreatedAt = DateTime.UtcNow,
                SecurityStamp = Guid.NewGuid().ToString()
            };

            if (!string.IsNullOrEmpty(permissions))
            {
                user.UserPermissions = permissions;
            }

            context.AppUsers.Add(user);
            context.SaveChanges();

            return user;
            #endregion
        }

        #endregion

        #endregion
    }
}
