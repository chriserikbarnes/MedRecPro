using MedRecPro.DataAccess;
using MedRecProTest.TestInfrastructure;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace MedRecProTest.Architecture
{
    /**************************************************************/
    /// <summary>
    /// Freezes the executable first-party callers of the legacy DtoLabelAccess facade.
    /// </summary>
    /// <remarks>
    /// This is a Phase 0 migration inventory: new or removed static callers require
    /// an explicit decision about their eventual feature-service owner.
    /// </remarks>
    /// <seealso cref="DtoLabelAccess"/>
    [TestClass]
    [TestCategory("Architecture")]
    public class DtoLabelAccessCallerInventoryTests
    {
        private const int ExpectedCallerCount = 0;
        private const string ExpectedCallerSnapshotHash = "E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855";

        /**************************************************************/
        /// <summary>
        /// Verifies the executable-caller inventory has not changed without review.
        /// </summary>
        /// <seealso cref="getExecutableCallers"/>
        [TestMethod]
        public void DtoLabelAccess_ExecutableFirstPartyCallers_AreFrozen()
        {
            #region implementation

            var callers = getExecutableCallers();
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n", callers))));

            Assert.AreEqual(ExpectedCallerCount, callers.Count, "Production code must use injected feature services instead of DtoLabelAccess.");
            Assert.AreEqual(ExpectedCallerSnapshotHash, hash, "An executable first-party static caller was added after the facade migration.");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Collects executable DtoLabelAccess invocations from production source files.
        /// </summary>
        /// <returns>Sorted repository-relative caller and method keys.</returns>
        /// <seealso cref="DtoLabelAccess"/>
        private static List<string> getExecutableCallers()
        {
            #region implementation

            var root = findRepoRoot();
            var productionRoot = Path.Combine(root, "MedRecPro");
            var callers = new List<string>();
            var callPattern = new Regex(@"\bDtoLabelAccess\.(?<method>[A-Za-z0-9_]+)\s*\(", RegexOptions.CultureInvariant);

            foreach (var file in Directory.EnumerateFiles(productionRoot, "*.cs", SearchOption.AllDirectories))
            {
                var relativePath = Path.GetRelativePath(root, file).Replace('\\', '/');
                if (relativePath.Contains("/bin/", StringComparison.Ordinal)
                    || relativePath.Contains("/obj/", StringComparison.Ordinal)
                    || relativePath.Contains("/Todo/", StringComparison.Ordinal)
                    || Path.GetFileName(file).StartsWith("DtoLabelAccess", StringComparison.Ordinal)
                    || Path.GetFileName(file).Equals("AeDashboardFavoriteAccess.cs", StringComparison.Ordinal))
                {
                    continue;
                }

                foreach (var line in File.ReadLines(file))
                {
                    if (line.TrimStart().StartsWith("///", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    foreach (Match match in callPattern.Matches(line))
                    {
                        callers.Add($"{relativePath}:{match.Groups["method"].Value}");
                    }
                }
            }

            return callers.OrderBy(value => value, StringComparer.Ordinal).ToList();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Locates the repository root from build-time assembly metadata.
        /// </summary>
        /// <returns>The directory containing MedRecPro.sln.</returns>
        /// <seealso cref="DirectoryInfo"/>
        private static string findRepoRoot()
        {
            #region implementation

            return RepositorySourceRoot.RootPath;

            #endregion
        }
    }
}
