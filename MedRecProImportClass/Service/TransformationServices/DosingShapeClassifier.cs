using MedRecProImportClass.Models;
using MedRecProImportClass.Service.TransformationServices.Dictionaries;

namespace MedRecProImportClass.Service.TransformationServices
{
    /**************************************************************/
    /// <summary>
    /// Classifies DOSING-category tables into parser shape profiles before
    /// cell-level extraction.
    /// </summary>
    /// <remarks>
    /// Shape classification keeps <see cref="DosingTableParser"/> from treating
    /// every row/column axis the same way. Dose-reduction rows, body-weight
    /// bands, lab-threshold modification triggers, dose ranges, and instruction
    /// prose all need different field routing even though they share the same
    /// section code and parser category.
    /// </remarks>
    /// <seealso cref="DosingTableParser"/>
    /// <seealso cref="DosingDescriptorDictionary"/>
    /// <seealso cref="PopulationDetector"/>
    internal static class DosingShapeClassifier
    {
        #region Public Members

        /**************************************************************/
        /// <summary>
        /// Dosing table shape profiles supported by <see cref="DosingTableParser"/>.
        /// </summary>
        public enum Shape
        {
            Standard,
            DoseReduction,
            BodyWeight,
            DoseRange,
            InstructionProse,
            LabThresholdDoseModification
        }

        /**************************************************************/
        /// <summary>
        /// Classifies a reconstructed dosing table by scanning row labels and
        /// data cells for stable shape signals.
        /// </summary>
        /// <param name="table">The reconstructed dosing table.</param>
        /// <returns>The best matching dosing shape profile.</returns>
        /// <seealso cref="PopulationDetector.LooksLikeLabThresholdDoseModification"/>
        /// <seealso cref="DosingDescriptorDictionary.IsDoseReductionLabel"/>
        public static Shape Classify(ReconstructedTable table)
        {
            #region implementation

            var doseReductionRows = 0;
            var bodyWeightRows = 0;
            var labThresholdRows = 0;
            var rangeCells = 0;
            var proseCells = 0;
            var totalCells = 0;

            foreach (var row in table.DataRows())
            {
                if (row.Classification == RowClassification.SocDivider)
                    continue;

                var rowLabel = row.CellAt(0)?.CleanedText?.Trim();

                if (DosingDescriptorDictionary.IsDoseReductionLabel(rowLabel))
                    doseReductionRows++;

                if (PopulationDetector.LooksLikeWeightBand(rowLabel))
                    bodyWeightRows++;

                if (PopulationDetector.LooksLikeLabThresholdDoseModification(rowLabel))
                    labThresholdRows++;

                if (row.Cells == null)
                    continue;

                foreach (var cell in row.Cells)
                {
                    if (cell.ResolvedColumnStart == 0)
                        continue;

                    var text = cell.CleanedText?.Trim();
                    if (string.IsNullOrWhiteSpace(text))
                        continue;

                    totalCells++;

                    if (DoseExtractor.LooksLikeDoseRange(text))
                        rangeCells++;

                    if (text.Length > 120 || text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length > 20)
                        proseCells++;
                }
            }

            if (labThresholdRows > 0)
                return Shape.LabThresholdDoseModification;

            if (doseReductionRows > 0)
                return Shape.DoseReduction;

            if (bodyWeightRows > 0)
                return Shape.BodyWeight;

            if (rangeCells > 0)
                return Shape.DoseRange;

            if (totalCells > 0 && (double)proseCells / totalCells >= 0.30)
                return Shape.InstructionProse;

            return Shape.Standard;

            #endregion
        }

        #endregion Public Members
    }
}
