using MedRecPro.Models;
using System.ComponentModel.DataAnnotations;
using static MedRecPro.Models.Label;

namespace MedRecPro.Service.ParsingValidators
{
    /**************************************************************/
    /// <summary>
    /// Service class showing manual validation usage.
    /// Demonstrates how to use validation attributes outside of MVC model binding.
    /// </summary>
    /// <seealso cref="Protocol"/>
    /// <seealso cref="Label"/>
    public class REMSValidationService
    {

        /**************************************************************/
        /// <summary>
        /// Result class for validation operations.
        /// </summary>
        /// <seealso cref="Label"/>
        public class ValidationResultSummary
        {
            #region implementation
            public bool IsValid { get; set; }
            public List<string> Errors { get; set; } = new List<string>();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Result class for save operations.
        /// </summary>
        /// <seealso cref="Label"/>
        public class SaveResult
        {
            #region implementation
            public bool Success { get; set; }
            public List<string> Errors { get; set; } = new List<string>();
            #endregion
        }

        #region implementation

        /**************************************************************/
        /// <summary>
        /// Validates a protocol object manually using validation attributes.
        /// Useful for validation in services, background jobs, or business logic.
        /// </summary>
        /// <param name="protocol">Protocol to validate.</param>
        /// <returns>Validation results with any errors found.</returns>
        /// <example>
        /// <code>
        /// var validationService = new REMSValidationService();
        /// var results = validationService.ValidateProtocol(protocol);
        /// if (!results.IsValid)
        /// {
        ///     foreach (var error in results.Errors)
        ///         Console.WriteLine(error);
        /// }
        /// </code>
        /// </example>
        /// <seealso cref="Protocol"/>
        /// <seealso cref="Label"/>
        public ValidationResultSummary ValidateProtocol(Protocol protocol)
        {
            #region implementation
            var context = new ValidationContext(protocol, serviceProvider: null, items: null);
            var validationResults = new List<ValidationResult>();

            // Validate all properties with validation attributes
            bool isValid = Validator.TryValidateObject(protocol, context, validationResults, validateAllProperties: true);

            return new ValidationResultSummary
            {
                IsValid = isValid,
                Errors = validationResults.SelectMany(r => r.ErrorMessage != null ? new[] { r.ErrorMessage } : Array.Empty<string>()).ToList()
            };
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Validates a stakeholder object with detailed error reporting.
        /// </summary>
        /// <param name="stakeholder">Stakeholder to validate.</param>
        /// <returns>Detailed validation results.</returns>
        /// <seealso cref="Stakeholder"/>
        /// <seealso cref="Label"/>
        public ValidationResultSummary ValidateStakeholder(Stakeholder stakeholder)
        {
            #region implementation
            var context = new ValidationContext(stakeholder);
            var validationResults = new List<ValidationResult>();

            bool isValid = Validator.TryValidateObject(stakeholder, context, validationResults, validateAllProperties: true);

            var errors = new List<string>();
            foreach (var result in validationResults)
            {
                if (result.ErrorMessage != null)
                {
                    errors.Add($"{string.Join(", ", result.MemberNames)}: {result.ErrorMessage}");
                }
            }

            return new ValidationResultSummary
            {
                IsValid = isValid,
                Errors = errors
            };
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Validates a requirement with custom business rules.
        /// Shows how to combine attribute validation with additional logic.
        /// </summary>
        /// <param name="requirement">Requirement to validate.</param>
        /// <returns>Comprehensive validation results.</returns>
        /// <seealso cref="Requirement"/>
        /// <seealso cref="Label"/>
        public ValidationResultSummary ValidateRequirement(Requirement requirement)
        {
            #region implementation
            var context = new ValidationContext(requirement);
            var validationResults = new List<ValidationResult>();

            // Run standard validation attributes
            bool isValid = Validator.TryValidateObject(requirement, context, validationResults, validateAllProperties: true);

            // Add custom business validation rules
            if (requirement.IsMonitoringObservation == true && requirement.RequirementSequenceNumber != 2)
            {
                validationResults.Add(new ValidationResult(
                    "Monitoring observations should typically occur during substance administration (sequence 2)",
                    new[] { nameof(requirement.RequirementSequenceNumber) }));
                isValid = false;
            }

            return new ValidationResultSummary
            {
                IsValid = isValid,
                Errors = validationResults.Select(r => r.ErrorMessage ?? "Unknown error").ToList()
            };
            #endregion
        }
        #endregion
    }
}
