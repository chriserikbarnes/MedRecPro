using MedRecPro.DataAccess;
using MedRecPro.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.Data.SqlClient.DataClassification;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using System.Reflection;
using System.Reflection.Emit;
using LabelContainer = MedRecPro.Models.Label;

namespace MedRecPro.Data
{
    /// <summary>
    /// Represents the application's database context.
    /// It now uses MedRecPro.Models.User with a long as the primary key,
    /// and IdentityRole[long] for roles with a long primary key.
    /// </summary>
    public class ApplicationDbContext : IdentityDbContext<User, IdentityRole<long>, long, IdentityUserClaim<long>, IdentityUserRole<long>, IdentityUserLogin<long>, IdentityRoleClaim<long>, IdentityUserToken<long>>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ApplicationDbContext"/> class using the specified options.
        /// </summary>
        /// <param name="options">The options to configure the database context.</param>
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        /// <summary>
        /// Gets or sets the DbSet for MedRecPro Users in the application.
        /// ASP.NET Core Identity will use `base.Users` for IdentityUser operations.
        /// This `AppUsers` can be used for querying your custom `User` entity directly if needed,
        /// but usually, you'd interact via `UserManager[User]`.
        /// </summary>
        public DbSet<User> AppUsers { get; set; } // This will map to the same table as Identity's Users.

        public DbSet<ActivityLog> ActivityLogs { get; set; } // Maps to AspNetUserActivityLog table

        public DbSet<SplData> SplData { get; set; }

        /// <summary>
        /// Configures the model for the database context.
        /// </summary>
        /// <param name="builder">The model builder used to configure the database schema.</param>
        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder); // This is crucial for Identity tables to be configured.

            // Dynamically discover and register public nested
            // classes MedRecPro.DataModels.Label as entities.
            var labelContainerType = typeof(MedRecPro.Models.Label);

            // Get public nested types that are classes
            // and not abstract (suitable for entities)
            var nestedLabelEntityTypes = labelContainerType?.GetNestedTypes(BindingFlags.Public)
                ?.Where(t => t.IsClass && !t.IsAbstract);

            // Register each nested entity type with the model builder.
            if (nestedLabelEntityTypes != null)
                foreach (var entityType in nestedLabelEntityTypes)
                {
                    var entityBuilder = builder.Entity(entityType);

                    // SQL table names are singular (e.g., "Document" table for "Document" class),
                    // explicitly set the table name to match the class name.
                    // In some cases the primary keys are resolved with a [Column("fieldId")] attribute,
                    entityBuilder.ToTable(entityType.Name);
                }

            // CONFIGURE SPECIFIC ENTITIES AFTER REFLECTION REGISTRATION
            configureDocumentRelationshipIdentifier(builder);

            builder.Entity<User>(entity =>
            {
                // The table name by default will be "AspNetUsers" if not changed.
                // If your existing table is "Users", you might need:
                // entity.ToTable("Users"); // Uncomment if your table is named "Users" and not "AspNetUsers"

                // Primary key 'Id' is already configured by IdentityDbContext.
                // entity.HasKey(e => e.Id); // This is inherited and configured.

                // Configure custom properties of MedRecPro.Models.User
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

                // ⚠️ CONFIGURE NAVIGATION WITHOUT FK CONSTRAINTS

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
    }
}
