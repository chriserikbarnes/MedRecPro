namespace MedRecPro.Models
{
    public static class Constant
    {
        public static string XML_NAMESPACE = "urn:hl7-org:v3";

        public const string BLANKET_NO_CHANGES_CERTIFICATION_CODE = "BNCC";
        public const string CERTIFICATION_RELATIONSHIP_TYPE = "Certification";
        public const int CERTIFICATION_RELATIONSHIP_LEVEL = 2;
        public const string DISCIPLINARY_ACTION_ENTITY_TYPE = "DisciplinaryAction";
        public const string COMPLIANCE_ACTION_ENTITY_TYPE = "ComplianceAction";

        // Compliance Action Codes
        public const string INACTIVATED_CODE = "C162847";
        public const string REACTIVATED_CODE = "C162848";

        // Media Types
        public const string PDF_MEDIA_TYPE = "application/pdf";
        public const string PDF_FILE_EXTENSION = ".pdf";

        // Warning Letter Document and Section Codes
        public const string WARNING_LETTER_DOCUMENT_CODE = "77288-9";
        public const string WARNING_LETTER_SECTION_CODE = "48779-3";

        // Other indexing document codes for reference
        public const string BIOLOGIC_DRUG_SUBSTANCE_DOCUMENT_CODE = "77648-4";

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

    /// <summary>
    /// Provides centralized, constant definitions for XML element and attribute names
    /// used in SPL (Structured Product Labeling) files. Using this class prevents
    /// magic strings and reduces errors from typos.
    /// </summary>
    public static class SplConstants
    {
        /**************************************************************/
        /// <summary>
        /// Defines the XML Element namespace used in SPL files.
        /// </summary>
        public static class E // Element Names
        {
            #region Header Elements
            public const string Document = "document";
            public const string Id = "id";
            public const string Code = "code";
            public const string Title = "title";
            public const string EffectiveTime = "effectiveTime";
            public const string SetId = "setId";
            public const string VersionNumber = "versionNumber";
            public const string Author = "author";
            public const string AssignedEntity = "assignedEntity";
            public const string RepresentedOrganization = "representedOrganization";
            public const string AssignedOrganization = "assignedOrganization";
            public const string Name = "name";
            public const string Addr = "addr";
            public const string StreetAddressLine = "streetAddressLine";
            public const string City = "city";
            public const string State = "state";
            public const string PostalCode = "postalCode";
            public const string Country = "country";
            public const string Telecom = "telecom";
            public const string ContactParty = "contactParty";
            public const string ContactPerson = "contactPerson";
            public const string RelatedDocument = "relatedDocument";
            public const string ConfidentialityCode = "confidentialityCode";
            public const string LegalAuthenticator = "legalAuthenticator";
            public const string AssignedPerson = "assignedPerson";
            public const string SignatureText = "signatureText";
            public const string NoteText = "noteText";
            public const string Time = "time";
            public const string AsNamedEntity = "asNamedEntity";
            public const string TerritorialAuthority = "territorialAuthority";
            public const string Territory = "territory";
            public const string GoverningAgency = "governingAgency";
            #endregion

            #region Body & Content Elements
            public const string Component = "component";
            public const string StructuredBody = "structuredBody";
            public const string Section = "section";
            public const string Text = "text";
            public const string Paragraph = "paragraph";
            public const string List = "list";
            public const string Item = "item";
            public const string Caption = "caption";
            public const string Table = "table";
            public const string Thead = "thead";
            public const string Tbody = "tbody";
            public const string Tfoot = "tfoot";
            public const string Tr = "tr";
            public const string Th = "th";
            public const string Td = "td";
            public const string ObservationMedia = "observationMedia";
            public const string RenderMultimedia = "renderMultimedia";
            public const string Reference = "reference";
            public const string Excerpt = "excerpt";
            public const string Highlight = "highlight";
            #endregion

            #region Product & Data Elements
            public const string Subject = "subject";
            public const string SubjectOf = "subjectOf";
            public const string ManufacturedProduct = "manufacturedProduct";
            public const string ManufacturedMaterialKind = "manufacturedMaterialKind";
            public const string FormCode = "formCode";
            public const string Suffix = "suffix";
            public const string Desc = "desc";
            public const string AsEntityWithGeneric = "asEntityWithGeneric";
            public const string GenericMedicine = "genericMedicine";
            public const string AsSpecializedKind = "asSpecializedKind";
            public const string GeneralizedMaterialKind = "generalizedMaterialKind";
            public const string AsEquivalentEntity = "asEquivalentEntity";
            public const string DefiningMaterialKind = "definingMaterialKind";
            public const string AsIdentifiedEntity = "asIdentifiedEntity";
            public const string Ingredient = "ingredient";
            public const string ActiveIngredient = "activeIngredient";
            public const string IngredientSubstance = "ingredientSubstance";
            public const string InactiveIngredientSubstance = "inactiveIngredientSubstance";
            public const string ActiveIngredientSubstance = "activeIngredientSubstance";
            public const string NumeratorIngredientTranslation = "translation";
            public const string DenominatorIngredientTranslation = "translation";
            public const string InactiveIngredient = "inactiveIngredient";
            public const string ActiveMoiety = "activeMoiety";
            public const string DefiningSubstance = "definingSubstance";
            public const string Quantity = "quantity";
            public const string Numerator = "numerator";
            public const string Denominator = "denominator";
            public const string SubstanceSpecification = "substanceSpecification";
            public const string AsContent = "asContent";
            public const string ContainerPackagedProduct = "containerPackagedProduct";
            public const string ContainerPackagedMedicine = "containerPackagedMedicine";
            public const string Approval = "approval";
            public const string MarketingAct = "marketingAct";
            public const string ManufacturedMedicine = "manufacturedMedicine";
            public const string StatusCode = "statusCode";
            public const string Characteristic = "characteristic";
            public const string Value = "value";
            public const string Low = "low";
            public const string High = "high";
            public const string Part = "part";
            public const string PartProduct = "partProduct";
            public const string AsPartOfAssembly = "asPartOfAssembly";
            public const string WholeProduct = "wholeProduct";
            public const string Policy = "policy";
            public const string ConsumedIn = "consumedIn";
            public const string SubstanceAdministration = "substanceAdministration";
            public const string RouteCode = "routeCode";
            public const string Performance = "performance";
            public const string ActDefinition = "actDefinition";
            public const string Product = "product";
            public const string IdentifiedSubstance = "identifiedSubstance";
            public const string InstanceOfKind = "instanceOfKind";
            public const string ProductInstance = "productInstance";
            public const string ExpirationTime = "expirationTime";
            public const string DoseQuantity = "doseQuantity";
            public const string IngredientProductInstance = "ingredientProductInstance";
            public const string Member = "member";
            public const string MemberProductInstance = "memberProductInstance";
            public const string ProductEvent = "productEvent";
            public const string Action = "action";
            public const string Analyte = "analyte";
            public const string PresentSubstance = "presentSubstance";
            public const string ReferenceRange = "referenceRange";
            public const string ObservationCriterion = "observationCriterion";
            public const string Holder = "holder";
            public const string Role = "role";
            public const string PlayingOrganization = "playingOrganization";
            public const string Protocol = "protocol";
            public const string Stakeholder = "stakeholder";
            public const string Requirement = "requirement";
            public const string MonitoringObservation = "monitoringObservation";
            public const string PauseQuantity = "pauseQuantity";
            public const string Period = "period";
            public const string Participation = "participation";
            public const string Issue = "issue";
            public const string SubstanceAdministrationCriterion = "substanceAdministrationCriterion";
            public const string Risk = "risk";
            public const string ConsequenceObservation = "consequenceObservation";
            public const string ManufacturerOrganization = "manufacturerOrganization";
            public const string AdministrableMaterialKind = "administrableMaterialKind";
            public const string AdministrableMaterial = "administrableMaterial";
            public const string Consumable = "consumable";
            public const string Subject2 = "subject2";
            public const string ComponentOf = "componentOf";
            public const string PartProductInstance = "partProductInstance";
            public const string AsInstanceOfKind = "asInstanceOfKind";
            public const string KindOfMaterialKind = "kindOfMaterialKind";
            public const string Performer = "performer";
            public const string SequenceNumber = "sequenceNumber";
            public const string OriginalText = "originalText";
            public const string AsEquivalentSubstance = "asEquivalentSubstance";
            public const string Moiety = "moiety";
            public const string PartMoiety = "partMoiety";

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Defines the XML Attribute names used in SPL files.
        /// </summary>
        public static class A // Attribute Names
        {
            public const string Root = "root";
            public const string ID = "ID"; // Attribute name is "ID"
            public const string Extension = "extension";
            public const string CodeValue = "code"; // Attribute name is "code"
            public const string CodeSystem = "codeSystem";
            public const string DisplayName = "displayName";
            public const string CodeSystemName = "codeSystemName";
            public const string Value = "value";
            public const string Unit = "unit";
            public const string TypeCode = "typeCode";
            public const string ClassCode = "classCode";
            public const string MoodCode = "moodCode";
            public const string Use = "use";
            public const string ListType = "listType";
            public const string StyleCode = "styleCode";
            public const string Width = "width";
            public const string Rowspan = "rowspan";
            public const string Colspan = "colspan";
            public const string Align = "align";
            public const string VAlign = "valign";
            public const string IdAttr = "ID"; // Attribute name is "ID"
            public const string MediaType = "mediaType";
            public const string ReferencedObject = "referencedObject";
            public const string XsiType = "xsi:type";
            public const string NullFlavor = "nullFlavor";
            public const string Closed = "closed";
            public const string Operator = "operator";
            public const string Inclusive = "inclusive";
            public const string Representation = "representation";
            public const string IntegrityCheckAlgorithm = "integrityCheckAlgorithm";
        }

    }

    /**************************************************************/
    /// <summary>
    /// Provides constant values for document comparison analysis operations to avoid magic strings.
    /// </summary>
    /// <remarks>
    /// This class centralizes string constants used throughout the comparison analysis workflow,
    /// improving maintainability and reducing the risk of typos in status messages and headers.
    /// </remarks>
    /// <seealso cref="Label"/>
    public static class ComparisonConstants
    {
        #region implementation

        #region status constants

        /**************************************************************/
        /// <summary>
        /// Status indicating the comparison operation has been queued for processing.
        /// </summary>
        /// <seealso cref="Label"/>
        public const string STATUS_QUEUED = "Queued";

        /**************************************************************/
        /// <summary>
        /// Status indicating the comparison operation is currently being processed.
        /// </summary>
        /// <seealso cref="Label"/>
        public const string STATUS_PROCESSING = "Processing";

        /**************************************************************/
        /// <summary>
        /// Status indicating the comparison operation is analyzing document structure.
        /// </summary>
        /// <seealso cref="Label"/>
        public const string STATUS_ANALYZING = "Analyzing document structure";

        /**************************************************************/
        /// <summary>
        /// Status indicating the comparison operation is finalizing results.
        /// </summary>
        /// <seealso cref="Label"/>
        public const string STATUS_FINALIZING = "Finalizing results";

        /**************************************************************/
        /// <summary>
        /// Status indicating the comparison operation has completed successfully.
        /// </summary>
        /// <seealso cref="Label"/>
        public const string STATUS_COMPLETED = "Completed";

        /**************************************************************/
        /// <summary>
        /// Status indicating the comparison operation was canceled.
        /// </summary>
        /// <seealso cref="Label"/>
        public const string STATUS_CANCELED = "Canceled";

        /**************************************************************/
        /// <summary>
        /// Status indicating the comparison operation failed with an error.
        /// </summary>
        /// <seealso cref="Label"/>
        public const string STATUS_FAILED = "Failed";

        #endregion

        #region http header constants

        /**************************************************************/
        /// <summary>
        /// HTTP response header key for the document GUID being analyzed.
        /// </summary>
        /// <seealso cref="Label"/>
        public const string HEADER_DOCUMENT_GUID = "X-Document-Guid";

        /**************************************************************/
        /// <summary>
        /// HTTP response header key for the operation ID tracking the analysis.
        /// </summary>
        /// <seealso cref="Label"/>
        public const string HEADER_OPERATION_ID = "X-Operation-Id";

        /**************************************************************/
        /// <summary>
        /// HTTP response header key for the type of analysis being performed.
        /// </summary>
        /// <seealso cref="Label"/>
        public const string HEADER_ANALYSIS_TYPE = "X-Analysis-Type";

        /**************************************************************/
        /// <summary>
        /// HTTP response header key for the analysis method (synchronous or asynchronous).
        /// </summary>
        /// <seealso cref="Label"/>
        public const string HEADER_ANALYSIS_METHOD = "X-Analysis-Method";

        /**************************************************************/
        /// <summary>
        /// HTTP response header key for the timestamp when analysis was initiated.
        /// </summary>
        /// <seealso cref="Label"/>
        public const string HEADER_ANALYSIS_TIMESTAMP = "X-Analysis-Timestamp";

        #endregion

        #region analysis type constants

        /**************************************************************/
        /// <summary>
        /// Analysis type identifier for document comparison operations.
        /// </summary>
        /// <seealso cref="Label"/>
        public const string ANALYSIS_TYPE_DOCUMENT_COMPARISON = "DocumentComparison";

        /**************************************************************/
        /// <summary>
        /// Analysis method identifier for synchronous processing.
        /// </summary>
        /// <seealso cref="Label"/>
        public const string ANALYSIS_METHOD_SYNCHRONOUS = "Synchronous";

        /**************************************************************/
        /// <summary>
        /// Analysis method identifier for asynchronous processing.
        /// </summary>
        /// <seealso cref="Label"/>
        public const string ANALYSIS_METHOD_ASYNCHRONOUS = "Asynchronous";

        #endregion

        #region error message constants

        /**************************************************************/
        /// <summary>
        /// Error message for empty or invalid document GUID parameters.
        /// </summary>
        /// <seealso cref="Label"/>
        public const string ERROR_EMPTY_DOCUMENT_GUID = "Document GUID cannot be empty.";

        /**************************************************************/
        /// <summary>
        /// Error message for empty or invalid operation ID parameters.
        /// </summary>
        /// <seealso cref="Label"/>
        public const string ERROR_EMPTY_OPERATION_ID = "Operation ID cannot be empty.";

        /**************************************************************/
        /// <summary>
        /// Generic error message for comparison analysis failures.
        /// </summary>
        /// <seealso cref="Label"/>
        public const string ERROR_ANALYSIS_FAILED = "An error occurred while performing document comparison analysis.";

        /**************************************************************/
        /// <summary>
        /// Error message for queuing operation failures.
        /// </summary>
        /// <seealso cref="Label"/>
        public const string ERROR_QUEUING_FAILED = "An error occurred while queuing document comparison analysis.";

        #endregion

        #region progress percentage constants

        /**************************************************************/
        /// <summary>
        /// Progress percentage when operation is initially queued.
        /// </summary>
        /// <seealso cref="Label"/>
        public const int PROGRESS_QUEUED = 0;

        /**************************************************************/
        /// <summary>
        /// Progress percentage when processing begins.
        /// </summary>
        /// <seealso cref="Label"/>
        public const int PROGRESS_PROCESSING_STARTED = 10;

        /**************************************************************/
        /// <summary>
        /// Progress percentage during document structure analysis.
        /// </summary>
        /// <seealso cref="Label"/>
        public const int PROGRESS_ANALYZING = 30;

        /**************************************************************/
        /// <summary>
        /// Progress percentage when finalizing results.
        /// </summary>
        /// <seealso cref="Label"/>
        public const int PROGRESS_FINALIZING = 90;

        /**************************************************************/
        /// <summary>
        /// Progress percentage when operation is completed.
        /// </summary>
        /// <seealso cref="Label"/>
        public const int PROGRESS_COMPLETED = 100;

        #endregion

        #endregion
    }
}
