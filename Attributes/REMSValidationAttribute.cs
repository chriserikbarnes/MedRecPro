using static MedRecPro.Models.Label;

namespace MedRecPro.Models.Validation
{
    /**************************************************************/
    /// <summary>
    /// Custom validation attribute for REMS protocol codes to ensure they conform to SPL Implementation Guide requirements.
    /// Validates that protocol codes are from the FDA SPL code system (2.16.840.1.113883.3.26.1.1).
    /// </summary>
    /// <seealso cref="Protocol"/>
    /// <seealso cref="Label"/>
    public class REMSProtocolCodeValidationAttribute : System.ComponentModel.DataAnnotations.ValidationAttribute
    {
        #region implementation
        /// <summary>
        /// Validates the REMS protocol code against SPL requirements.
        /// </summary>
        /// <param name="value">The protocol code value to validate.</param>
        /// <param name="validationContext">The validation context containing the Protocol model.</param>
        /// <returns>ValidationResult indicating success or specific validation errors.</returns>
        /// <seealso cref="Protocol"/>
        /// <seealso cref="Label"/>
        protected override System.ComponentModel.DataAnnotations.ValidationResult? IsValid(object? value, System.ComponentModel.DataAnnotations.ValidationContext validationContext)
        {
            #region implementation
            var protocolCode = value as string;
            var protocol = validationContext.ObjectInstance as Protocol;

            if (protocol == null)
            {
                return new System.ComponentModel.DataAnnotations.ValidationResult("Protocol context is required for validation.");
            }

            // Protocol code is required for REMS protocols
            if (string.IsNullOrWhiteSpace(protocolCode))
            {
                return new System.ComponentModel.DataAnnotations.ValidationResult("Protocol code is required for REMS protocols (SPL IG 23.2.6.3).");
            }

            // Code system validation - must be FDA SPL system
            if (string.IsNullOrWhiteSpace(protocol.ProtocolCodeSystem))
            {
                return new System.ComponentModel.DataAnnotations.ValidationResult("Protocol code system is required when protocol code is specified.");
            }

            if (protocol.ProtocolCodeSystem != "2.16.840.1.113883.3.26.1.1")
            {
                return new System.ComponentModel.DataAnnotations.ValidationResult("Protocol code system must be 2.16.840.1.113883.3.26.1.1 for FDA SPL compliance (SPL IG 23.2.6.5).");
            }

            // Basic format validation
            if (!System.Text.RegularExpressions.Regex.IsMatch(protocolCode.Trim(), @"^[A-Za-z0-9]+$"))
            {
                return new System.ComponentModel.DataAnnotations.ValidationResult("Protocol code must contain only alphanumeric characters.");
            }

            return System.ComponentModel.DataAnnotations.ValidationResult.Success;
            #endregion
        }
        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Custom validation attribute for REMS stakeholder codes to ensure they conform to SPL Implementation Guide requirements.
    /// Validates that stakeholder codes are from the FDA SPL code system and represent valid stakeholder roles.
    /// </summary>
    /// <seealso cref="Stakeholder"/>
    /// <seealso cref="Label"/>
    public class REMSStakeholderCodeValidationAttribute : System.ComponentModel.DataAnnotations.ValidationAttribute
    {
        #region implementation
        /// <summary>
        /// Validates the REMS stakeholder code against SPL requirements.
        /// </summary>
        /// <param name="value">The stakeholder code value to validate.</param>
        /// <param name="validationContext">The validation context containing the Stakeholder model.</param>
        /// <returns>ValidationResult indicating success or specific validation errors.</returns>
        /// <seealso cref="Stakeholder"/>
        /// <seealso cref="Label"/>
        protected override System.ComponentModel.DataAnnotations.ValidationResult? IsValid(object? value, System.ComponentModel.DataAnnotations.ValidationContext validationContext)
        {
            #region implementation
            var stakeholderCode = value as string;
            var stakeholder = validationContext.ObjectInstance as Stakeholder;

            if (stakeholder == null)
            {
                return new System.ComponentModel.DataAnnotations.ValidationResult("Stakeholder context is required for validation.");
            }

            // Stakeholder code is required for REMS stakeholders
            if (string.IsNullOrWhiteSpace(stakeholderCode))
            {
                return new System.ComponentModel.DataAnnotations.ValidationResult("Stakeholder code is required for REMS stakeholders (SPL IG 23.2.7.19).");
            }

            // Code system validation - must be FDA SPL system
            if (string.IsNullOrWhiteSpace(stakeholder.StakeholderCodeSystem))
            {
                return new System.ComponentModel.DataAnnotations.ValidationResult("Stakeholder code system is required when stakeholder code is specified.");
            }

            if (stakeholder.StakeholderCodeSystem != "2.16.840.1.113883.3.26.1.1")
            {
                return new System.ComponentModel.DataAnnotations.ValidationResult("Stakeholder code system must be 2.16.840.1.113883.3.26.1.1 for FDA SPL compliance (SPL IG 23.2.7.19).");
            }

            // Basic format validation
            if (!System.Text.RegularExpressions.Regex.IsMatch(stakeholderCode.Trim(), @"^[A-Za-z0-9]+$"))
            {
                return new System.ComponentModel.DataAnnotations.ValidationResult("Stakeholder code must contain only alphanumeric characters.");
            }

            return System.ComponentModel.DataAnnotations.ValidationResult.Success;
            #endregion
        }
        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Custom validation attribute for REMS requirement codes to ensure they conform to SPL Implementation Guide requirements.
    /// Validates that requirement codes are from the FDA SPL code system and have proper sequence numbering.
    /// </summary>
    /// <seealso cref="Requirement"/>
    /// <seealso cref="Label"/>
    public class REMSRequirementValidationAttribute : System.ComponentModel.DataAnnotations.ValidationAttribute
    {
        #region implementation
        /// <summary>
        /// Validates the REMS requirement against SPL requirements.
        /// </summary>
        /// <param name="value">The requirement code value to validate.</param>
        /// <param name="validationContext">The validation context containing the Requirement model.</param>
        /// <returns>ValidationResult indicating success or specific validation errors.</returns>
        /// <seealso cref="Requirement"/>
        /// <seealso cref="Label"/>
        protected override System.ComponentModel.DataAnnotations.ValidationResult? IsValid(object? value, System.ComponentModel.DataAnnotations.ValidationContext validationContext)
        {
            #region implementation
            var requirementCode = value as string;
            var requirement = validationContext.ObjectInstance as Requirement;

            if (requirement == null)
            {
                return new System.ComponentModel.DataAnnotations.ValidationResult("Requirement context is required for validation.");
            }

            // Requirement code is required
            if (string.IsNullOrWhiteSpace(requirementCode))
            {
                return new System.ComponentModel.DataAnnotations.ValidationResult("Requirement code is required for REMS requirements (SPL IG 23.2.7.7).");
            }

            // Code system validation - must be FDA SPL system
            if (string.IsNullOrWhiteSpace(requirement.RequirementCodeSystem))
            {
                return new System.ComponentModel.DataAnnotations.ValidationResult("Requirement code system is required when requirement code is specified.");
            }

            if (requirement.RequirementCodeSystem != "2.16.840.1.113883.3.26.1.1")
            {
                return new System.ComponentModel.DataAnnotations.ValidationResult("Requirement code system must be 2.16.840.1.113883.3.26.1.1 for FDA SPL compliance (SPL IG 23.2.7.8).");
            }

            // Sequence number validation (SPL IG 23.2.7.2)
            if (requirement.RequirementSequenceNumber.HasValue)
            {
                var sequenceNumber = requirement.RequirementSequenceNumber.Value;
                if (sequenceNumber < 1 || sequenceNumber > 3)
                {
                    return new System.ComponentModel.DataAnnotations.ValidationResult("Requirement sequence number must be 1, 2, or 3 (SPL IG 23.2.7.2).");
                }
            }

            return System.ComponentModel.DataAnnotations.ValidationResult.Success;
            #endregion
        }
        #endregion
    }
}
