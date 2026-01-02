using MedRecProImportClass.Models;
using static MedRecProImportClass.Models.Label;

namespace MedRecProImportClass.Service.ParsingServices
{
    /**************************************************************/
    /// <summary>
    /// Enforces mutually exclusive cosmetic product category code business 
    /// rules (SPL Implementation Guide 3.4.3).
    /// </summary>
    /// <seealso cref="Label"/>
    /// <seealso cref="SpecializedKind"/>
    public static class SpecializedKindValidatorService
    {
        #region implementation

        /**************************************************************/
        /// <summary>
        /// Exempt document types (allowed to have conflicting codes).
        /// </summary>
        /// <seealso cref="Label"/>
        private static readonly HashSet<string> exemptDocTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "103573-2", // Cosmetic Facility Registration
            "X8888-1",  // Amendment
            "X8888-4"   // Biennial Renewal
        };

        /**************************************************************/
        /// <summary>
        /// List of mutually exclusive code pairs. Each entry: (Code1, Code2).
        /// </summary>
        /// <seealso cref="Label"/>
        private static readonly (string, string)[] mutuallyExclusivePairs =
        {
            ("01D1", "01D2"), ("01D2", "01D1"),
            ("06F1", "06F2"), ("06F2", "06F1"),
            ("06I1", "06I2"), ("06I2", "06I1"),
            ("07C1", "07C2"), ("07C2", "07C1"),
            ("07D1", "07D2"), ("07D2", "07D1"),
            ("07I1", "07I2"), ("07I2", "07I1"),
            ("12D1", "12D2"), ("12D2", "12D1"),
            ("12F1", "12F2"), ("12F2", "12F1"),
            ("14C1", "14C2"), ("14C2", "14C1"),
            ("14D1", "14D2"), ("14D2", "14D1"),
            ("14J1", "14J2"), ("14J2", "14J1"),
            ("06A1", "06A2"), ("06A2", "06A1")
        };

        #endregion

        /**************************************************************/
        /// <summary>
        /// Validates a collection of SpecializedKind codes against SPL cosmetic mutual exclusion rules.
        /// </summary>
        /// <param name="kinds">All SpecializedKind records (for a single product)</param>
        /// <param name="documentTypeCode">Document type code (from the SPL document)</param>
        /// <param name="logger">Logger for rule violations</param>
        /// <param name="rejectedKinds">OUT: The rejected codes with explanations</param>
        /// <returns>List of SpecializedKind entities to keep (pass validation)</returns>
        /// <remarks>
        /// This method enforces SPL Implementation Guide 3.4.3 business rules for cosmetic products.
        /// Certain document types are exempt from mutual exclusion rules, allowing conflicting codes.
        /// For non-exempt documents, when mutually exclusive codes are found, one is arbitrarily removed.
        /// Only codes with FDA Product Classification System (2.16.840.1.113883.6.303) are evaluated.
        /// </remarks>
        /// <example>
        /// <code>
        /// var validKinds = SpecializedKindValidator.ValidateCosmeticCategoryRules(
        ///     productKinds, documentType, logger, out var rejected);
        /// foreach (var (kind, reason) in rejected)
        /// {
        ///     Console.WriteLine($"Rejected {kind.KindCode}: {reason}");
        /// }
        /// </code>
        /// </example>
        /// <seealso cref="SpecializedKind"/>
        /// <seealso cref="Label"/>
        public static List<SpecializedKind> ValidateCosmeticCategoryRules(
            IEnumerable<SpecializedKind> kinds,
            string? documentTypeCode,
            ILogger logger,
            out List<(SpecializedKind Kind, string Reason)> rejectedKinds)
        {
            #region implementation
            var keep = new List<SpecializedKind>();
            rejectedKinds = new List<(SpecializedKind, string)>();

            if (kinds != null && kinds.Any())
            {
                // Quick out if exempt - certain document types allow conflicting codes
                if (!string.IsNullOrWhiteSpace(documentTypeCode) && exemptDocTypes.Contains(documentTypeCode))
                {
                    // All codes allowed for exempt doc types
                    keep.AddRange(kinds);
                    return keep;
                }

                // Only interested in codes with FDA Product Classification System (2.16.840.1.113883.6.303)
                var cosmeticKinds = kinds
                    .Where(k => string.Equals(k.KindCodeSystem, "2.16.840.1.113883.6.303", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                // Process cosmetic kinds if any exist
                if (cosmeticKinds != null && cosmeticKinds.Any())
                {
                    // Create a set of all cosmetic codes for efficient lookup
                    var codeSet = new HashSet<string>(
                       cosmeticKinds?.Select(static k => k.KindCode ?? string.Empty) ?? Enumerable.Empty<string>(),
                       StringComparer.OrdinalIgnoreCase
                    );

                    // Flag codes to remove based on mutual exclusion rules
                    var removeCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                    // Check each mutually exclusive pair for conflicts
                    foreach (var (codeA, codeB) in mutuallyExclusivePairs)
                    {
                        if (codeSet != null
                            && removeCodes != null
                            && !string.IsNullOrWhiteSpace(codeA)
                            && !string.IsNullOrWhiteSpace(codeB)
                            && codeSet.Contains(codeA)
                            && codeSet.Contains(codeB))
                        {
                            // Both are present, must reject one (arbitrary: reject codeB)
                            removeCodes.Add(codeB);

                            logger.LogWarning(
                                $"Cosmetic category codes '{codeA}' and '{codeB}' are mutually exclusive for this product and document type '{documentTypeCode}'. Removing '{codeB}'."
                            );
                        }
                    }

                    // Build result set, categorizing each kind as kept or rejected
                    foreach (var kind in kinds)
                    {
                        if (kind == null || string.IsNullOrWhiteSpace(kind.KindCode))
                        {
                            // Skip null or empty codes
                            continue;
                        }

                        if (removeCodes == null || removeCodes.Count == 0)
                        {
                            // No codes to remove, keep all kinds
                            keep.Add(kind);
                            continue;
                        }

                        if (removeCodes.Contains(kind.KindCode ?? ""))
                        {
                            rejectedKinds.Add((kind, $"Conflicts with mutually exclusive code in SPL business rules."));
                        }
                        else
                        {
                            keep.Add(kind);
                        }
                    }
                }
                else
                {
                    // No cosmetic kinds found, keep all kinds as-is
                    keep.AddRange(kinds);
                }
            }

            return keep;
            #endregion
        }
    }
}