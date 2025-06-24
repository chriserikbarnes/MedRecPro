namespace MedRecPro.Service.ParsingServices
{
    /// <summary>
    /// Provides centralized, constant definitions for XML element and attribute names
    /// used in SPL (Structured Product Labeling) files. Using this class prevents
    /// magic strings and reduces errors from typos.
    /// </summary>
    public static class SplConstants
    {
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
            public const string InactiveIngredient = "inactiveIngredient";
            public const string ActiveMoiety = "activeMoiety";
            public const string DefiningSubstance = "definingSubstance";
            public const string Quantity = "quantity";
            public const string Numerator = "numerator";
            public const string Denominator = "denominator";
            public const string SubstanceSpecification = "substanceSpecification";
            public const string AsContent = "asContent";
            public const string ContainerPackagedProduct = "containerPackagedProduct";
            public const string Approval = "approval";
            public const string MarketingAct = "marketingAct";
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
            #endregion
        }

        /// <summary>
        /// Defines the XML Attribute names used in SPL files.
        /// </summary>
        public static class A // Attribute Names
        {
            public const string Root = "root";
            public const string Extension = "extension";
            public const string CodeValue = "code"; // Attribute name is "code"
            public const string CodeSystem = "codeSystem";
            public const string DisplayName = "displayName";
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
}