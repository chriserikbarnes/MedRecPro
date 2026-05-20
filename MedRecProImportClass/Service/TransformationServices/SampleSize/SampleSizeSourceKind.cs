namespace MedRecProImportClass.Service.TransformationServices.SampleSize
{
    /**************************************************************/
    /// <summary>
    /// Identifies where sample-size evidence was found in a reconstructed table.
    /// </summary>
    /// <remarks>
    /// The source kind describes evidence origin only. Resolver outcomes such as
    /// conflict rejection are represented as diagnostic codes on
    /// <see cref="SampleSizeEvidence"/>.
    /// </remarks>
    /// <seealso cref="SampleSizeEvidence"/>
    /// <seealso cref="SampleSizeParser"/>
    internal enum SampleSizeSourceKind
    {
        /**************************************************************/
        /// <summary>Sample size was parsed from a treatment-arm header.</summary>
        ArmHeader,

        /**************************************************************/
        /// <summary>Sample size was parsed from a standardized column header.</summary>
        ColumnHeader,

        /**************************************************************/
        /// <summary>Sample size was parsed from a multi-row header tier.</summary>
        HeaderTier,

        /**************************************************************/
        /// <summary>Sample size was parsed from a leading or mid-body metadata row.</summary>
        BodyMetadataRow,

        /**************************************************************/
        /// <summary>Sample size was parsed from a count-over-denominator cell.</summary>
        FractionDenominator,

        /**************************************************************/
        /// <summary>Sample size was parsed from an inline value suffix.</summary>
        InlineValueSuffix,

        /**************************************************************/
        /// <summary>Sample size was inferred from count-percent consistency.</summary>
        CountPercentInference,

        /**************************************************************/
        /// <summary>Sample size was parsed from caption or footnote text.</summary>
        CaptionOrFootnote,

        /**************************************************************/
        /// <summary>Only a range-like sample-size expression was available.</summary>
        RangeOnly,

        /**************************************************************/
        /// <summary>Sample size was filled by the Stage 5 same-arm group backfill.</summary>
        Stage5GroupBackfill
    }
}
