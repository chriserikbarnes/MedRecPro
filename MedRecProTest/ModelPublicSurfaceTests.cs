using MedRecPro.Helpers;
using MedRecPro.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Text;
using static MedRecPro.Models.Label;

namespace MedRecPro.Service.Test
{
    /**************************************************************/
    /// <summary>
    /// Tests public model conversion and helper methods.
    /// </summary>
    /// <remarks>
    /// Covers model methods listed in the public surface coverage plan using local
    /// in-memory objects and temporary files only.
    /// </remarks>
    /// <seealso cref="ActivityLogDto"/>
    /// <seealso cref="BufferedFile"/>
    /// <seealso cref="NewUser"/>
    /// <seealso cref="User"/>
    [TestClass]
    public class ModelPublicSurfaceTests
    {
        #region implementation

        private const string TestSecret = "ModelPublicSurfaceTests-Fixed-Secret";

        /**************************************************************/
        /// <summary>
        /// Verifies NewUser conversion maps identity and custom fields and hashes passwords.
        /// </summary>
        /// <seealso cref="NewUser.ToUser"/>
        [TestMethod]
        public void ToUser_NewUser_MapsIdentityFieldsAndHashesPassword()
        {
            #region implementation
            var dto = new NewUser
            {
                PrimaryEmail = "Person@Example.Test",
                Password = "Passw0rd!",
                CanonicalUsername = "PERSON",
                DisplayName = "Person Example",
                PhoneNumber = "2025550123",
                MfaEnabled = true,
                UserRole = "Admin",
                Timezone = "America/New_York",
                Locale = "en-US"
            };
            var hasher = new PasswordHasher<User>();

            var user = dto.ToUser(hasher);

            Assert.AreEqual("Person@Example.Test", user.UserName);
            Assert.AreEqual("PERSON@EXAMPLE.TEST", user.NormalizedUserName);
            Assert.AreEqual("person", user.CanonicalUsername);
            Assert.AreEqual("Person Example", user.DisplayName);
            Assert.AreEqual("2025550123", user.PhoneNumber);
            Assert.IsTrue(user.MfaEnabled);
            Assert.IsTrue(user.TwoFactorEnabled);
            Assert.IsFalse(string.IsNullOrWhiteSpace(user.PasswordHash));
            Assert.AreEqual(PasswordVerificationResult.Success, hasher.VerifyHashedPassword(user, user.PasswordHash!, "Passw0rd!"));
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies User admin role checks are case-insensitive and null-safe.
        /// </summary>
        /// <seealso cref="User.IsUserAdmin"/>
        [TestMethod]
        public void IsUserAdmin_AdminUserAdminAndUserRoles_ReturnExpectedBooleans()
        {
            #region implementation
            Assert.IsTrue(new User { UserRole = "admin" }.IsUserAdmin());
            Assert.IsTrue(new User { UserRole = "User Admin" }.IsUserAdmin());
            Assert.IsFalse(new User { UserRole = "User" }.IsUserAdmin());
            Assert.IsFalse(new User { UserRole = string.Empty }.IsUserAdmin());
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies UserFacingUpdateDto conversion maps user-facing fields.
        /// </summary>
        /// <seealso cref="UserFacingUpdateDto.ToUser"/>
        [TestMethod]
        public void ToUser_UserFacingUpdateDto_MapsEditableFields()
        {
            #region implementation
            var dto = new UserFacingUpdateDto
            {
                EncryptedUserId = "encrypted-user-id",
                DisplayName = "Updated User",
                PrimaryEmail = "updated@example.test",
                MfaEnabled = true,
                Timezone = "UTC",
                Locale = "en-US",
                NotificationSettings = "{}",
                UiTheme = "dark",
                UserFollowing = "[]",
                UserName = "updated@example.test",
                Email = "updated@example.test",
                PhoneNumber = "2025550123",
                TwoFactorEnabled = true
            };

            var user = dto.ToUser();

            Assert.AreEqual("Updated User", user.DisplayName);
            Assert.AreEqual("updated@example.test", user.PrimaryEmail);
            Assert.IsTrue(user.MfaEnabled);
            Assert.AreEqual("UTC", user.Timezone);
            Assert.AreEqual("dark", user.UiTheme);
            Assert.AreEqual("encrypted-user-id", user.EncryptedUserId);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies ActivityLogDto transforms activity logs into encrypted dictionaries.
        /// </summary>
        /// <seealso cref="ActivityLogDto.FromActivityLogs"/>
        [TestMethod]
        public void FromActivityLogs_ActivityLogCollection_ReturnsEncryptedDictionaryRows()
        {
            #region implementation
            var activityLogs = new List<ActivityLog>
            {
                new()
                {
                    ActivityLogId = 100,
                    UserId = 200,
                    User = new User
                    {
                        Email = "user@example.test",
                        DisplayName = "User Example",
                        PrimaryEmail = "user@example.test"
                    },
                    ActivityType = "Read",
                    ActivityTimestamp = new DateTime(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc),
                    ControllerName = "Labels",
                    ActionName = "Get",
                    HttpMethod = "GET",
                    Result = "Success"
                }
            };

            var result = ActivityLogDto.FromActivityLogs(activityLogs, TestSecret, NullLogger.Instance);

            Assert.AreEqual(1, result.Count);
            Assert.IsTrue(result[0].ContainsKey("EncryptedId"));
            Assert.IsTrue(result[0].ContainsKey("EncryptedUserId"));
            Assert.AreEqual("user@example.test", result[0]["Email"]);
            Assert.AreEqual("User Example", result[0]["DisplayName"]);
            Assert.AreEqual("100", result[0]["EncryptedId"]?.ToString()?.Decrypt(TestSecret));
            Assert.AreEqual("200", result[0]["EncryptedUserId"]?.ToString()?.Decrypt(TestSecret));
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies BufferedFile copies uploaded content to temporary files and preserves names.
        /// </summary>
        /// <seealso cref="BufferedFile.BufferFilesToTempAsync"/>
        [TestMethod]
        public async Task BufferFilesToTempAsync_FormFiles_CopiesContentToTempFiles()
        {
            #region implementation
            var content = "buffered file content";
            await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
            var formFile = new FormFile(stream, 0, stream.Length, "file", "label.xml");
            var sut = new BufferedFile();
            List<BufferedFile>? bufferedFiles = null;

            try
            {
                bufferedFiles = await sut.BufferFilesToTempAsync(new List<IFormFile> { formFile }, CancellationToken.None);

                Assert.AreEqual(1, bufferedFiles.Count);
                Assert.AreEqual("label.xml", bufferedFiles[0].FileName);
                Assert.IsTrue(File.Exists(bufferedFiles[0].TempFilePath));
                Assert.AreEqual(content, await File.ReadAllTextAsync(bufferedFiles[0].TempFilePath));
            }
            finally
            {
                foreach (var bufferedFile in bufferedFiles ?? new List<BufferedFile>())
                {
                    if (File.Exists(bufferedFile.TempFilePath))
                    {
                        File.Delete(bufferedFile.TempFilePath);
                    }
                }
            }
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies text table columns prefer local values over colgroup fallback values.
        /// </summary>
        /// <seealso cref="TextTableColumn.GetEffectiveStyleCode"/>
        /// <seealso cref="TextTableColumn.GetEffectiveAlign"/>
        /// <seealso cref="TextTableColumn.GetEffectiveVAlign"/>
        [TestMethod]
        public void TextTableColumnEffectiveValues_LocalAndFallbackValues_ReturnExpectedValues()
        {
            #region implementation
            var fallback = new TextTableColumn
            {
                ColGroupStyleCode = "fallback-style",
                ColGroupAlign = "center",
                ColGroupVAlign = "top"
            };
            var local = new TextTableColumn
            {
                StyleCode = "local-style",
                Align = "right",
                VAlign = "bottom",
                ColGroupStyleCode = "fallback-style",
                ColGroupAlign = "center",
                ColGroupVAlign = "top"
            };

            Assert.AreEqual("fallback-style", fallback.GetEffectiveStyleCode());
            Assert.AreEqual("center", fallback.GetEffectiveAlign());
            Assert.AreEqual("top", fallback.GetEffectiveVAlign());
            Assert.AreEqual("local-style", local.GetEffectiveStyleCode());
            Assert.AreEqual("right", local.GetEffectiveAlign());
            Assert.AreEqual("bottom", local.GetEffectiveVAlign());
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies warning letter validation helpers return the same model for fluent validation.
        /// </summary>
        /// <seealso cref="WarningLetterProductInfo.ValidateAll"/>
        /// <seealso cref="WarningLetterDate.ValidateAll"/>
        [TestMethod]
        public void WarningLetterValidateAll_ValidModels_ReturnSameInstances()
        {
            #region implementation
            var productInfo = new WarningLetterProductInfo
            {
                ProductName = "Product",
                GenericName = "Generic",
                FormCode = "C42972",
                ItemCodesText = "NDC 12345-6789"
            };
            var warningDate = new WarningLetterDate
            {
                AlertIssueDate = new DateTime(2026, 7, 1),
                ResolutionDate = new DateTime(2026, 7, 2)
            };

            Assert.AreSame(productInfo, productInfo.ValidateAll());
            Assert.AreSame(warningDate, warningDate.ValidateAll());
            #endregion
        }

        #endregion
    }
}
