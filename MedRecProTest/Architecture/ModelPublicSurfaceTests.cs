using MedRecPro.Helpers;
using MedRecPro.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Text;
using static MedRecPro.Models.Constant;
using static MedRecPro.Models.Label;

namespace MedRecProTest.Architecture
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
    [TestCategory("Architecture")]
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

        /**************************************************************/
        /// <summary>
        /// Verifies Permission.New maps all supplied permission fields.
        /// </summary>
        /// <seealso cref="Permission.New"/>
        [TestMethod]
        public void New_PermissionFields_ReturnsInitializedPermission()
        {
            #region implementation
            var permission = Permission.New(
                ActorType.LabelAdmin,
                "labels",
                PermissionType.Write,
                maskedPII: false);

            Assert.AreEqual(ActorType.LabelAdmin, permission.Actor);
            Assert.AreEqual("labels", permission.Resource);
            Assert.AreEqual(PermissionType.Write, permission.Type);
            Assert.IsFalse(permission.MaskedPII);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies SectionRendering returns child sections as ordered by the hierarchy service.
        /// </summary>
        /// <seealso cref="SectionRendering.GetOrderedChildren"/>
        [TestMethod]
        public void GetOrderedChildren_NoChildrenAndExistingChildren_ReturnsExpectedLists()
        {
            #region implementation
            var empty = new SectionRendering
            {
                Section = new SectionDto { Section = new Dictionary<string, object?>() }
            };
            var children = new List<SectionDto>
            {
                new() { Section = new Dictionary<string, object?> { [nameof(SectionDto.Title)] = "First" } },
                new() { Section = new Dictionary<string, object?> { [nameof(SectionDto.Title)] = "Second" } }
            };
            var populated = new SectionRendering
            {
                Section = new SectionDto { Section = new Dictionary<string, object?>() },
                Children = children
            };

            Assert.AreEqual(0, empty.GetOrderedChildren().Count);
            Assert.AreSame(children, populated.GetOrderedChildren());
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies ApplicationNumberSearch.Parse decomposes combined,
        /// numeric-only, prefix-only, and null inputs into normalized search
        /// terms.
        /// </summary>
        /// <seealso cref="ApplicationNumberSearch.Parse"/>
        [TestMethod]
        public void ApplicationNumberSearch_Parse_DecomposesInputIntoSearchTerms()
        {
            #region implementation
            var combined = ApplicationNumberSearch.Parse("anda 125669");
            var numericOnly = ApplicationNumberSearch.Parse("125669");
            var prefixOnly = ApplicationNumberSearch.Parse(" NDA ");
            var empty = ApplicationNumberSearch.Parse(null);

            // Combined input normalizes casing/whitespace and splits parts.
            Assert.AreEqual("ANDA125669", combined.Normalized);
            Assert.AreEqual("125669", combined.NumericOnly);
            Assert.AreEqual("ANDA", combined.AlphaOnly);
            Assert.IsFalse(combined.IsNumericOnly);
            Assert.IsFalse(combined.IsPrefixOnly);

            // Numeric-only and prefix-only modes.
            Assert.IsTrue(numericOnly.IsNumericOnly);
            Assert.IsFalse(numericOnly.IsPrefixOnly);
            Assert.IsTrue(prefixOnly.IsPrefixOnly);
            Assert.AreEqual("NDA", prefixOnly.Normalized);

            // Null input yields empty terms with both flags false.
            Assert.AreEqual(string.Empty, empty.Normalized);
            Assert.IsFalse(empty.IsNumericOnly);
            Assert.IsFalse(empty.IsPrefixOnly);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies DosingSpecification.Validate enforces the paired dose
        /// quantity value/unit rule from SPL IG 16.2.4.3.
        /// </summary>
        /// <seealso cref="DosingSpecification"/>
        [TestMethod]
        public void DosingSpecification_Validate_EnforcesPairedDoseQuantityValueAndUnit()
        {
            #region implementation
            var valueWithoutUnit = new DosingSpecification { DoseQuantityValue = 5m };
            var unitWithoutValue = new DosingSpecification { DoseQuantityUnit = "mg" };
            var paired = new DosingSpecification { DoseQuantityValue = 5m, DoseQuantityUnit = "mg" };

            var missingUnit = valueWithoutUnit.Validate(
                new System.ComponentModel.DataAnnotations.ValidationContext(valueWithoutUnit)).ToList();
            var missingValue = unitWithoutValue.Validate(
                new System.ComponentModel.DataAnnotations.ValidationContext(unitWithoutValue)).ToList();
            var complete = paired.Validate(
                new System.ComponentModel.DataAnnotations.ValidationContext(paired)).ToList();

            Assert.AreEqual(1, missingUnit.Count);
            CollectionAssert.Contains(missingUnit[0].MemberNames.ToList(), nameof(DosingSpecification.DoseQuantityUnit));
            Assert.AreEqual(1, missingValue.Count);
            CollectionAssert.Contains(missingValue[0].MemberNames.ToList(), nameof(DosingSpecification.DoseQuantityValue));
            Assert.AreEqual(0, complete.Count);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies ProductEvent.Validate delegates to the validation service
        /// when a logger is resolvable from the context and stays silent when
        /// it is not.
        /// </summary>
        /// <remarks>
        /// The IValidatableObject implementation resolves an
        /// ILogger&lt;ProductEvent&gt; from the validation context; without one
        /// it performs no validation and returns an empty result set.
        /// </remarks>
        /// <seealso cref="ProductEvent"/>
        [TestMethod]
        public void ProductEvent_Validate_UsesValidationServiceOnlyWhenLoggerAvailable()
        {
            #region implementation
            // Arrange - negative quantity violates SPL IG 16.2.9 rules.
            var invalidEvent = new ProductEvent { QuantityValue = -5 };
            var provider = new Microsoft.Extensions.DependencyInjection.ServiceCollection()
                .AddLogging()
                .BuildServiceProvider();

            // Act - without a logger the guard clause returns no results.
            var withoutLogger = invalidEvent.Validate(
                new System.ComponentModel.DataAnnotations.ValidationContext(invalidEvent)).ToList();

            // Act - with a logger the validation service reports the violation.
            var contextWithServices = new System.ComponentModel.DataAnnotations.ValidationContext(
                invalidEvent, provider, items: null);
            var withLogger = invalidEvent.Validate(contextWithServices).ToList();

            // Assert
            Assert.AreEqual(0, withoutLogger.Count, "No logger in the context means the service validation is skipped.");
            Assert.IsTrue(withLogger.Count > 0, "A negative quantity must produce at least one validation error.");
            #endregion
        }

        #endregion
    }
}
