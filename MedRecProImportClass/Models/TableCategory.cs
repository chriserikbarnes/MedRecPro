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
    /// - **DOSING**: Dosage and administration tables
    /// - **BMD**: Bone mineral density / timepoint tables
    /// - **TISSUE_DISTRIBUTION**: Tissue-to-plasma ratio tables
    /// - **DRUG_INTERACTION**: Drug interaction tables (stub — future implementation)
    /// - **OTHER**: Unclassified but parseable tables
    /// - **SKIP**: Tables to exclude (patient info, NDC, formulas, How Supplied)
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
        /// Dosage and administration tables. Rows are parameters/populations,
        /// columns are dose levels or units.
        /// </summary>
        DOSING,

        /**************************************************************/
        /// <summary>
        /// Bone mineral density tables. Columns are timepoints (Week/Month/Year),
        /// rows are anatomical sites. Values are typically mean percent change.
        /// </summary>
        BMD,

        /**************************************************************/
        /// <summary>
        /// Tissue-to-plasma ratio tables. Simple two-column structure:
        /// tissue name and ratio value.
        /// </summary>
        TISSUE_DISTRIBUTION,

        /**************************************************************/
        /// <summary>
        /// Drug interaction tables. Stub for future implementation.
        /// These have unique structure with direction arrows and magnitude percentages.
        /// </summary>
        DRUG_INTERACTION,

        /**************************************************************/
        /// <summary>
        /// Text-only narrative tables — drug interaction prose, safety narratives,
        /// hormone physiology descriptions, and similar. Values are whole-cell text
        /// and are not numerically comparable. Produced by the router downgrading
        /// section-code-hinted PK tables that fail content validation (no PK
        /// parameter names in headers or row labels and prose-heavy cells).
        /// </summary>
        TEXT_DESCRIPTIVE,

        /**************************************************************/
        /// <summary>
        /// Unclassified but parseable tables. Used when no specific parser matches
        /// but the table contains structured data worth preserving.
        /// </summary>
        OTHER,

        /**************************************************************/
        /// <summary>
        /// Tables to exclude from normalization. Includes patient information leaflets,
        /// NDC/packaging tables, formulas, How Supplied, and single-column text tables.
        /// </summary>
        SKIP
    }
}
