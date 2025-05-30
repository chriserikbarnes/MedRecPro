namespace MedRecPro.Models
{
    public static class Constant
    {
        public enum ActorType
        {
            Aggregator,
            Analyst,
            Approver,
            Auditor,
            Caregiver,
            Collector,
            Collaborator,
            Consumer,
            Contributor,
            DataManager,
            EmergencyContact,
            FamilyMember,
            Institution,
            Investigator,
            LabelAdmin,
            LabelCreator,
            LabelManager,
            LabelUser,
            LegalGuardian,
            Nurse,
            Patient,
            Pharmacist,
            Prescriber,
            PrimaryCareProvider,
            Physician,
            Publisher,
            QualityAssurance,
            QualityControl,
            Regulator,
            ResearchCoordinator,
            ResearchParticipant,
            Researcher,
            ResearcherContact,
            Reviewer,
            Sponsor,
            SurrogateDecisionMaker,
            SystemAdmin,
            Technician,
            Validator
        }

        public enum PermissionType
        {
            Read,
            Write,
            Own,
            Delete,
            Share
        }

        public static int MAX_FAILED_ATTEMPTS = 5;
        public static int LOCKOUT_DURATION_MINUTES = 15;
    }
}
