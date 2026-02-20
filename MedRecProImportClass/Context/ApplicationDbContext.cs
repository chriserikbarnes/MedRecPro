using MedRecProImportClass.DataAccess;
using MedRecProImportClass.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.Data.SqlClient.DataClassification;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using System.Reflection;
using System.Reflection.Emit;
using LabelContainer = MedRecProImportClass.Models.Label;

namespace MedRecProImportClass.Data
{
    /// <summary>
    /// Represents the application's database context.
    /// It now uses MedRecProImportClass.Models.User with a long as the primary key,
    /// and IdentityRole[long] for roles with a long primary key.
    /// </summary>
    public class ApplicationDbContext : IdentityDbContext<User, IdentityRole<long>, long, IdentityUserClaim<long>, IdentityUserRole<long>, IdentityUserLogin<long>, IdentityRoleClaim<long>, IdentityUserToken<long>>
    {
        /**************************************************************/
        /// <summary>
        /// Initializes a new instance of the <see cref="ApplicationDbContext"/> class using the specified options.
        /// </summary>
        /// <param name="options">The options to configure the database context.</param>
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        /**************************************************************/
        /// <summary>
        /// Gets or sets the DbSet for MedRecPro Users in the application.
        /// ASP.NET Core Identity will use `base.Users` for IdentityUser operations.
        /// This `AppUsers` can be used for querying your custom `User` entity directly if needed,
        /// but usually, you'd interact via `UserManager[User]`.
        /// </summary>
        public DbSet<User> AppUsers { get; set; } // This will map to the same table as Identity's Users.

        public DbSet<ActivityLog> ActivityLogs { get; set; } // Maps to AspNetUserActivityLog table

        public DbSet<SplData> SplData { get; set; }

        #region Custom Database Functions

        /**************************************************************/
        /// <summary>
        /// Maps to SQL Server's SOUNDEX function for phonetic encoding.
        /// </summary>
        /// <param name="value">The string to compute Soundex for.</param>
        /// <returns>Four-character Soundex code.</returns>
        /// <remarks>
        /// This method is for EF Core query translation only.
        /// Do not call directly - use within LINQ queries.
        /// </remarks>
        public static string Soundex(string value)
            => throw new NotSupportedException("Use only in EF Core queries.");

        /**************************************************************/
        /// <summary>
        /// Maps to SQL Server's DIFFERENCE function for phonetic similarity comparison.
        /// Compares the Soundex values of two strings and returns a score indicating similarity.
        /// </summary>
        /// <param name="value1">First string to compare.</param>
        /// <param name="value2">Second string to compare.</param>
        /// <returns>
        /// Integer from 0-4 indicating phonetic similarity:
        /// 4 = Identical or nearly identical sounds
        /// 3 = Strong similarity (recommended threshold for drug names)
        /// 2 = Moderate similarity
        /// 1 = Weak similarity
        /// 0 = No similarity (different Soundex codes)
        /// </returns>
        /// <remarks>
        /// This method is for EF Core query translation only.
        /// Do not call directly - use within LINQ queries.
        /// Particularly useful for pharmaceutical names where misspellings are common.
        /// </remarks>
        /// <example>
        /// <code>
        /// // In a LINQ query:
        /// var matches = db.Products
        ///     .Where(p => ApplicationDbContext.Difference(p.Name, "testostone") >= 3)
        ///     .ToList();
        /// </code>
        /// </example>
        public static int Difference(string value1, string value2)
            => throw new NotSupportedException("Use only in EF Core queries.");

        #endregion

        /**************************************************************/
        /// <summary>
        /// Configures the model for the database context.
        /// </summary>
        /// <param name="builder">The model builder used to configure the database schema.</param>
        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder); // This is crucial for Identity tables to be configured.

            // Dynamically discover and register public nested
            // classes MedRecProImportClass.DataModels.Label as entities.
            var labelContainerType = typeof(MedRecProImportClass.Models.Label);

            // Get public nested types that are classes
            // and not abstract (suitable for entities)
            var nestedLabelEntityTypes = labelContainerType?.GetNestedTypes(BindingFlags.Public)
                ?.Where(t => t.IsClass && !t.IsAbstract);

            // Register each nested entity type with the model builder.
            if (nestedLabelEntityTypes != null)
            {
                foreach (var entityType in nestedLabelEntityTypes)
                {
                    var entityBuilder = builder.Entity(entityType);

                    // SQL table names are singular (e.g., "Document" table for "Document" class),
                    // explicitly set the table name to match the class name.
                    // In some cases the primary keys are resolved with a [Column("fieldId")] attribute,
                    entityBuilder.ToTable(entityType.Name);
                }
            }

            // Dynamically discover and register public nested
            // classes from MedRecProImportClass.Models.LabelView as view entities.
            var labelViewContainerType = typeof(MedRecProImportClass.Models.LabelView);

            var nestedViewEntityTypes = labelViewContainerType?.GetNestedTypes(BindingFlags.Public)
                ?.Where(t => t.IsClass && !t.IsAbstract);

            if (nestedViewEntityTypes != null)
            {
                foreach (var entityType in nestedViewEntityTypes)
                {
                    var entityBuilder = builder.Entity(entityType);

                    // Check for [Table] attribute to get view name
                    var tableAttr = entityType.GetCustomAttribute<System.ComponentModel.DataAnnotations.Schema.TableAttribute>();
                    var viewName = tableAttr?.Name ?? entityType.Name;

                    // Views are keyless - they're read-only query types
                    entityBuilder.HasNoKey();

                    // Register as a view (read-only, no migrations)
                    entityBuilder.ToView(viewName);
                }
            }

            // Dynamically discover and register public nested
            // classes from MedRecProImportClass.Models.OrangeBook as entities.
            var orangeBookContainerType = typeof(MedRecProImportClass.Models.OrangeBook);

            var nestedOrangeBookEntityTypes = orangeBookContainerType?.GetNestedTypes(BindingFlags.Public)
                ?.Where(t => t.IsClass && !t.IsAbstract);

            if (nestedOrangeBookEntityTypes != null)
            {
                foreach (var entityType in nestedOrangeBookEntityTypes)
                {
                    var entityBuilder = builder.Entity(entityType);

                    // OrangeBook table names include the "OrangeBook" prefix
                    // (e.g., "OrangeBookProduct"), so read [Table] attribute
                    // rather than using the simple class name.
                    var tableAttr = entityType.GetCustomAttribute<System.ComponentModel.DataAnnotations.Schema.TableAttribute>();
                    var tableName = tableAttr?.Name ?? entityType.Name;

                    entityBuilder.ToTable(tableName);
                }
            }

            // CONFIGURE SPECIFIC ENTITIES AFTER REFLECTION REGISTRATION
            configureDocumentRelationshipIdentifier(builder);
            configureDecimalPrecision(builder);

            builder.Entity<User>(entity =>
            {
                // The table name by default will be "AspNetUsers" if not changed.
                // If your existing table is "Users", you might need:
                // entity.ToTable("Users"); // Uncomment if your table is named "Users" and not "AspNetUsers"

                // Primary key 'Id' is already configured by IdentityDbContext.
                // entity.HasKey(e => e.Id); // This is inherited and configured.

                // Configure custom properties of MedRecProImportClass.Models.User
                entity.Property(e => e.CanonicalUsername).HasMaxLength(256);

                // PhoneNumber is inherited from IdentityUser, already configured.
                // entity.Property(e => e.PhoneNumber).HasMaxLength(20); 
                entity.Property(e => e.DisplayName).HasMaxLength(256);

                // PrimaryEmail is a custom property. IdentityUser.Email is the standard.
                // Ensure this doesn't conflict if both are mapped.
                entity.Property(e => e.PrimaryEmail).HasMaxLength(320).IsRequired();

                // PasswordHash is inherited.
                // entity.Property(e => e.PasswordHash).HasMaxLength(500); 

                entity.Property(e => e.UserRole).HasMaxLength(100).HasDefaultValue("User");
                entity.Property(e => e.Timezone).HasMaxLength(100).HasDefaultValue("UTC");
                entity.Property(e => e.Locale).HasMaxLength(20).HasDefaultValue("en-US");
                entity.Property(e => e.UiTheme).HasMaxLength(50);
                entity.Property(e => e.TosVersionAccepted).HasMaxLength(20);
                entity.Property(e => e.SuspensionReason).HasMaxLength(500);
                entity.Property(e => e.LastIpAddress).HasMaxLength(45);

                // Default values for custom properties
                entity.Property(e => e.FailedLoginCount).HasDefaultValue(0); // Custom property
                // MfaEnabled is custom. IdentityUser.TwoFactorEnabled is standard.
                entity.Property(e => e.MfaEnabled).HasDefaultValue(false);

                entity.Property(e => e.TosMarketingOptIn).HasDefaultValue(false);
                entity.Property(e => e.TosEmailNotification).HasDefaultValue(false);
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");

                // SecurityStamp is inherited (string).
                // entity.Property(e => e.SecurityStamp).HasDefaultValueSql("NEWID()"); 

                // Configure indexes for custom properties if needed
                // Identity configures indexes for its own properties (UserName, Email).
                if (!string.IsNullOrEmpty(entity.Property(e => e.CanonicalUsername).Metadata.GetColumnName(StoreObjectIdentifier.Table("AspNetUsers", null)))) // Check if column exists before creating index
                {
                    entity.HasIndex(e => e.CanonicalUsername)
                        .HasFilter("[DeletedAt] IS NULL AND [CanonicalUsername] IS NOT NULL") // Ensure column is not null for unique index
                        .IsUnique()
                        .HasDatabaseName("UX_Users_CanonicalUsername_Active");
                }

                // PrimaryEmail is custom. Identity's Email already has a unique index.
                // If PrimaryEmail is different and also needs to be unique:
                if (!string.IsNullOrEmpty(entity.Property(e => e.PrimaryEmail).Metadata.GetColumnName(StoreObjectIdentifier.Table("AspNetUsers", null))))
                {
                    entity.HasIndex(e => e.PrimaryEmail)
                        .HasFilter("[DeletedAt] IS NULL")
                        .IsUnique()
                        .HasDatabaseName("UX_Users_PrimaryEmail_Active");
                }

                entity.HasIndex(e => e.LastActivityAt)
                    .HasDatabaseName("IX_Users_LastActivityAt");

                // Ignore non-mapped properties from the base NewUser DTO if they exist on User model
                // and are not actual database columns.
                // The 'Password' property for plaintext password should not be in the User entity mapped to DB.
                // It's already ignored in UserDataAccess when creating.
                // entity.Ignore(e => e.Password); // If it were part of User model directly.

                // EncryptedUserId is explicitly NotMapped.
                // UserIdInternal is NotMapped.
            });

            builder.Entity<IdentityRole<long>>(entity =>
            {
                entity.ToTable("AspNetRoles"); // Default table name
            });

            // Register SOUNDEX function
            builder.HasDbFunction(typeof(ApplicationDbContext).GetMethod(nameof(Soundex))!)
                .HasName("SOUNDEX")
                .IsBuiltIn();

            // Register DIFFERENCE function
            builder.HasDbFunction(typeof(ApplicationDbContext).GetMethod(nameof(Difference))!)
                .HasName("DIFFERENCE")
                .IsBuiltIn();
        }

        /**************************************************************/
        /// <summary>
        /// Configures the DocumentRelationshipIdentifier entity with navigation properties
        /// but WITHOUT database foreign key constraints for performance.
        /// </summary>
        /// <param name="builder">The model builder to configure.</param>
        /// <remarks>
        /// This configuration creates logical relationships for EF Core navigation properties
        /// while preventing the creation of physical FK constraints in the database.
        /// Referential integrity must be maintained in application code.
        /// </remarks>
        /// <seealso cref="LabelContainer.DocumentRelationshipIdentifier"/>
        /// <seealso cref="LabelContainer.DocumentRelationship"/>
        /// <seealso cref="LabelContainer.OrganizationIdentifier"/>
        private void configureDocumentRelationshipIdentifier(ModelBuilder builder)
        {
            builder.Entity<LabelContainer.DocumentRelationshipIdentifier>(entity =>
            {
                // Primary key is already configured via [Key] attribute
                entity.HasKey(e => e.DocumentRelationshipIdentifierID);

                // ?? CONFIGURE NAVIGATION WITHOUT FK CONSTRAINTS

                // Relationship to DocumentRelationship - NO database constraint
                entity.HasOne(d => d.DocumentRelationship)
                    .WithMany() // Add collection property to DocumentRelationship if needed
                    .HasForeignKey(d => d.DocumentRelationshipID)
                    .OnDelete(DeleteBehavior.NoAction)
                    .IsRequired(false);

                // Relationship to OrganizationIdentifier - NO database constraint
                entity.HasOne(d => d.OrganizationIdentifier)
                    .WithMany() // Add collection property to OrganizationIdentifier if needed
                    .HasForeignKey(d => d.OrganizationIdentifierID)
                    .OnDelete(DeleteBehavior.NoAction)
                    .IsRequired(false);

                // INDEXES FOR PERFORMANCE 
                entity.HasIndex(e => e.DocumentRelationshipID)
                    .HasDatabaseName("IX_DocumentRelationshipIdentifier_DocumentRelationshipID");

                entity.HasIndex(e => e.OrganizationIdentifierID)
                    .HasDatabaseName("IX_DocumentRelationshipIdentifier_OrganizationIdentifierID");

                // COMPOSITE UNIQUE INDEX to prevent duplicate links
                entity.HasIndex(e => new { e.DocumentRelationshipID, e.OrganizationIdentifierID })
                    .IsUnique()
                    .HasFilter("[DocumentRelationshipID] IS NOT NULL AND [OrganizationIdentifierID] IS NOT NULL")
                    .HasDatabaseName("UX_DocumentRelationshipIdentifier_Unique");
            });
        }

        /**************************************************************/
        /// <summary>
        /// Configures decimal precision for all entities with quantity, measurement, and value fields.
        /// </summary>
        /// <param name="builder">The model builder to configure.</param>
        /// <remarks>
        /// The database schema uses DECIMAL(18, 9) for quantity and measurement fields across multiple entities.
        /// Without explicit configuration, EF Core defaults to DECIMAL(18, 2), causing precision loss
        /// during save operations. This method ensures EF Core uses the correct precision for all
        /// entities that were registered via reflection.
        /// 
        /// Entities configured:
        /// - Ingredient: QuantityNumerator, QuantityDenominator
        /// - PackagingLevel: QuantityNumerator, QuantityDenominator
        /// - ProductPart: PartQuantityNumerator
        /// - Characteristic: ValuePQ_Value, ValueIVLPQ_LowValue, ValueIVLPQ_HighValue
        /// - Moiety: QuantityNumeratorLowValue, QuantityDenominatorValue
        /// - DosingSpecification: DoseQuantityValue
        /// - Requirement: PauseQuantityValue, PeriodValue
        /// - ObservationCriterion: ToleranceHighValue
        /// </remarks>
        /// <seealso cref="LabelContainer.Ingredient"/>
        /// <seealso cref="LabelContainer.PackagingLevel"/>
        /// <seealso cref="LabelContainer.ProductPart"/>
        /// <seealso cref="LabelContainer.Characteristic"/>
        /// <seealso cref="LabelContainer.Moiety"/>
        /// <seealso cref="LabelContainer.DosingSpecification"/>
        /// <seealso cref="LabelContainer.Requirement"/>
        /// <seealso cref="LabelContainer.ObservationCriterion"/>
        private void configureDecimalPrecision(ModelBuilder builder)
        {
            #region implementation

            // Standard precision for pharmaceutical quantities and measurements
            const string decimalPrecision = "decimal(18, 9)";

            #region Ingredient
            // Configure ingredient quantity fields
            builder.Entity<LabelContainer.Ingredient>(entity =>
            {
                entity.Property(e => e.QuantityNumerator)
                    .HasColumnType(decimalPrecision);

                entity.Property(e => e.QuantityDenominator)
                    .HasColumnType(decimalPrecision);
            });
            #endregion

            #region PackagingLevel
            // Configure packaging quantity fields
            builder.Entity<LabelContainer.PackagingLevel>(entity =>
            {
                entity.Property(e => e.QuantityNumerator)
                    .HasColumnType(decimalPrecision);

                entity.Property(e => e.QuantityDenominator)
                    .HasColumnType(decimalPrecision);
            });
            #endregion

            #region ProductPart
            // Configure product part quantity fields (for kit components)
            builder.Entity<LabelContainer.ProductPart>(entity =>
            {
                entity.Property(e => e.PartQuantityNumerator)
                    .HasColumnType(decimalPrecision);
            });
            #endregion

            #region Characteristic
            // Configure characteristic value fields (product/package/moiety characteristics)
            builder.Entity<LabelContainer.Characteristic>(entity =>
            {
                entity.Property(e => e.ValuePQ_Value)
                    .HasColumnType(decimalPrecision);

                entity.Property(e => e.ValueIVLPQ_LowValue)
                    .HasColumnType(decimalPrecision);

                entity.Property(e => e.ValueIVLPQ_HighValue)
                    .HasColumnType(decimalPrecision);
            });
            #endregion

            #region Moiety
            // Configure moiety quantity fields (substance indexing)
            builder.Entity<LabelContainer.Moiety>(entity =>
            {
                entity.Property(e => e.QuantityNumeratorLowValue)
                    .HasColumnType(decimalPrecision);

                entity.Property(e => e.QuantityDenominatorValue)
                    .HasColumnType(decimalPrecision);
            });
            #endregion

            #region DosingSpecification
            // Configure dosing specification quantity fields
            builder.Entity<LabelContainer.DosingSpecification>(entity =>
            {
                entity.Property(e => e.DoseQuantityValue)
                    .HasColumnType(decimalPrecision);
            });
            #endregion

            #region Requirement
            // Configure requirement timing and period fields (REMS)
            builder.Entity<LabelContainer.Requirement>(entity =>
            {
                entity.Property(e => e.PauseQuantityValue)
                    .HasColumnType(decimalPrecision);

                entity.Property(e => e.PeriodValue)
                    .HasColumnType(decimalPrecision);
            });
            #endregion

            #region ObservationCriterion
            // Configure observation criterion tolerance fields
            builder.Entity<LabelContainer.ObservationCriterion>(entity =>
            {
                entity.Property(e => e.ToleranceHighValue)
                    .HasColumnType(decimalPrecision);
            });
            #endregion

            #endregion
        }
    }
}
