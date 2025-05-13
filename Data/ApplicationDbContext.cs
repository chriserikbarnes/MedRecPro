using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using MedRecPro.Models;
using Microsoft.EntityFrameworkCore.Metadata;

namespace MedRecPro.Data
{
    /// <summary>
    /// Represents the application's database context.
    /// It now uses MedRecPro.Models.User with a long as the primary key,
    /// and IdentityRole<long> for roles with a long primary key.
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
        /// but usually, you'd interact via `UserManager<User>`.
        /// </summary>
        public DbSet<User> AppUsers { get; set; } // This will map to the same table as Identity's Users.

        /// <summary>
        /// Configures the model for the database context.
        /// </summary>
        /// <param name="builder">The model builder used to configure the database schema.</param>
        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder); // This is crucial for Identity tables to be configured.

            // Configure your MedRecPro.Models.User entity
            // Most Identity-related configurations (like PK name 'Id', table names 'AspNetUsers', etc.)
            // are handled by base.OnModelCreating(builder).
            // You only need to configure your *custom* properties here if they need special mapping.

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
    }
}
