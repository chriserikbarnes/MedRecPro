using MedRecPro.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedRecPro.Features.AeDashboard.Persistence
{
    /**************************************************************/
    /// <summary>
    /// Applies feature-owned EF Core mappings for AE dashboard read models.
    /// </summary>
    /// <remarks>
    /// These mappings were extracted from <see cref="MedRecPro.Data.ApplicationDbContext"/>
    /// so the dashboard's table-backed read models do not keep adding special cases
    /// to the general label-view registration loop.
    /// </remarks>
    /// <seealso cref="LabelView.FlattenedAdverseEventTable"/>
    /// <seealso cref="LabelView.AeDashboardProductCatalog"/>
    internal static class AeDashboardModelConfigurations
    {
        #region implementation

        /**************************************************************/
        /// <summary>
        /// Gets the label-view nested types configured by this feature.
        /// </summary>
        /// <remarks>
        /// The application context uses this set to skip the reflection-based
        /// keyless-view registration path for these explicitly configured models.
        /// </remarks>
        /// <seealso cref="Apply(ModelBuilder)"/>
        internal static IReadOnlySet<Type> ConfiguredTypes { get; } = new HashSet<Type>
        {
            typeof(LabelView.FlattenedAdverseEventTable),
            typeof(LabelView.FlattenedAdverseEventCoverageTable),
            typeof(LabelView.FlattenedAdverseEventRiskTable),
            typeof(LabelView.AeDashboardProductCatalog),
            typeof(LabelView.AeDrugSummary)
        };

        /**************************************************************/
        /// <summary>
        /// Applies all AE dashboard model configurations to the supplied model builder.
        /// </summary>
        /// <param name="builder">The EF Core model builder being configured.</param>
        /// <seealso cref="FlattenedAdverseEventTableConfiguration"/>
        /// <seealso cref="AeDashboardProductCatalogConfiguration"/>
        internal static void Apply(ModelBuilder builder)
        {
            #region implementation

            builder.ApplyConfiguration(new FlattenedAdverseEventTableConfiguration());
            builder.ApplyConfiguration(new FlattenedAdverseEventCoverageTableConfiguration());
            builder.ApplyConfiguration(new FlattenedAdverseEventRiskTableConfiguration());
            builder.ApplyConfiguration(new AeDashboardProductCatalogConfiguration());
            builder.ApplyConfiguration(new AeDrugSummaryConfiguration());

            #endregion
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Configures the keyed Stage 5 adverse-event statistics table.
    /// </summary>
    /// <seealso cref="LabelView.FlattenedAdverseEventTable"/>
    internal sealed class FlattenedAdverseEventTableConfiguration : IEntityTypeConfiguration<LabelView.FlattenedAdverseEventTable>
    {
        #region implementation

        /**************************************************************/
        /// <summary>
        /// Applies table, key, identity, and decimal precision mapping.
        /// </summary>
        /// <param name="builder">Entity type builder for the flattened AE statistics table.</param>
        /// <seealso cref="LabelView.FlattenedAdverseEventTable"/>
        public void Configure(EntityTypeBuilder<LabelView.FlattenedAdverseEventTable> builder)
        {
            #region implementation

            builder.ToTable("tmp_FlattenedAdverseEventTable");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id)
                .HasColumnName("tmp_FlattenedAdverseEventTableID")
                .ValueGeneratedOnAdd();

            // Match DDL DECIMAL(18,6).
            builder.Property(x => x.Dose)
                .HasColumnType("decimal(18, 6)");

            #endregion
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Configures the keyed Stage 5 adverse-event coverage audit table.
    /// </summary>
    /// <seealso cref="LabelView.FlattenedAdverseEventCoverageTable"/>
    internal sealed class FlattenedAdverseEventCoverageTableConfiguration : IEntityTypeConfiguration<LabelView.FlattenedAdverseEventCoverageTable>
    {
        #region implementation

        /**************************************************************/
        /// <summary>
        /// Applies table, key, identity, and decimal precision mapping.
        /// </summary>
        /// <param name="builder">Entity type builder for the flattened AE coverage table.</param>
        /// <seealso cref="LabelView.FlattenedAdverseEventCoverageTable"/>
        public void Configure(EntityTypeBuilder<LabelView.FlattenedAdverseEventCoverageTable> builder)
        {
            #region implementation

            builder.ToTable("tmp_FlattenedAdverseEventCoverageTable");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id)
                .HasColumnName("tmp_FlattenedAdverseEventCoverageTableID")
                .ValueGeneratedOnAdd();

            // Match DDL DECIMAL(18,6).
            builder.Property(x => x.Dose)
                .HasColumnType("decimal(18, 6)");
            builder.Property(x => x.ComparatorDose)
                .HasColumnType("decimal(18, 6)");

            #endregion
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Configures the keyed materialized adverse-event risk table.
    /// </summary>
    /// <seealso cref="LabelView.FlattenedAdverseEventRiskTable"/>
    internal sealed class FlattenedAdverseEventRiskTableConfiguration : IEntityTypeConfiguration<LabelView.FlattenedAdverseEventRiskTable>
    {
        #region implementation

        /**************************************************************/
        /// <summary>
        /// Applies table, key, identity, and decimal precision mapping.
        /// </summary>
        /// <param name="builder">Entity type builder for the flattened AE risk table.</param>
        /// <seealso cref="LabelView.FlattenedAdverseEventRiskTable"/>
        public void Configure(EntityTypeBuilder<LabelView.FlattenedAdverseEventRiskTable> builder)
        {
            #region implementation

            builder.ToTable("tmp_FlattenedAdverseEventRiskTable");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id)
                .HasColumnName("tmp_FlattenedAdverseEventRiskTableID")
                .ValueGeneratedOnAdd();

            // Match DDL DECIMAL(18,6).
            builder.Property(x => x.Dose)
                .HasColumnType("decimal(18, 6)");

            #endregion
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Configures the keyed materialized AE dashboard product catalog table.
    /// </summary>
    /// <seealso cref="LabelView.AeDashboardProductCatalog"/>
    internal sealed class AeDashboardProductCatalogConfiguration : IEntityTypeConfiguration<LabelView.AeDashboardProductCatalog>
    {
        #region implementation

        /**************************************************************/
        /// <summary>
        /// Applies table, key, and identity mapping.
        /// </summary>
        /// <param name="builder">Entity type builder for the AE dashboard product catalog table.</param>
        /// <seealso cref="LabelView.AeDashboardProductCatalog"/>
        public void Configure(EntityTypeBuilder<LabelView.AeDashboardProductCatalog> builder)
        {
            #region implementation

            builder.ToTable("tmp_AeDashboardProductCatalog");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id)
                .HasColumnName("AeDashboardProductCatalogID")
                .ValueGeneratedOnAdd();

            #endregion
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Configures the keyless AE dashboard summary view.
    /// </summary>
    /// <seealso cref="LabelView.AeDrugSummary"/>
    internal sealed class AeDrugSummaryConfiguration : IEntityTypeConfiguration<LabelView.AeDrugSummary>
    {
        #region implementation

        /**************************************************************/
        /// <summary>
        /// Applies keyless view mapping for the product summary projection.
        /// </summary>
        /// <param name="builder">Entity type builder for the AE drug summary view.</param>
        /// <seealso cref="LabelView.AeDrugSummary"/>
        public void Configure(EntityTypeBuilder<LabelView.AeDrugSummary> builder)
        {
            #region implementation

            builder.HasNoKey();
            builder.ToView("vw_AeDrugSummary");

            #endregion
        }

        #endregion
    }
}
