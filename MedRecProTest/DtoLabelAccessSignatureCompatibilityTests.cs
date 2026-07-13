using MedRecPro.DataAccess;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace MedRecProTest
{
    /**************************************************************/
    /// <summary>
    /// Freezes the full legacy DtoLabelAccess overload contract.
    /// </summary>
    /// <remarks>
    /// This test deliberately captures more than the public-surface inventory:
    /// overloads, return types, parameter order and names, nullable reference
    /// metadata, and optional defaults are all compatibility requirements.
    /// </remarks>
    /// <seealso cref="DtoLabelAccess"/>
    [TestClass]
    public class DtoLabelAccessSignatureCompatibilityTests
    {
        private const string ExpectedSnapshotHash = "A085BA4CCCDD08CDEABD689EAD2650FED88A33F1531298D87211D0B2C6FFD530";

        /**************************************************************/
        /// <summary>
        /// Verifies every public facade overload against the frozen compatibility fixture.
        /// </summary>
        /// <remarks>
        /// The hash covers the canonical ordered list of all return types,
        /// parameters, nullability annotations, and optional defaults.
        /// </remarks>
        /// <seealso cref="signatureKeyFor"/>
        [TestMethod]
        public void DtoLabelAccess_PublicSignatures_AreFrozen()
        {
            #region implementation

            var actual = typeof(DtoLabelAccess)
                .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .OrderBy(signatureKeyFor, StringComparer.Ordinal)
                .Select(signatureKeyFor)
                .ToList();

            var snapshotHash = Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n", actual))));
            var methodNameCount = typeof(DtoLabelAccess)
                .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Select(method => method.Name)
                .Distinct(StringComparer.Ordinal)
                .Count();

            Assert.AreEqual(58, actual.Count, "The frozen facade must contain all 58 public overloads.");
            Assert.AreEqual(58, actual.Distinct(StringComparer.Ordinal).Count(), "Each frozen overload must have one unique signature key.");
            Assert.AreEqual(57, methodNameCount, "BuildDocumentsAsync is the only supported overloaded facade method.");
            Assert.AreEqual(ExpectedSnapshotHash, snapshotHash, "A facade signature, nullability annotation, or default value changed.");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Produces a canonical, order-stable compatibility key for one facade overload.
        /// </summary>
        /// <param name="method">The public legacy facade method to describe.</param>
        /// <returns>The complete method signature including reference nullability and defaults.</returns>
        /// <seealso cref="NullabilityInfoContext"/>
        private static string signatureKeyFor(MethodInfo method)
        {
            #region implementation

            var nullability = new NullabilityInfoContext();
            var parameters = method.GetParameters().Select(parameter =>
            {
                var nullable = nullability.Create(parameter).ReadState == NullabilityState.Nullable ? "?" : string.Empty;
                var optional = parameter.IsOptional ? $" = {formatDefault(parameter.DefaultValue)}" : string.Empty;
                return $"{formatType(parameter.ParameterType)}{nullable} {parameter.Name}{optional}";
            });

            return $"{formatType(method.ReturnType)} {method.Name}({string.Join(", ", parameters)})";

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Formats a type without assembly qualification.
        /// </summary>
        /// <param name="type">The reflected parameter or return type.</param>
        /// <returns>A stable type descriptor.</returns>
        /// <seealso cref="signatureKeyFor"/>
        private static string formatType(Type type)
        {
            #region implementation

            if (!type.IsGenericType)
            {
                return type.FullName ?? type.Name;
            }

            var genericName = type.GetGenericTypeDefinition().FullName!;
            genericName = genericName[..genericName.IndexOf('`')];
            return $"{genericName}<{string.Join(",", type.GetGenericArguments().Select(formatType))}>";

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Formats a reflected optional parameter default value.
        /// </summary>
        /// <param name="value">The reflection default value.</param>
        /// <returns>A stable display value for the compatibility key.</returns>
        /// <seealso cref="signatureKeyFor"/>
        private static string formatDefault(object? value)
        {
            #region implementation

            return value == null || value == Missing.Value
                ? "null"
                : value is string text
                    ? $"\"{text}\""
                    : value.ToString() ?? string.Empty;

            #endregion
        }
    }
}
