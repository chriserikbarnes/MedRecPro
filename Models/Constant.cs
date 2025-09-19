using System;

namespace MedRecPro.Models
{
    public static class Constant
    {
        public static string XML_NAMESPACE = "urn:hl7-org:v3";

        public static readonly string[] ACTIVE_INGREDIENT_CLASS_CODES = { "ACTIB", "ACTIM", "ACTIR" };
        public const string INACTIVE_INGREDIENT_CLASS_CODE = "IACT";

        public const string BLANKET_NO_CHANGES_CERTIFICATION_CODE = "BNCC";
        public const string CERTIFICATION_RELATIONSHIP_TYPE = "Certification";
        public const int CERTIFICATION_RELATIONSHIP_LEVEL = 2;
        public const string DISCIPLINARY_ACTION_ENTITY_TYPE = "DisciplinaryAction";
        public const string COMPLIANCE_ACTION_ENTITY_TYPE = "ComplianceAction";


        // Media Types
        public const string PDF_MEDIA_TYPE = "application/pdf";
        public const string PDF_FILE_EXTENSION = ".pdf";
        public const string JPEG_MEDIA_TYPE = "image/jpeg";
        public const string JPEG_FILE_EXTENSION = ".jpg";
        public const string TEXT_PLAIN_MEDIA_TYPE = "text/plain";

        #region hl7/general codes
        // HL7 and XML General Codes
        public const string DOCUMENT_CLASS_CODE = "DOC";
        public const string EVENT_MOOD_CODE = "EVN";
        public const string OBSERVATION_CLASS_CODE = "OBS";
        public const string GENERIC_KIND_CLASS_CODE = "GEN";
        public const string MANUFACTURED_PRODUCT_CLASS_CODE = "MANU";
        public const string EQUIVALENCE_CLASS_CODE = "EQUIV";
        public const string IDENTIFIED_ENTITY_CLASS_CODE = "IDENT";
        public const string PERFORMER_PARTICIPATION_TYPE_CODE = "PPRF";
        public const string NULL_FLAVOR_OTHER = "OTH";
        public const string NULL_FLAVOR_POSITIVE_INFINITY = "PINF"; 
        #endregion

        #region document relation codes
        // Related Document Type Codes
        public const string APPEND_REL_DOC_TYPE_CODE = "APND";
        public const string DERIVED_REL_DOC_TYPE_CODE = "DRIV";
        public const string REPLACE_REL_DOC_TYPE_CODE = "RPLC";
        public const string SUBJECT_REL_DOC_TYPE_CODE = "SUBJ";
        public const string EXCERPT_REL_DOC_TYPE_CODE = "XCRPT";
        public const string TRANSFORM_REL_DOC_TYPE_CODE = "XFRM"; 
        #endregion

        #region document section codes
        // Document and Section Type Codes
        public const string SPL_PRODUCT_DATA_ELEMENTS_SECTION_CODE = "48780-1";
        public const string SPL_INDEXING_DATA_ELEMENTS_SECTION_CODE = "48779-3";
        public const string ESTABLISHMENT_REGISTRATION_DOC_CODE = "51725-0";
        public const string ESTABLISHMENT_DE_REGISTRATION_DOC_CODE = "70097-1";
        public const string NO_CHANGE_NOTIFICATION_DOC_CODE = "53410-7";
        public const string OUT_OF_BUSINESS_NOTIFICATION_DOC_CODE = "53411-5";
        public const string NDC_NHRIC_LABELER_CODE_REQUEST_DOC_CODE = "51726-8";
        public const string NDC_LABELER_CODE_INACTIVATION_DOC_CODE = "69968-6";
        public const string HUMAN_PRESCRIPTION_DRUG_LABEL_DOC_CODE = "34391-3";
        public const string HUMAN_OTC_DRUG_LABEL_DOC_CODE = "34390-5";
        public const string LOT_DISTRIBUTION_DATA_DOC_CODE = "66105-8";
        public const string REMS_DOCUMENT_DOC_CODE = "82351-8";
        public const string BLANKET_NO_CHANGES_CERTIFICATION_DOC_CODE = "86445-4";
        public const string COSMETIC_PRODUCT_LISTING_DOC_CODE = "103572-4";
        public const string COSMETIC_FACILITY_REGISTRATION_DOC_CODE = "103573-2";
        public const string PACKAGE_LABEL_PRINCIPAL_DISPLAY_PANEL_SECTION_CODE = "51945-4";
        public const string WARNING_LETTER_DOCUMENT_CODE = "77288-9";
        public const string BIOLOGIC_DRUG_SUBSTANCE_DOCUMENT_CODE = "77648-4";
        public const string DRUG_INTERACTIONS_INDEXING_DOC_CODE = "93723-5";
        public const string NCT_INDEXING_DOC_CODE = "93372-1";
        public const string FDA_INITIATED_COMPLIANCE_ACTION_DOC_CODE = "89600-1";
        public const string FDA_INITIATED_COMPLIANCE_ACTION_ANIMAL_DRUG_DOC_CODE = "99282-6";
        public const string WARNING_LETTER_SECTION_CODE = "48779-3"; 
        #endregion

        #region marketing status codes
        // Marketing Status and Category Codes
        public const string MARKETING_STATUS_ACTIVE = "active";
        public const string MARKETING_STATUS_COMPLETED = "completed";
        public const string MARKETING_STATUS_NEW = "new";
        public const string MARKETING_STATUS_CANCELLED = "cancelled";
        public const string MARKETING_CATEGORY_NDA = "C73594";
        public const string MARKETING_CATEGORY_ANDA = "C73584";
        public const string MARKETING_CATEGORY_BLA = "C73585";
        public const string MARKETING_CATEGORY_OTC_MONOGRAPH_DRUG = "C200263";
        public const string MARKETING_CATEGORY_UNAPPROVED_DRUG_OTHER = "C73627";
        public const string MARKETING_CATEGORY_DIETARY_SUPPLEMENT = "C86952";
        public const string MARKETING_CATEGORY_COSMETIC = "C86965";
        public const string MARKETING_CATEGORY_MEDICAL_FOOD = "C86964"; 
        #endregion

        #region business operation codes
        // Business Operation Codes
        public const string MANUFACTURE_CODE = "C43360";
        public const string REPACK_CODE = "C73606";
        public const string RELABEL_CODE = "C73607";
        public const string ANALYSIS_CODE = "C25391";
        public const string API_MANUFACTURE_CODE = "C82401";
        public const string SALVAGE_CODE = "C70827";
        public const string US_AGENT_CODE = "C73330";
        public const string WHOLESALE_DRUG_DISTRIBUTOR_CODE = "C118411";
        public const string THIRD_PARTY_LOGISTICS_PROVIDER_CODE = "C118412";
        public const string HUMAN_DRUG_COMPOUNDING_OUTSOURCING_FACILITY_CODE = "C112113";
        public const string DISTRIBUTE_CODE = "C201565";
        public const string PACK_CODE = "C84731"; 
        #endregion

        #region ingredient form codes
        // Ingredient and Product Form Codes
        public const string ACTIVE_INGREDIENT_BASIS_OF_STRENGTH_CODE = "ACTIB";
        public const string ACTIVE_INGREDIENT_MOIETY_BASIS_CODE = "ACTIM";
        public const string ACTIVE_INGREDIENT_REFERENCE_BASIS_CODE = "ACTIR";
        public const string INACTIVE_INGREDIENT_CODE = "IACT";
        public const string MAY_CONTAIN_CODE = "CNTM";
        public const string KIT_FORM_CODE = "C47916"; 
        #endregion

        #region charactertistic codes
        // Characteristic Codes
        public const string SPL_COLOR_CODE = "SPLCOLOR";
        public const string SPL_SHAPE_CODE = "SPLSHAPE";
        public const string SPL_SIZE_CODE = "SPLSIZE";
        public const string SPL_SCORING_CODE = "SPLSCORE";
        public const string SPL_IMPRINT_CODE = "SPLIMPRINT";
        public const string SPL_FLAVOR_CODE = "SPLFLAVOR";
        public const string SPL_IMAGE_CODE = "SPLIMAGE";
        public const string SPL_COMBINATION_PRODUCT_TYPE_CODE = "SPLCMBPRDTP";
        public const string SPL_SMALL_BUSINESS_CODE = "SPLSMALLBUSINESS";
        public const string SPL_PROFESSIONAL_USE_CODE = "SPLPROFESSIONALUSE";

        public const string BULK_INGREDIENT_CODE = "53409-9";
        public const string BULK_INGREDIENT_ANIMAL_DRUG_CODE = "81203-2";
        public const string OTC_ANIMAL_DRUG_LABEL_CODE = "50577-6";
        public const string OTC_TYPE_A_MEDICATED_ARTICLE_ANIMAL_DRUG_LABEL_CODE = "50576-8";
        public const string OTC_TYPE_B_MEDICATED_FEED_ANIMAL_DRUG_LABEL_CODE = "50574-3";
        public const string OTC_TYPE_C_MEDICATED_FEED_ANIMAL_DRUG_LABEL_CODE = "50573-5";
        public const string PRESCRIPTION_ANIMAL_DRUG_LABEL_CODE = "50578-4";
        public const string VFD_TYPE_A_MEDICATED_ARTICLE_ANIMAL_DRUG_LABEL_CODE = "50575-0";
        public const string VFD_TYPE_B_MEDICATED_FEED_ANIMAL_DRUG_LABEL_CODE = "50572-7";
        public const string VFD_TYPE_C_MEDICATED_FEED_ANIMAL_DRUG_LABEL_CODE = "50571-9";
        public const string COSMETIC_CODE = "58474-8";
        public const string DIETARY_SUPPLEMENT_CODE = "58476-3";
        public const string MEDICAL_FOOD_CODE = "58475-5";
        public const string HUMAN_COMPOUNDED_DRUG_LABEL_CODE = "75031-5";
        public const string LICENSED_VACCINE_BULK_INTERMEDIATE_LABEL_CODE = "53406-5";
        public const string DRUG_FOR_FURTHER_PROCESSING_CODE = "78744-0";
        public const string ANIMAL_COMPOUNDED_DRUG_LABEL_CODE = "77647-6";
        public const string ANIMAL_CELLS_TISSUES_AND_CELL_AND_TISSUE_BASED_PRODUCT_LABEL_CODE = "98075-5";

        public const string AEROSOL_METERED_CODE = "C42960";
        public const string GEL_METERED_CODE = "C60930";
        public const string POWDER_METERED_CODE = "C42961";
        public const string SPRAY_METERED_CODE = "C42962";
        public const string TABLET_WITH_SENSOR_CODE = "C147579";

        public const string COMBINATION_PRODUCT_TYPE_0_NOT_A_COMBINATION_PRODUCT_CODE = "C112160";
        public const string COMBINATION_PRODUCT_TYPE_1_CONVENIENCE_KIT_CODE = "C102834";
        public const string COMBINATION_PRODUCT_TYPE_2_PREFILLED_DRUG_DELIVERY_DEVICE_CODE = "C102835";
        public const string COMBINATION_PRODUCT_TYPE_3_PREFILLED_BIOLOGIC_DELIVERY_DEVICE_CODE = "C102836";
        public const string COMBINATION_PRODUCT_TYPE_4_DEVICE_COATED_WITH_DRUG_CODE = "C102837";
        public const string COMBINATION_PRODUCT_TYPE_9_OTHER_CODE = "C102842";

        public const string INHALER_CODE = "C16738";
        public const string SYRINGE_CODE = "C43202";
        public const string SYRINGE_GLASS_CODE = "C43203";
        public const string SYRINGE_PLASTIC_CODE = "C43204";

        #endregion

        #region compliance codes
        // Compliance Action Codes
        public const string INACTIVATED_CODE = "C162847";
        public const string REACTIVATED_CODE = "C162848"; 
        #endregion

        #region drug interaction codes
        // Drug Interaction Codes
        public const string INTERACTION_CODE = "C54708";
        public const string PHARMACOKINETIC_EFFECT_CODE = "C54386";
        public const string MEDICAL_PROBLEM_CODE = "44100-6"; 
        #endregion

        #region rems codes
        // REMS Codes
        public const string REMS_APPROVAL_CODE = "C128899";
        public const string REMS_SUMMARY_SECTION_CODE = "82347-6";
        public const string REMS_ETASU_SECTION_CODE = "82345-0";
        public const string REMS_PARTICIPANT_REQUIREMENTS_SECTION_CODE = "87525-2"; 
        #endregion

        #region code systems
        // Code systems
        public const string LOINC_CODE_SYSTEM = "2.16.840.1.113883.6.1";
        public const string SNOMED_CT_CODE_SYSTEM = "2.16.840.1.113883.6.96";
        public const string CONFIDENTIALITY_CODE_SYSTEM = "2.16.840.1.113883.5.25";
        public const string FDA_UNII_CODE_SYSTEM = "2.16.840.1.113883.4.9";
        public const string FDA_SPL_CODE_SYSTEM = "2.16.840.1.113883.3.26.1.1";
        public const string FDA_PROD_CLASSIFICATION_CODE_SYSTEM = "2.16.840.1.113883.6.303";
        public const string NHRIC_CODE_SYSTEM = "2.16.840.1.113883.6.69";
        public const string HIBCC_CODE_SYSTEM = "2.16.840.1.113883.6.40";
        public const string DUNS_CODE_SYSTEM = "1.3.6.1.4.1.519.1";
        public const string GS1_CODE_SYSTEM = "1.3.160";
        public const string ISBT_128_CODE_SYSTEM = "2.16.840.1.113883.6.18";
        public const string FDA_FEI_CODE_SYSTEM = "2.16.840.1.113883.4.82";
        public const string FDA_APP_TRACKING_CODE_SYSTEM = "2.16.840.1.113883.3.150";
        public const string SPL_CHARACTERISTICS_CODE_SYSTEM = "2.16.840.1.113883.1.11.19255";
        public const string COSMETIC_PRODUCT_LISTING_CODE_SYSTEM = "2.16.840.1.113883.3.9848";
        public const string ISO_3166_1_CODE_SYSTEM = "1.0.3166.1.2.3";
        public const string ISO_3166_2_CODE_SYSTEM = "1.0.3166.2";
        public const string OTC_MONOGRAPH_ID_CODE_SYSTEM = "2.16.840.1.113883.3.9421";
        public const string MED_RT_CODE_SYSTEM = "2.16.840.1.113883.6.345";
        public const string MESH_CODE_SYSTEM = "2.16.840.1.113883.6.177";
        public const string NCPDP_BILLING_UNIT_CODE_SYSTEM = "2.16.840.1.113883.2.13";
        public const string PRODUCT_CONCEPT_CODE_SYSTEM = "2.16.840.1.113883.3.3389";
        public const string PRODUCT_CONCEPT_EQUIVALENCE_CODE_SYSTEM = "2.16.840.1.113883.3.2964";
        public const string MANUFACTURER_LICENSE_NUMBER_CODE_SYSTEM = "1.3.6.1.4.1.32366.1.3.1.2";
        public const string DEA_LICENSE_CODE_SYSTEM = "1.3.6.1.4.1.32366.4.840.1";
        public const string EPA_DOCUMENT_TYPE_CODE_SYSTEM = "2.16.840.1.113883.6.275.1";
        public const string EPA_SRS_TRACKING_CODE_SYSTEM = "2.16.840.1.113883.6.275";
        public const string CFR_CODE_SYSTEM = "2.16.840.1.113883.3.149";
        public const string SPECIFIED_SUBSTANCE_CODE_SYSTEM = "2.16.840.1.113883.3.6277";
        public const string NCT_NUMBER_CODE_SYSTEM = "2.16.840.1.113883.3.1077"; 
        #endregion

        #region marketing codes
        // Marketing Category Codes
        public const string ANADA_CODE = "C73583";
        public const string ANDA_CODE = "C73584";
        public const string APPROVED_DRUG_PRODUCT_MANUFACTURED_UNDER_CONTRACT_CODE = "C132333";
        public const string BLA_CODE = "C73585";
        public const string BULK_INGREDIENT_FOR_ANIMAL_DRUG_COMPOUNDING_CODE = "C98252";
        public const string BULK_INGREDIENT_FOR_HUMAN_PRESCRIPTION_COMPOUNDING_CODE = "C96793";
        public const string CONDITIONAL_NADA_CODE = "C73588";
        public const string EMERGENCY_USE_AUTHORIZATION_CODE = "C96966";
        public const string EXEMPT_DEVICE_CODE = "C80438";
        public const string EXPORT_ONLY_CODE = "C73590";
        public const string HUMANITARIAN_DEVICE_EXEMPTION_CODE = "C80440";
        public const string IND_CODE = "C75302";
        public const string LEGALLY_MARKETED_UNAPPROVED_NEW_ANIMAL_DRUGS_FOR_MINOR_SPECIES_CODE = "C92556";
        public const string MULTI_MARKET_APPROVED_PRODUCT_CODE = "C175238";
        public const string NADA_CODE = "C73593";
        public const string NDA_CODE = "C73594";
        public const string NDA_AUTHORIZED_GENERIC_CODE = "C73605";
        public const string OTC_MONOGRAPH_DRUG_CODE = "C200263";
        public const string OTC_MONOGRAPH_DRUG_PRODUCT_MANUFACTURED_UNDER_CONTRACT_CODE = "C132334";
        public const string OUTSOURCING_FACILITY_COMPOUNDED_HUMAN_DRUG_PRODUCT_EXEMPT_FROM_APPROVAL_REQUIREMENTS_CODE = "C181659";
        public const string OUTSOURCING_FACILITY_COMPOUNDED_HUMAN_DRUG_PRODUCT_NOT_MARKETED_NOT_DISTRIBUTED_CODE = "C190698";
        public const string PREMARKET_APPLICATION_CODE = "C80441";
        public const string PREMARKET_NOTIFICATION_CODE = "C80442";
        public const string SIP_APPROVED_DRUG_CODE = "C175462";
        public const string UNAPPROVED_DRUG_FOR_USE_IN_DRUG_SHORTAGE_CODE = "C101533";
        public const string UNAPPROVED_DRUG_OTHER_CODE = "C73627";
        public const string UNAPPROVED_DRUG_PRODUCT_MANUFACTURED_UNDER_CONTRACT_CODE = "C132335";
        public const string UNAPPROVED_HOMEOPATHIC_CODE = "C73614";
        public const string UNAPPROVED_MEDICAL_GAS_CODE = "C73613"; 
        #endregion

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
            public const string Translation = "translation";

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
