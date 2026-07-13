using MedRecPro.DataAccess;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace MedRecProTest
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
    public class DtoLabelAccessCallerInventoryTests
    {
        private const int ExpectedCallerCount = 45;
        private const string ExpectedCallerSnapshotHash = "E1F9BEB331335B9D1A2AA8EBAC229095DCA4710C2EF373EEE8CC2CC048C4347F";

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

            Assert.AreEqual(ExpectedCallerCount, callers.Count, "An executable first-party static caller was added or removed.");
            Assert.AreEqual(ExpectedCallerSnapshotHash, hash, "A caller-to-facade-method mapping changed without an explicit ownership decision.");

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
        /// Locates the repository root from the test output directory.
        /// </summary>
        /// <returns>The directory containing MedRecPro.sln.</returns>
        /// <seealso cref="DirectoryInfo"/>
        private static string findRepoRoot()
        {
            #region implementation

            for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
            {
                if (File.Exists(Path.Combine(directory.FullName, "MedRecPro.sln")))
                {
                    return directory.FullName;
                }
            }

            throw new DirectoryNotFoundException("Unable to locate the MedRecPro repository root.");

            #endregion
        }
    }
}
