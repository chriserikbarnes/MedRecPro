using MedRecPro.Helpers;
using MedRecPro.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static MedRecPro.Models.Constant;

namespace MedRecPro.Service.Test
{
    /**************************************************************/
    /// <summary>
    /// Tests public permission service methods with deterministic in-memory configuration.
    /// </summary>
    /// <seealso cref="PermissionService"/>
    /// <seealso cref="Permission"/>
    [TestClass]
    public class PermissionServiceTests
    {
        #region implementation

        private const string TestSecret = "PermissionServiceTests-Fixed-Secret";

        /**************************************************************/
        /// <summary>
        /// Verifies permission, actor, and role validation paths.
        /// </summary>
        /// <seealso cref="PermissionService.HasPermission"/>
        /// <seealso cref="PermissionService.HasAnyActorType"/>
        /// <seealso cref="PermissionService.ValidateUserRole"/>
        /// <seealso cref="PermissionService.GetActorTypes"/>
        [TestMethod]
        public void ValidationMethods_MatchingAndMissingPermissions_ReturnExpectedBooleans()
        {
            #region implementation
            var service = createService();
            var permissions = new List<Permission>
            {
                Permission.New(ActorType.LabelAdmin, "labels", PermissionType.Read, maskedPII: false),
                Permission.New(ActorType.SystemAdmin, "system", PermissionType.Own)
            };

            Assert.IsTrue(service.HasPermission(permissions, ActorType.LabelAdmin, "labels", PermissionType.Read, maskedPII: false));
            Assert.IsFalse(service.HasPermission(permissions, ActorType.LabelAdmin, "labels", PermissionType.Write, maskedPII: false));
            Assert.IsTrue(service.HasPermission(permissions, ActorType.SystemAdmin, "anything", PermissionType.Delete));
            Assert.IsTrue(service.HasAnyActorType(permissions, ActorType.LabelManager, ActorType.LabelAdmin));
            Assert.IsTrue(service.HasAnyActorType(permissions, ActorType.Patient));
            Assert.IsTrue(service.ValidateUserRole(new User { UserRole = "Admin" }, "admin", "User"));
            Assert.IsFalse(service.ValidateUserRole(new User { UserRole = "User" }, "Admin"));
            CollectionAssert.AreEquivalent(
                new List<ActorType> { ActorType.LabelAdmin, ActorType.SystemAdmin },
                service.GetActorTypes(permissions));
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies append, remove, clone, JSON, and encryption round-trip behavior.
        /// </summary>
        /// <seealso cref="PermissionService.Append"/>
        /// <seealso cref="PermissionService.Remove"/>
        /// <seealso cref="PermissionService.Clone"/>
        /// <seealso cref="PermissionService.ToJson"/>
        /// <seealso cref="PermissionService.FromJson"/>
        /// <seealso cref="PermissionService.Encrypt"/>
        /// <seealso cref="PermissionService.Decrypt"/>
        /// <seealso cref="PermissionService.TryDecrypt"/>
        [TestMethod]
        public void MutationSerializationAndEncryptionMethods_RoundTripPermissions()
        {
            #region implementation
            var service = createService();
            var permission = Permission.New(ActorType.LabelAdmin, "labels", PermissionType.Read, maskedPII: false);
            var permissions = service.Append(new List<Permission>(), permission);

            service.Append(permissions, permission);
            Assert.AreEqual(1, permissions.Count);

            var json = service.ToJson(permissions);
            var fromJson = service.FromJson(json);
            var clone = service.Clone(permissions);
            var encrypted = service.Encrypt(permissions);
            var decryptSucceeded = service.TryDecrypt(encrypted, out var decrypted);
            var invalidSucceeded = service.TryDecrypt("not-cipher-text", out var invalidResult);

            Assert.AreEqual(1, fromJson.Count);
            Assert.AreEqual(1, clone.Count);
            Assert.AreNotSame(permissions[0], clone[0]);
            Assert.IsTrue(decryptSucceeded);
            Assert.AreEqual(1, decrypted.Count);
            Assert.IsFalse(invalidSucceeded);
            Assert.AreEqual(0, invalidResult.Count);

            var removed = service.Remove(permissions, permission);
            Assert.AreEqual(0, removed.Count);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates the service under test with deterministic configuration.
        /// </summary>
        /// <returns>A permission service.</returns>
        /// <seealso cref="PermissionService"/>
        private static PermissionService createService()
        {
            #region implementation
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Security:DB:PKSecret"] = TestSecret
                })
                .Build();

            return new PermissionService(
                configuration,
                NullLogger<PermissionService>.Instance,
                new StringCipher());
            #endregion
        }

        #endregion
    }
}
