namespace MedRecProImportClass.Service.TransformationServices.SampleSize
{
    /**************************************************************/
    /// <summary>
    /// Structured evidence that a text fragment contains sample-size information.
    /// </summary>
    /// <remarks>
    /// Parser callers receive evidence objects instead of raw regular-expression
    /// matches so syntax recognition remains centralized and assignment policy can
    /// stay in <see cref="ArmNResolver"/>.
    /// </remarks>
    /// <seealso cref="SampleSizeParser"/>
    /// <seealso cref="ArmNResolver"/>
    internal sealed record SampleSizeEvidence
    {
        /**************************************************************/
        /// <summary>Exact sample size value, populated only for exact evidence.</summary>
        public int? Value { get; init; }

        /**************************************************************/
        /// <summary>Whether the evidence is exact enough to assign to ArmN.</summary>
        public bool IsExact { get; init; }

        /**************************************************************/
        /// <summary>Where the evidence was found in the source table.</summary>
        public SampleSizeSourceKind SourceKind { get; init; }

        /**************************************************************/
        /// <summary>Original text that produced this evidence.</summary>
        public string RawText { get; init; } = string.Empty;

        /**************************************************************/
        /// <summary>Text remaining after removing the sample-size annotation.</summary>
        public string? CleanedText { get; init; }

        /**************************************************************/
        /// <summary>Optional source row index for scoped body/header evidence.</summary>
        public int? RowIndex { get; init; }

        /**************************************************************/
        /// <summary>Optional source column index for scoped arm evidence.</summary>
        public int? ColumnIndex { get; init; }

        /**************************************************************/
        /// <summary>Optional treatment-arm text associated with the evidence.</summary>
        public string? ArmCandidate { get; init; }

        /**************************************************************/
        /// <summary>Optional logical scope used by resolver callers.</summary>
        public string? ScopeKey { get; init; }

        /**************************************************************/
        /// <summary>Optional format or display hint that followed the N value.</summary>
        public string? FormatHint { get; init; }

        /**************************************************************/
        /// <summary>Machine-readable diagnostic code for rejected or audited evidence.</summary>
        public string? DiagnosticCode { get; init; }

        /**************************************************************/
        /// <summary>Human-readable diagnostic reason for rejected or audited evidence.</summary>
        public string? DiagnosticReason { get; init; }

        /**************************************************************/
        /// <summary>
        /// Creates exact sample-size evidence.
        /// </summary>
        /// <param name="value">Positive exact sample size.</param>
        /// <param name="sourceKind">Source location of the evidence.</param>
        /// <param name="rawText">Original source text.</param>
        /// <param name="cleanedText">Text after removing the N annotation.</param>
        /// <param name="rowIndex">Optional source row index.</param>
        /// <param name="columnIndex">Optional source column index.</param>
        /// <param name="armCandidate">Optional treatment-arm candidate.</param>
        /// <param name="scopeKey">Optional logical scope.</param>
        /// <param name="formatHint">Optional value-format hint.</param>
        /// <param name="diagnosticCode">Optional diagnostic code.</param>
        /// <param name="diagnosticReason">Optional diagnostic reason.</param>
        /// <returns>Exact evidence suitable for resolver policy.</returns>
        /// <seealso cref="SampleSizeParser"/>
        public static SampleSizeEvidence Exact(
            int value,
            SampleSizeSourceKind sourceKind,
            string rawText,
            string? cleanedText = null,
            int? rowIndex = null,
            int? columnIndex = null,
            string? armCandidate = null,
            string? scopeKey = null,
            string? formatHint = null,
            string? diagnosticCode = null,
            string? diagnosticReason = null)
        {
            #region implementation

            return new SampleSizeEvidence
            {
                Value = value,
                IsExact = true,
                SourceKind = sourceKind,
                RawText = rawText,
                CleanedText = cleanedText,
                RowIndex = rowIndex,
                ColumnIndex = columnIndex,
                ArmCandidate = armCandidate,
                ScopeKey = scopeKey,
                FormatHint = formatHint,
                DiagnosticCode = diagnosticCode,
                DiagnosticReason = diagnosticReason
            };

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates inexact sample-size evidence that must not populate ArmN.
        /// </summary>
        /// <param name="sourceKind">Source location of the evidence.</param>
        /// <param name="rawText">Original source text.</param>
        /// <param name="cleanedText">Text after removing any recognized annotation.</param>
        /// <param name="diagnosticCode">Optional diagnostic code.</param>
        /// <param name="diagnosticReason">Optional diagnostic reason.</param>
        /// <returns>Inexact evidence for auditing.</returns>
        /// <seealso cref="Rejected"/>
        public static SampleSizeEvidence Inexact(
            SampleSizeSourceKind sourceKind,
            string rawText,
            string? cleanedText = null,
            string? diagnosticCode = null,
            string? diagnosticReason = null)
        {
            #region implementation

            return new SampleSizeEvidence
            {
                IsExact = false,
                SourceKind = sourceKind,
                RawText = rawText,
                CleanedText = cleanedText,
                DiagnosticCode = diagnosticCode,
                DiagnosticReason = diagnosticReason
            };

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates rejected denominator-like evidence that should be surfaced in diagnostics.
        /// </summary>
        /// <param name="sourceKind">Source location of the rejected evidence.</param>
        /// <param name="rawText">Original source text.</param>
        /// <param name="diagnosticCode">Machine-readable rejection code.</param>
        /// <param name="diagnosticReason">Human-readable rejection reason.</param>
        /// <returns>Rejected evidence for audit-only routing.</returns>
        /// <seealso cref="Inexact"/>
        public static SampleSizeEvidence Rejected(
            SampleSizeSourceKind sourceKind,
            string rawText,
            string diagnosticCode,
            string diagnosticReason)
        {
            #region implementation

            return Inexact(sourceKind, rawText, diagnosticCode: diagnosticCode, diagnosticReason: diagnosticReason);

            #endregion
        }
    }
}
