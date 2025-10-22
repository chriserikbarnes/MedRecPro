using Microsoft.EntityFrameworkCore.Migrations;

/*
 * https://g.co/gemini/share/2c9d7863c397
 */

#nullable disable

namespace MedRecPro.Migrations
{
    /// <inheritdoc />
    /// <inheritdoc />
    public partial class UpdateAspNetUsersAndAddDescriptions : Migration
    {
        private const string TableName = "AspNetUsers";
        private const string SchemaName = "dbo";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Alter column types
            migrationBuilder.AlterColumn<string>(
                name: "MfaSecret",
                table: TableName,
                schema: SchemaName, // Specify schema for AlterColumn
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "UserPermissions",
                table: TableName,
                schema: SchemaName,
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "NotificationSettings",
                table: TableName,
                schema: SchemaName,
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "UserFollowing",
                table: TableName,
                schema: SchemaName,
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "PasswordHash",
                table: TableName,
                schema: SchemaName,
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "SecurityStamp",
                table: TableName,
                schema: SchemaName,
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ConcurrencyStamp",
                table: TableName,
                schema: SchemaName,
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "PhoneNumber",
                table: TableName,
                schema: SchemaName,
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            // Add Table-level description
            // CORRECTED CALL: Removed the trailing nulls to use default level types
            AddExtendedProperty(migrationBuilder, null,
                "Stores user account information for the application, including credentials, contact details, security settings, preferences, and audit trails, compatible with ASP.NET Identity.",
                SchemaName, TableName);

            // Add Column-level descriptions
            // (These calls were likely correct as they relied on defaults for level types)
            AddExtendedProperty(migrationBuilder, "Id", "Surrogate primary key (BIGINT IDENTITY) for the user.", SchemaName, TableName);
            AddExtendedProperty(migrationBuilder, "UserName", "User's chosen username, often used for login.", SchemaName, TableName);
            AddExtendedProperty(migrationBuilder, "NormalizedUserName", "Normalized (e.g., uppercase) version of UserName, used for efficient lookups and uniqueness checks.", SchemaName, TableName);
            AddExtendedProperty(migrationBuilder, "Email", "User's email address, may be used for login and communication.", SchemaName, TableName);
            AddExtendedProperty(migrationBuilder, "NormalizedEmail", "Normalized (e.g., uppercase) version of Email, used for efficient lookups.", SchemaName, TableName);
            AddExtendedProperty(migrationBuilder, "EmailConfirmed", "Flag indicating if the user's email address has been verified/confirmed (1 if true, 0 if false).", SchemaName, TableName);
            AddExtendedProperty(migrationBuilder, "PhoneNumberConfirmed", "Flag indicating if the user's phone number has been verified/confirmed (1 if true, 0 if false).", SchemaName, TableName);
            AddExtendedProperty(migrationBuilder, "TwoFactorEnabled", "Flag indicating if two-factor authentication is enabled for the user (1 if true, 0 if false).", SchemaName, TableName);
            AddExtendedProperty(migrationBuilder, "LockoutEnd", "UTC date and time until which the user is locked out, if lockout is enabled and triggered. This is a DATETIMEOFFSET.", SchemaName, TableName);
            AddExtendedProperty(migrationBuilder, "LockoutEnabled", "Flag indicating if account lockout is enabled for this user (1 if true, 0 if false).", SchemaName, TableName);
            AddExtendedProperty(migrationBuilder, "AccessFailedCount", "The number of consecutive failed login attempts for the user; reset on successful login. This is the standard Identity field for this purpose.", SchemaName, TableName);
            AddExtendedProperty(migrationBuilder, "ConcurrencyStamp", "A random value that changes whenever a user is persisted to the store, used for optimistic concurrency control.", SchemaName, TableName);
            AddExtendedProperty(migrationBuilder, "CanonicalUsername", "Lower-/case-folded username used for uniqueness checks and login. Typically mirrors NormalizedUserName.", SchemaName, TableName);
            AddExtendedProperty(migrationBuilder, "PhoneNumber", "User's phone number, optionally used for multi-factor authentication (MFA) or account recovery.", SchemaName, TableName);
            AddExtendedProperty(migrationBuilder, "DisplayName", "Friendly name displayed in the UI; can be non-unique and user-modifiable.", SchemaName, TableName);
            AddExtendedProperty(migrationBuilder, "PrimaryEmail", "Primary email address (RFC 5322 max length 320 chars). Often mirrors the Email field.", SchemaName, TableName);
            AddExtendedProperty(migrationBuilder, "PasswordHash", "Password hash produced by a strong one-way hashing algorithm (e.g., PBKDF2, bcrypt, Argon2id); plaintext passwords are never stored.", SchemaName, TableName);
            AddExtendedProperty(migrationBuilder, "PasswordChangedAt", "UTC timestamp of the most recent password change or reset. Useful for security auditing and policy enforcement.", SchemaName, TableName);
            AddExtendedProperty(migrationBuilder, "FailedLoginCount", "Custom field tracking consecutive failed login attempts since the last successful login; reset on success. Used for lockout policy. Note: ASP.NET Identity uses AccessFailedCount by default.", SchemaName, TableName);
            AddExtendedProperty(migrationBuilder, "LockoutUntil", "Custom field for UTC timestamp until which the account is locked out due to repeated failed login attempts. Note: ASP.NET Identity uses LockoutEnd (DATETIMEOFFSET) by default.", SchemaName, TableName);
            AddExtendedProperty(migrationBuilder, "MfaEnabled", "Boolean flag (0/1) indicating whether multi-factor authentication is currently active for the user. Typically mirrors TwoFactorEnabled.", SchemaName, TableName);
            AddExtendedProperty(migrationBuilder, "MfaSecret", "Encrypted Time-based One-Time Password (TOTP) seed or other MFA credential (e.g., WebAuthn ID) used to validate MFA challenges.", SchemaName, TableName);
            AddExtendedProperty(migrationBuilder, "SecurityStamp", "A random GUID or string that is regenerated when security-sensitive information (like password or MFA settings) changes, used to invalidate existing sessions/tokens.", SchemaName, TableName);
            AddExtendedProperty(migrationBuilder, "UserRole", "Coarse-grained role assignment for Role-Based Access Control (RBAC) (e.g., 'User', 'Admin'). May be extended via a separate roles table.", SchemaName, TableName);
            AddExtendedProperty(migrationBuilder, "UserPermissions", "JSON blob or delimited string representing fine-grained permissions granted to the user, complementing UserRole for detailed authorization.", SchemaName, TableName);
            AddExtendedProperty(migrationBuilder, "UserFollowing", "JSON blob or delimited string storing IDs of entities (e.g., other users, topics, documents) that this user is following, for social or notification features.", SchemaName, TableName);
            AddExtendedProperty(migrationBuilder, "Timezone", "IANA timezone identifier (e.g., 'America/New_York', 'Europe/London') to localize dates and times displayed to the user.", SchemaName, TableName);
            AddExtendedProperty(migrationBuilder, "Locale", "Locale/region code (e.g., 'en-US', 'fr-FR') for content localization, affecting language, date formats, and number formats.", SchemaName, TableName);
            AddExtendedProperty(migrationBuilder, "NotificationSettings", "JSON-encoded set of user preferences for various types of notifications (e.g., email, SMS, push notifications).", SchemaName, TableName);
            AddExtendedProperty(migrationBuilder, "UiTheme", "User's preferred UI theme (e.g., 'Dark', 'Light', 'SystemDefault', 'HighContrast') for personalization.", SchemaName, TableName);
            AddExtendedProperty(migrationBuilder, "TosVersionAccepted", "Version string or identifier of the Terms of Service (ToS) that the user has accepted (e.g., 'v3.2-20250115').", SchemaName, TableName);
            AddExtendedProperty(migrationBuilder, "TosAcceptedAt", "UTC timestamp when the user accepted the current or a specific version of the Terms of Service.", SchemaName, TableName);
            AddExtendedProperty(migrationBuilder, "TosMarketingOptIn", "Boolean flag (0/1) indicating whether the user has opted-in to receive marketing communications, relevant for GDPR and other privacy regulations.", SchemaName, TableName);
            AddExtendedProperty(migrationBuilder, "TosEmailNotification", "Boolean flag (0/1) indicating whether the user has agreed to receive email notifications (transactional, system alerts, etc.).", SchemaName, TableName);
            AddExtendedProperty(migrationBuilder, "CreatedAt", "Record creation timestamp (UTC), indicating when the user account was first created.", SchemaName, TableName);
            AddExtendedProperty(migrationBuilder, "CreatedByID", "Identifier (e.g., UserID of an admin or system process ID) of the entity that created this user record; NULL for self-registration.", SchemaName, TableName);
            AddExtendedProperty(migrationBuilder, "UpdatedAt", "UTC timestamp of the most recent update to the user's profile or settings.", SchemaName, TableName);
            AddExtendedProperty(migrationBuilder, "UpdatedBy", "Identifier (e.g., UserID or system process ID) of the entity that performed the most recent update.", SchemaName, TableName);
            AddExtendedProperty(migrationBuilder, "DeletedAt", "Soft-delete marker (UTC timestamp); non-NULL rows are considered deleted and typically excluded from normal queries. Essential for GDPR 'right to be forgotten' compliance.", SchemaName, TableName);
            AddExtendedProperty(migrationBuilder, "SuspendedAt", "UTC timestamp when the account was administratively suspended, distinct from soft deletion (e.g., for temporary deactivation).", SchemaName, TableName);
            AddExtendedProperty(migrationBuilder, "SuspensionReason", "Reason provided for administrative suspension (e.g., policy violation, security concern, fraud investigation).", SchemaName, TableName);
            AddExtendedProperty(migrationBuilder, "LastLoginAt", "UTC timestamp of the last successful login, used for activity tracking and identifying dormant accounts.", SchemaName, TableName);
            AddExtendedProperty(migrationBuilder, "LastActivityAt", "UTC timestamp of the user's most recent detected activity within the application (API call, UI interaction, etc.).", SchemaName, TableName);
            AddExtendedProperty(migrationBuilder, "LastIpAddress", "IP address (IPv4 or IPv6) recorded during the user's last login or significant activity; consider anonymization or truncation based on privacy policies.", SchemaName, TableName);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop All Extended Properties for columns
            var columnsWithProps = new[] {
                "Id", "UserName", "NormalizedUserName", "Email", "NormalizedEmail", "EmailConfirmed", "PhoneNumberConfirmed",
                "TwoFactorEnabled", "LockoutEnd", "LockoutEnabled", "AccessFailedCount", "ConcurrencyStamp",
                "CanonicalUsername", "PhoneNumber", "DisplayName", "PrimaryEmail", "PasswordHash", "PasswordChangedAt",
                "FailedLoginCount", "LockoutUntil", "MfaEnabled", "MfaSecret", "SecurityStamp", "UserRole",
                "UserPermissions", "UserFollowing", "Timezone", "Locale", "NotificationSettings", "UiTheme",
                "TosVersionAccepted", "TosAcceptedAt", "TosMarketingOptIn", "TosEmailNotification", "CreatedAt",
                "CreatedByID", "UpdatedAt", "UpdatedBy", "DeletedAt", "SuspendedAt", "SuspensionReason",
                "LastLoginAt", "LastActivityAt", "LastIpAddress"
            };

            foreach (var column in columnsWithProps)
            {
                DropExtendedProperty(migrationBuilder, column, SchemaName, TableName);
            }
            // CORRECTED CALL: Removed the trailing nulls to use default level types
            DropExtendedProperty(migrationBuilder, null, SchemaName, TableName);


            // Revert column types to original (mostly NVARCHAR(MAX) or previous specific types)
            migrationBuilder.AlterColumn<string>(
                name: "MfaSecret",
                table: TableName,
                schema: SchemaName,
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "UserPermissions",
                table: TableName,
                schema: SchemaName,
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(4000)",
                oldMaxLength: 4000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "NotificationSettings",
                table: TableName,
                schema: SchemaName,
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(4000)",
                oldMaxLength: 4000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "UserFollowing",
                table: TableName,
                schema: SchemaName,
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(4000)",
                oldMaxLength: 4000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "PasswordHash",
                table: TableName,
                schema: SchemaName,
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "SecurityStamp",
                table: TableName,
                schema: SchemaName,
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(2000)",
                oldMaxLength: 2000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ConcurrencyStamp",
                table: TableName,
                schema: SchemaName,
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "PhoneNumber",
                table: TableName,
                schema: SchemaName,
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20,
                oldNullable: true);
        }

        // Helper method to add or update an extended property
        private void AddExtendedProperty(MigrationBuilder migrationBuilder, string columnName, string value,
            string schema = "dbo", string table = null, string level0Type = "SCHEMA", string level1Type = "TABLE", string level2Type = "COLUMN")
        {
            if (string.IsNullOrEmpty(table)) throw new ArgumentNullException(nameof(table));
            var escapedValue = value.Replace("'", "''");
            string sql;

            if (columnName == null) // Table property
            {
                if (string.IsNullOrEmpty(level0Type)) level0Type = "SCHEMA"; // Ensure default if accidentally null/empty
                if (string.IsNullOrEmpty(level1Type)) level1Type = "TABLE";   // Ensure default

                sql = $@"
                    IF NOT EXISTS (SELECT 1 FROM sys.fn_listextendedproperty(N'MS_Description', N'{level0Type}', N'{schema}', N'{level1Type}', N'{table}', default, default))
                    BEGIN
                        EXEC sys.sp_addextendedproperty
                            @name = N'MS_Description',
                            @value = N'{escapedValue}',
                            @level0type = N'{level0Type}', @level0name = N'{schema}',
                            @level1type = N'{level1Type}', @level1name = N'{table}';
                    END
                    ELSE
                    BEGIN
                        EXEC sys.sp_updateextendedproperty
                            @name = N'MS_Description',
                            @value = N'{escapedValue}',
                            @level0type = N'{level0Type}', @level0name = N'{schema}',
                            @level1type = N'{level1Type}', @level1name = N'{table}';
                    END";
            }
            else // Column property
            {
                if (string.IsNullOrEmpty(level0Type)) level0Type = "SCHEMA";
                if (string.IsNullOrEmpty(level1Type)) level1Type = "TABLE";
                if (string.IsNullOrEmpty(level2Type)) level2Type = "COLUMN";

                sql = $@"
                    IF NOT EXISTS (SELECT 1 FROM sys.fn_listextendedproperty(N'MS_Description', N'{level0Type}', N'{schema}', N'{level1Type}', N'{table}', N'{level2Type}', N'{columnName}'))
                    BEGIN
                        EXEC sys.sp_addextendedproperty
                            @name = N'MS_Description',
                            @value = N'{escapedValue}',
                            @level0type = N'{level0Type}', @level0name = N'{schema}',
                            @level1type = N'{level1Type}', @level1name = N'{table}',
                            @level2type = N'{level2Type}', @level2name = N'{columnName}';
                    END
                    ELSE
                    BEGIN
                        EXEC sys.sp_updateextendedproperty
                            @name = N'MS_Description',
                            @value = N'{escapedValue}',
                            @level0type = N'{level0Type}', @level0name = N'{schema}',
                            @level1type = N'{level1Type}', @level1name = N'{table}',
                            @level2type = N'{level2Type}', @level2name = N'{columnName}';
                    END";
            }
            migrationBuilder.Sql(sql);
        }

        // Helper method to drop an extended property
        private void DropExtendedProperty(MigrationBuilder migrationBuilder, string columnName,
            string schema = "dbo", string table = null, string level0Type = "SCHEMA", string level1Type = "TABLE", string level2Type = "COLUMN")
        {
            if (string.IsNullOrEmpty(table)) throw new ArgumentNullException(nameof(table));
            string sql;
            if (columnName == null) // Table property
            {
                if (string.IsNullOrEmpty(level0Type)) level0Type = "SCHEMA";
                if (string.IsNullOrEmpty(level1Type)) level1Type = "TABLE";

                sql = $@"
                    IF EXISTS (SELECT 1 FROM sys.fn_listextendedproperty(N'MS_Description', N'{level0Type}', N'{schema}', N'{level1Type}', N'{table}', default, default))
                    BEGIN
                        EXEC sys.sp_dropextendedproperty
                            @name = N'MS_Description',
                            @level0type = N'{level0Type}', @level0name = N'{schema}',
                            @level1type = N'{level1Type}', @level1name = N'{table}';
                    END";
            }
            else // Column property
            {
                if (string.IsNullOrEmpty(level0Type)) level0Type = "SCHEMA";
                if (string.IsNullOrEmpty(level1Type)) level1Type = "TABLE";
                if (string.IsNullOrEmpty(level2Type)) level2Type = "COLUMN";

                sql = $@"
                    IF EXISTS (SELECT 1 FROM sys.fn_listextendedproperty(N'MS_Description', N'{level0Type}', N'{schema}', N'{level1Type}', N'{table}', N'{level2Type}', N'{columnName}'))
                    BEGIN
                        EXEC sys.sp_dropextendedproperty
                            @name = N'MS_Description',
                            @level0type = N'{level0Type}', @level0name = N'{schema}',
                            @level1type = N'{level1Type}', @level1name = N'{table}',
                            @level2type = N'{level2Type}', @level2name = N'{columnName}';
                    END";
            }
            migrationBuilder.Sql(sql);
        }
    }
}
