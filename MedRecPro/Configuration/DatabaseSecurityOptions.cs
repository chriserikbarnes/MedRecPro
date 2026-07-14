namespace MedRecPro.Configuration
{
    /**************************************************************/
    /// <summary>
    /// Holds database-security configuration that is bound once at the composition root.
    /// </summary>
    /// <remarks>
    /// Application code consumes narrow security services instead of reading the primary-key secret
    /// directly from configuration.
    /// </remarks>
    /// <seealso cref="MedRecPro.Service.IPrimaryKeyCipher"/>
    public sealed class DatabaseSecurityOptions
    {
        /**************************************************************/
        /// <summary>
        /// Gets the configuration section containing database-security settings.
        /// </summary>
        public const string SectionName = "Security:DB";

        /**************************************************************/
        /// <summary>
        /// Gets or sets the secret used to encrypt primary-key values.
        /// </summary>
        /// <remarks>
        /// This value is intentionally consumed only by the primary-key cipher implementation.
        /// </remarks>
        public string PKSecret { get; set; } = string.Empty;
    }
}
