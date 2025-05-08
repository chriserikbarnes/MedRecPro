using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace MedRecPro.Data
{
    /// <summary>
    /// Represents the application's database context, which includes Identity-related tables.
    /// </summary>
    public class ApplicationDbContext : IdentityDbContext<IdentityUser> // Use default IdentityUser
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
        /// Configures the model for the database context.
        /// </summary>
        /// <param name="builder">The model builder used to configure the database schema.</param>
        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);
            // Customize Identity model if needed
        }
    }
}
