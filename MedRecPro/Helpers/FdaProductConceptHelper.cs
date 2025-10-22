using MedRecPro.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace MedRecPro.Helpers
{
    /**************************************************************/
    /// <summary>
    /// Helper methods for generating FDA SPL Product Concept Codes according to the specification.
    /// Implements MD5 hash generation for product and kit concept codes with normalized strength calculations.
    /// </summary>
    /// <seealso cref="Label"/>
    public static class FdaProductConceptHelper
    {
        #region implementation - unit conversion table

        /// <summary>
        /// Normalized units conversion table as specified in Table 17 of the FDA SPL Implementation Guide.
        /// Contains conversion factors and normalized unit symbols for various measurement types.
        /// </summary>
        /// <example>
        /// var factor = NormalizedUnits["g"].Factor; // Returns 1000
        /// var unit = NormalizedUnits["g"].NormalizedUnit; // Returns "mg"
        /// </example>
        /// <seealso cref="NormalizeUnit"/>
        private static readonly Dictionary<string, (double Factor, string NormalizedUnit)> NormalizedUnits =
            new Dictionary<string, (double, string)>
            {
            // Amount of substance
            { "mmol", (1.0, "mmol") },
            { "nmol", (1e-3, "mmol") },
            
            // Amount of valence
            { "meq", (1.0, "meq") },
            
            // Area
            { "cm2", (1.0, "cm2") },
            
            // Elapsed time
            { "d", (86400.0, "s") },
            { "h", (3600.0, "s") },
            { "min", (60.0, "s") },
            
            // Katalytic activity
            { "U", (1.0, "U") },
            
            // Mass
            { "g", (1000.0, "mg") },
            { "kg", (1000000.0, "mg") },
            { "mg", (1.0, "mg") },
            { "ng", (1e-6, "mg") },
            { "ug", (1e-3, "mg") },
            
            // Radioactivity
            { "Ci", (37000.0, "MBq") },
            { "mCi", (37.0, "MBq") },
            
            // Volume
            { "L", (1000.0, "mL") },
            { "mL", (1.0, "mL") },
            { "uL", (1e-3, "mL") }
            };

        #endregion

        #region implementation - core structures

        /**************************************************************/
        /// <summary>
        /// Represents an active ingredient with its code, strength, and optional moiety information.
        /// Used in the construction of product concept codes.
        /// </summary>
        /// <seealso cref="GenerateProductConceptCode"/>
        public class ActiveIngredient
        {
            /// <summary>
            /// The UNII code of the active ingredient.
            /// </summary>
            public string? UniiCode { get; set; }

            /// <summary>
            /// The strength numerator value.
            /// </summary>
            public double StrengthNumerator { get; set; }

            /// <summary>
            /// The unit for the strength numerator.
            /// </summary>
            public string? StrengthNumeratorUnit { get; set; }

            /// <summary>
            /// The strength denominator value. Defaults to 1 if not specified.
            /// </summary>
            public double StrengthDenominator { get; set; } = 1.0;

            /// <summary>
            /// The unit for the strength denominator. Defaults to "1" if not specified.
            /// </summary>
            public string StrengthDenominatorUnit { get; set; } = "1";

            /// <summary>
            /// Optional moiety UNII code when basis of strength is active moiety (ACTIM) or reference substance (ACTIR).
            /// </summary>
            public string? MoietyUniiCode { get; set; }
        }

        /**************************************************************/
        /// <summary>
        /// Represents a kit part with its concept code and quantity information.
        /// Used in the construction of kit concept codes.
        /// </summary>
        /// <seealso cref="GenerateKitConceptCode"/>
        public class KitPart
        {
            /// <summary>
            /// The product concept code of this kit part.
            /// </summary>
            public string? ProductConceptCode { get; set; }

            /// <summary>
            /// The quantity numerator value.
            /// </summary>
            public double QuantityNumerator { get; set; }

            /// <summary>
            /// The unit for the quantity numerator.
            /// </summary>
            public string? QuantityNumeratorUnit { get; set; }
        }

        #endregion

        #region implementation - unit normalization

        /**************************************************************/
        /// <summary>
        /// Normalizes a unit according to the FDA SPL specification rules.
        /// Handles special cases for "1" units, bracketed units, and table lookups.
        /// </summary>
        /// <param name="unit">The unit to normalize.</param>
        /// <returns>A tuple containing the conversion factor and normalized unit symbol.</returns>
        /// <example>
        /// var (factor, normalizedUnit) = NormalizeUnit("g");
        /// // Returns (1000.0, "mg")
        /// </example>
        /// <seealso cref="NormalizedUnits"/>
        private static (double Factor, string NormalizedUnit) NormalizeUnit(string unit)
        {
            // Step 1: If unit is "1", factor is 1 and normalized unit is empty string
            if (unit == "1")
            {
                return (1.0, string.Empty);
            }

            // Step 2: Find unit in normalized units table
            if (NormalizedUnits.ContainsKey(unit))
            {
                return NormalizedUnits[unit];
            }

            // Step 3: If unit is entirely in square brackets, factor is 1 and unit unchanged
            if (unit.StartsWith("[") && unit.EndsWith("]"))
            {
                return (1.0, unit);
            }

            // If not found, assume factor 1 and keep original unit
            return (1.0, unit);
        }

        #endregion

        #region implementation - strength normalization

        /**************************************************************/
        /// <summary>
        /// Normalizes strength values according to the FDA SPL specification.
        /// Converts units using conversion factors and calculates the normalized strength ratio.
        /// </summary>
        /// <param name="numerator">The strength numerator value.</param>
        /// <param name="numeratorUnit">The unit for the numerator.</param>
        /// <param name="denominator">The strength denominator value.</param>
        /// <param name="denominatorUnit">The unit for the denominator.</param>
        /// <returns>A tuple containing the normalized strength number and combined unit string.</returns>
        /// <example>
        /// var (strength, unit) = NormalizeStrength(125, "mg", 5, "mL");
        /// // Returns (25.0, "mg/mL")
        /// </example>
        /// <seealso cref="NormalizeUnit"/>
        private static (double NormalizedStrength, string CombinedUnit) NormalizeStrength(
            double numerator, string numeratorUnit,
            double denominator, string denominatorUnit)
        {
            var (numFactor, numNormalizedUnit) = NormalizeUnit(numeratorUnit);
            var (denFactor, denNormalizedUnit) = NormalizeUnit(denominatorUnit);

            // Calculate normalized strength
            var normalizedStrength = (numerator * numFactor) / (denominator * denFactor);

            // Build combined unit
            string combinedUnit;
            if (string.IsNullOrEmpty(denNormalizedUnit))
            {
                combinedUnit = numNormalizedUnit;
            }
            else
            {
                combinedUnit = $"{numNormalizedUnit}/{denNormalizedUnit}";
            }

            return (normalizedStrength, combinedUnit);
        }

        #endregion

        #region implementation - scientific notation formatting

        /**************************************************************/
        /// <summary>
        /// Formats a number in scientific notation according to the FDA SPL specification.
        /// Uses format "-9.000e-9" with exactly 4 significant digits.
        /// </summary>
        /// <param name="number">The number to format.</param>
        /// <returns>The number formatted in scientific notation.</returns>
        /// <example>
        /// var formatted = FormatScientificNotation(25.0);
        /// // Returns "2.500e1"
        /// </example>
        /// <remarks>
        /// The format ensures consistent representation with a non-zero digit before the decimal point,
        /// exactly 3 digits after the decimal, and minimal exponent padding.
        /// </remarks>
        private static string FormatScientificNotation(double number)
        {
            if (number == 0)
                return "0.000e0";

            // Calculate the exponent
            var exponent = (int)Math.Floor(Math.Log10(Math.Abs(number)));

            // Calculate the mantissa (coefficient)
            var mantissa = number / Math.Pow(10, exponent);

            // Format with exactly 3 decimal places
            var mantissaFormatted = mantissa.ToString("F3");

            // Format exponent without zero padding
            var exponentFormatted = exponent.ToString();

            return $"{mantissaFormatted}e{exponentFormatted}";
        }

        #endregion

        #region implementation - md5 hashing

        /**************************************************************/
        /// <summary>
        /// Computes MD5 hash of the input string and formats it according to FDA SPL specification.
        /// Returns hash in format: 8-4-4-4-12 hexadecimal digits separated by hyphens.
        /// </summary>
        /// <param name="input">The input string to hash.</param>
        /// <returns>The formatted MD5 hash string.</returns>
        /// <example>
        /// var hash = ComputeMd5Hash("C42972|Z49QDT0J8Z~O1R9FJ93ED|2.500e1 mg/mL");
        /// // Returns "7fead104-1147-b435-1545-606b40a2cd6b"
        /// </example>
        /// <seealso cref="GenerateProductConceptCode"/>
        /// <seealso cref="GenerateKitConceptCode"/>
        private static string ComputeMd5Hash(string input)
        {
            using (var md5 = MD5.Create())
            {
                var hashBytes = md5.ComputeHash(Encoding.UTF8.GetBytes(input));
                var hex = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();

                // Format as 8-4-4-4-12
                return $"{hex.Substring(0, 8)}-{hex.Substring(8, 4)}-{hex.Substring(12, 4)}-{hex.Substring(16, 4)}-{hex.Substring(20, 12)}";
            }
        }

        #endregion

        #region public methods

        /**************************************************************/
        /// <summary>
        /// Generates a product concept code according to the FDA SPL specification.
        /// Creates an MD5 hash from the dosage form and active ingredients with normalized strengths.
        /// </summary>
        /// <param name="dosageFormCode">The NCI thesaurus code for the dosage form.</param>
        /// <param name="activeIngredients">List of active ingredients with their strengths.</param>
        /// <returns>The generated product concept code in MD5 hash format.</returns>
        /// <example>
        /// var ingredients = new List&lt;ActiveIngredient&gt;
        /// {
        ///     new ActiveIngredient 
        ///     { 
        ///         UniiCode = "Z49QDT0J8Z", 
        ///         MoietyUniiCode = "O1R9FJ93ED",
        ///         StrengthNumerator = 125, 
        ///         StrengthNumeratorUnit = "mg",
        ///         StrengthDenominator = 5, 
        ///         StrengthDenominatorUnit = "mL" 
        ///     }
        /// };
        /// var code = GenerateProductConceptCode("C42972", ingredients);
        /// // Returns "7fead104-1147-b435-1545-606b40a2cd6b"
        /// </example>
        /// <seealso cref="ActiveIngredient"/>
        /// <seealso cref="GenerateKitConceptCode"/>
        public static string GenerateProductConceptCode(string dosageFormCode, List<ActiveIngredient> activeIngredients)
        {
            var parts = new List<string> { dosageFormCode };

            // Sort ingredients by UNII code alphabetically
            var sortedIngredients = activeIngredients.OrderBy(ai => ai.UniiCode).ToList();

            foreach (var ingredient in sortedIngredients)
            {
                // Build ingredient code with optional moiety
                var ingredientCode = ingredient.UniiCode;
                if (!string.IsNullOrEmpty(ingredient.MoietyUniiCode))
                {
                    ingredientCode += $"~{ingredient.MoietyUniiCode}";
                }

                // Normalize strength
                var (normalizedStrength, combinedUnit) = NormalizeStrength(
                    ingredient.StrengthNumerator, ingredient.StrengthNumeratorUnit,
                    ingredient.StrengthDenominator, ingredient.StrengthDenominatorUnit);

                // Format strength in scientific notation
                var formattedStrength = FormatScientificNotation(normalizedStrength);

                // Build strength string
                var strengthString = $"{formattedStrength} {combinedUnit}";

                parts.Add($"{ingredientCode}|{strengthString}");
            }

            var dataString = string.Join("|", parts);
            return ComputeMd5Hash(dataString);
        }

        /**************************************************************/
        /// <summary>
        /// Generates a kit concept code according to the FDA SPL specification.
        /// Creates an MD5 hash from the kit form code and constituent parts with normalized quantities.
        /// </summary>
        /// <param name="kitParts">List of kit parts with their quantities.</param>
        /// <returns>The generated kit concept code in MD5 hash format.</returns>
        /// <example>
        /// var parts = new List&lt;KitPart&gt;
        /// {
        ///     new KitPart 
        ///     { 
        ///         ProductConceptCode = "a46c150b-8203-ac62-31ef-adb5c0aca5a2", 
        ///         QuantityNumerator = 16.8, 
        ///         QuantityNumeratorUnit = "mL" 
        ///     },
        ///     new KitPart 
        ///     { 
        ///         ProductConceptCode = "bdce178d-00b2-6beb-4d96-259f444aee1d", 
        ///         QuantityNumerator = 0.9, 
        ///         QuantityNumeratorUnit = "g" 
        ///     }
        /// };
        /// var code = GenerateKitConceptCode(parts);
        /// // Returns "a76b62f9-257b-7918-620e-4db706c928f8"
        /// </example>
        /// <seealso cref="KitPart"/>
        /// <seealso cref="GenerateProductConceptCode"/>
        public static string GenerateKitConceptCode(List<KitPart> kitParts)
        {
            var parts = new List<string> { "C47916" }; // Kit form code

            // Sort parts by product concept code alphabetically
            var sortedParts = kitParts.OrderBy(kp => kp.ProductConceptCode).ToList();

            foreach (var part in sortedParts)
            {
                // Normalize quantity (denominator is always 1 for kit parts)
                var (numFactor, numNormalizedUnit) = NormalizeUnit(part.QuantityNumeratorUnit);
                var normalizedQuantity = part.QuantityNumerator * numFactor;

                // Format quantity in scientific notation
                var formattedQuantity = FormatScientificNotation(normalizedQuantity);

                // Build quantity string
                var quantityString = string.IsNullOrEmpty(numNormalizedUnit)
                    ? formattedQuantity
                    : $"{formattedQuantity} {numNormalizedUnit}";

                parts.Add($"{part.ProductConceptCode}|{quantityString}");
            }

            var dataString = string.Join("|", parts);
            return ComputeMd5Hash(dataString);
        }

        /**************************************************************/
        /// <summary>
        /// Validates that a concept code string matches the expected MD5 hash format.
        /// Checks for proper 8-4-4-4-12 hexadecimal digit grouping with hyphens.
        /// </summary>
        /// <param name="conceptCode">The concept code to validate.</param>
        /// <returns>True if the format is valid, false otherwise.</returns>
        /// <example>
        /// var isValid = ValidateConceptCodeFormat("7fead104-1147-b435-1545-606b40a2cd6b");
        /// // Returns true
        /// </example>
        /// <remarks>
        /// This method only validates the format structure, not the actual hash validity.
        /// </remarks>
        public static bool ValidateConceptCodeFormat(string conceptCode)
        {
            if (string.IsNullOrWhiteSpace(conceptCode))
                return false;

            // Check format: 8-4-4-4-12 hexadecimal digits
            var pattern = @"^[a-f0-9]{8}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{12}$";
            return Regex.IsMatch(conceptCode, pattern, RegexOptions.IgnoreCase);
        }

        #endregion
    }
}
