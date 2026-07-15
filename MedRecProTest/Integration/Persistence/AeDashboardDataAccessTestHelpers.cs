using MedRecPro.Data;
using MedRecPro.DataAccess;
using MedRecPro.Models;
using MedRecPro.Service;
using MedRecPro.Service.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MedRecProTest.Integration.Persistence
{
    /**************************************************************/
    /// <summary>
    /// Contains shared builders and persistence helpers for AE dashboard data-access tests.
    /// </summary>
    /// <remarks>
    /// This partial preserves the original AE dashboard test identities and setup.
    /// </remarks>
    /// <seealso cref="AeDashboardDataAccessTests"/>
    public partial class AeDashboardDataAccessTests
    {
        #region helpers

        /**************************************************************/
        /// <summary>
        /// Builds a deterministic, distinct document GUID for a synthetic correlation drug.
        /// </summary>
        private static Guid drugDoc(int n) => Guid.Parse($"a1a1a1a1-0000-0000-0000-{n:D12}");

        /**************************************************************/
        /// <summary>
        /// Seeds one risk-table row for a synthetic correlation drug, keeping the CI tight and non-fragile.
        /// </summary>
        private static void seedCorrelationRow(
            SqliteConnection connection,
            Guid documentGuid,
            int riskId,
            int activeMoietyId,
            string pharmClassCode,
            string parameterCategory,
            double rr,
            int pharmacologicClassId = 500,
            string? parameterName = null,
            bool isPlaceboControlled = true,
            string significance = "elevated",
            string? calculationFlags = null,
            bool isCombo = false,
            string pharmClassName = "Test Pharmacologic Class",
            double? eventsTreatment = 30,
            double? eventsComparator = 10)
        {
            #region implementation

            DtoLabelAccessTestHelper.SeedAeRiskSignalTable(
                connection,
                documentGuid: documentGuid,
                riskId: riskId,
                adverseEventId: riskId,
                standardizedId: riskId,
                activeMoietyId: activeMoietyId,
                ingredientSubstanceId: activeMoietyId + 1000,
                pharmacologicClassId: pharmacologicClassId,
                pharmClassCode: pharmClassCode,
                pharmClassName: pharmClassName,
                substanceName: $"Substance {activeMoietyId}",
                parameterCategory: parameterCategory,
                parameterName: parameterName ?? $"{parameterCategory} term",
                significance: significance,
                isPlaceboControlled: isPlaceboControlled,
                isCombo: isCombo,
                rr: rr,
                rrLowerBound: rr * 0.7,
                rrUpperBound: rr * 1.4,
                calculationFlags: calculationFlags,
                eventsTreatment: eventsTreatment,
                eventsComparator: eventsComparator);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Seeds a minimal authenticated user for favorite FK-compatible tests.
        /// </summary>
        private static async Task seedUserAsync(ApplicationDbContext context, long userId)
        {
            #region implementation

            context.Users.Add(new User
            {
                Id = userId,
                UserName = $"user{userId}@example.test",
                NormalizedUserName = $"USER{userId}@EXAMPLE.TEST",
                Email = $"user{userId}@example.test",
                NormalizedEmail = $"USER{userId}@EXAMPLE.TEST",
                PrimaryEmail = $"user{userId}@example.test",
                CreatedAt = DateTime.UtcNow,
                SecurityStamp = Guid.NewGuid().ToString()
            });

            await context.SaveChangesAsync();

            #endregion
        }

        #endregion helpers
    }
}
