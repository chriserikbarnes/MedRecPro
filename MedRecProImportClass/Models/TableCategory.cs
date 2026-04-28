namespace MedRecProImportClass.Models
{
    /**************************************************************/
    /// <summary>
    /// Classifies SPL tables into functional categories for Stage 3 parser routing
    /// in the SPL Table Normalization pipeline. Each category maps to one or more
    /// type-specific parsers that know how to decompose the table into normalized observations.
    /// </summary>
    /// <remarks>
    /// ## Routing
    /// The <see cref="TableCategory"/> is determined by <c>ParentSectionCode</c> (LOINC) on the
    /// <see cref="ReconstructedTable"/>. The <c>TableParserRouter</c> maps section codes to
    /// categories, then selects the most specific parser within that category based on
    /// structural flags (header row count, SOC dividers, column count).
    ///
    /// ## Category Descriptions
    /// - **PK**: Pharmacokinetic parameter tables (Cmax, AUC, t½, Tmax, Cl, Vd)
    /// - **ADVERSE_EVENT**: Adverse reaction incidence tables with treatment arm columns
    /// - **EFFICACY**: Clinical study efficacy/outcomes tables with stat columns
    /// - **DRUG_INTERACTION**: Drug interaction tables (structural subset of PK)
    /// - **SKIP**: Tables to exclude (patient info, NDC, formulas, How Supplied,
    ///   and any table that doesn't classify into one of the four kept categories)
    /// </remarks>
    /// <seealso cref="ReconstructedTable"/>
    public enum TableCategory
    {
        /**************************************************************/
        /// <summary>
        /// Pharmacokinetic parameter tables. Columns are PK parameters (Cmax, AUC, t½),
        /// rows are dose regimens. Unpivoted by column.
        /// </summary>
        PK,

        /**************************************************************/
        /// <summary>
        /// Adverse reaction incidence tables. Columns are treatment arms with N values,
        /// rows are adverse event names. May include SOC divider rows.
        /// </summary>
        ADVERSE_EVENT,

        /**************************************************************/
        /// <summary>
        /// Clinical study efficacy/outcomes tables. Similar to AE tables but may include
        /// statistical columns (ARR, RR, P-value, 95% CI).
        /// </summary>
        EFFICACY,

        /**************************************************************/
        /// <summary>
        /// Drug interaction tables. Structural subset of PK with co-administered drug
        /// names in row labels and geometric mean ratios with 90% CI bounds.
        /// </summary>
        DRUG_INTERACTION,

        /**************************************************************/
        /// <summary>
        /// Tables to exclude from normalization. Includes patient information leaflets,
        /// NDC/packaging tables, formulas, How Supplied, single-column text tables, and
        /// any table that does not classify into one of the four parsed categories.
        /// </summary>
        SKIP
    }
}
