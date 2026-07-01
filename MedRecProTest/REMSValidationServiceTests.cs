using MedRecPro.Service.ParsingValidators;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static MedRecPro.Models.Label;

namespace MedRecPro.Service.Test
{
    /**************************************************************/
    /// <summary>
    /// Tests REMS validation service public methods.
    /// </summary>
    /// <seealso cref="REMSValidationService"/>
    [TestClass]
    public class REMSValidationServiceTests
    {
        #region implementation

        /**************************************************************/
        /// <summary>
        /// Verifies protocol validation reports invalid protocol codes.
        /// </summary>
        /// <seealso cref="REMSValidationService.ValidateProtocol"/>
        [TestMethod]
        public void ValidateProtocol_InvalidProtocolCode_ReturnsErrors()
        {
            #region implementation
            var sut = new REMSValidationService();
            var protocol = new Protocol
            {
                ProtocolCode = "invalid code with spaces",
                ProtocolCodeSystem = "2.16.840.1.113883.3.26.1.1"
            };

            var result = sut.ValidateProtocol(protocol);

            Assert.IsFalse(result.IsValid);
            Assert.IsTrue(result.Errors.Count > 0);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies stakeholder validation prefixes member names in errors.
        /// </summary>
        /// <seealso cref="REMSValidationService.ValidateStakeholder"/>
        [TestMethod]
        public void ValidateStakeholder_InvalidStakeholderCode_ReturnsMemberErrors()
        {
            #region implementation
            var sut = new REMSValidationService();
            var stakeholder = new Stakeholder
            {
                StakeholderCode = "invalid stakeholder",
                StakeholderCodeSystem = "2.16.840.1.113883.3.26.1.1"
            };

            var result = sut.ValidateStakeholder(stakeholder);

            Assert.IsFalse(result.IsValid);
            Assert.IsTrue(result.Errors.Any(error => error.Contains("Stakeholder code must contain only alphanumeric characters.")));
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies requirement validation applies monitoring-observation business rules.
        /// </summary>
        /// <seealso cref="REMSValidationService.ValidateRequirement"/>
        [TestMethod]
        public void ValidateRequirement_MonitoringObservationWrongSequence_ReturnsBusinessRuleError()
        {
            #region implementation
            var sut = new REMSValidationService();
            var requirement = new Requirement
            {
                IsMonitoringObservation = true,
                RequirementSequenceNumber = 1
            };

            var result = sut.ValidateRequirement(requirement);

            Assert.IsFalse(result.IsValid);
            Assert.IsTrue(result.Errors.Any(error => error.Contains("Monitoring observations")));
            #endregion
        }

        #endregion
    }
}
