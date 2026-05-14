namespace MedRecProImportClass.Service.TransformationServices.AdverseEventTableFlattening
{
    /**************************************************************/
    /// <summary>
    /// Classifies treatment-arm text and dose values as placebo-equivalent or active.
    /// </summary>
    /// <remarks>
    /// This small contract lets upstream correction guardrails and downstream adverse-event
    /// denormalization share the same placebo/sham/vehicle and zero-dose semantics without
    /// duplicating regular expressions.
    /// </remarks>
    /// <seealso cref="RelativeRiskCalculator"/>
    public interface IPlaceboArmClassifier
    {
        /**************************************************************/
        /// <summary>
        /// Returns true when an arm should be treated as placebo-equivalent.
        /// </summary>
        /// <param name="treatmentArm">Treatment-arm text.</param>
        /// <param name="dose">Parsed dose value.</param>
        /// <returns><c>true</c> for placebo, sham, vehicle, or zero-dose arms.</returns>
        bool IsPlaceboArm(string? treatmentArm, decimal? dose);
    }

    /**************************************************************/
    /// <summary>
    /// Default <see cref="IPlaceboArmClassifier"/> implementation backed by
    /// <see cref="RelativeRiskCalculator"/>.
    /// </summary>
    /// <remarks>
    /// Keeping the classifier as a thin wrapper preserves the Stage 5 static utility as the
    /// source of truth while allowing services such as Claude correction to depend on a
    /// replaceable abstraction.
    /// </remarks>
    /// <seealso cref="IPlaceboArmClassifier"/>
    /// <seealso cref="RelativeRiskCalculator"/>
    public sealed class PlaceboArmClassifier : IPlaceboArmClassifier
    {
        /**************************************************************/
        /// <inheritdoc/>
        public bool IsPlaceboArm(string? treatmentArm, decimal? dose)
        {
            #region implementation

            return RelativeRiskCalculator.IsPlaceboArm(treatmentArm, dose);

            #endregion
        }
    }
}
