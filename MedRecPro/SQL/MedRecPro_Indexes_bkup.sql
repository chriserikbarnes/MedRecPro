CREATE NONCLUSTERED INDEX [IX_ActiveMoiety_IngredientSubstanceID] ON [dbo].[ActiveMoiety] ([IngredientSubstanceID] ASC);

CREATE NONCLUSTERED INDEX [IX_ActiveMoiety_MoietyUNII] ON [dbo].[ActiveMoiety] ([MoietyUNII] ASC);

CREATE NONCLUSTERED INDEX [IX_AdditionalIdentifier_ProductID] ON [dbo].[AdditionalIdentifier] ([ProductID] ASC);

CREATE NONCLUSTERED INDEX [IX_AspNetRoleClaims_RoleId] ON [dbo].[AspNetRoleClaims] ([RoleId] ASC);

CREATE UNIQUE NONCLUSTERED INDEX [RoleNameIndex] ON [dbo].[AspNetRoles] ([NormalizedName] ASC);

CREATE NONCLUSTERED INDEX [IX_ActivityLog_ActivityType] ON [dbo].[AspNetUserActivityLog] ([ActivityType] ASC);

CREATE NONCLUSTERED INDEX [IX_ActivityLog_Controller_Action] ON [dbo].[AspNetUserActivityLog] (
	[ControllerName] ASC
	,[ActionName] ASC
	);

CREATE NONCLUSTERED INDEX [IX_ActivityLog_ExecutionTime] ON [dbo].[AspNetUserActivityLog] ([ExecutionTimeMs] ASC);

CREATE NONCLUSTERED INDEX [IX_ActivityLog_ResponseStatus] ON [dbo].[AspNetUserActivityLog] ([ResponseStatusCode] ASC);

CREATE NONCLUSTERED INDEX [IX_ActivityLog_Timestamp] ON [dbo].[AspNetUserActivityLog] ([ActivityTimestamp] ASC);

CREATE NONCLUSTERED INDEX [IX_ActivityLog_UserId] ON [dbo].[AspNetUserActivityLog] ([UserId] ASC);

CREATE NONCLUSTERED INDEX [IX_AspNetUserClaims_UserId] ON [dbo].[AspNetUserClaims] ([UserId] ASC);

CREATE NONCLUSTERED INDEX [IX_AspNetUserLogins_UserId] ON [dbo].[AspNetUserLogins] ([UserId] ASC);

CREATE NONCLUSTERED INDEX [IX_AspNetUserRoles_RoleId] ON [dbo].[AspNetUserRoles] ([RoleId] ASC);

CREATE NONCLUSTERED INDEX [EmailIndex] ON [dbo].[AspNetUsers] ([NormalizedEmail] ASC);

CREATE NONCLUSTERED INDEX [IX_Users_LastActivityAt] ON [dbo].[AspNetUsers] ([LastActivityAt] ASC);

CREATE UNIQUE NONCLUSTERED INDEX [UserNameIndex] ON [dbo].[AspNetUsers] ([NormalizedUserName] ASC);

CREATE UNIQUE NONCLUSTERED INDEX [UX_Users_CanonicalUsername_Active] ON [dbo].[AspNetUsers] ([CanonicalUsername] ASC);

CREATE UNIQUE NONCLUSTERED INDEX [UX_Users_PrimaryEmail_Active] ON [dbo].[AspNetUsers] ([PrimaryEmail] ASC);

CREATE NONCLUSTERED INDEX [IX_BusinessOperation_DocumentRelationshipID] ON [dbo].[BusinessOperation] ([DocumentRelationshipID] ASC);

CREATE NONCLUSTERED INDEX [IX_BusinessOperationQualifier_BusinessOperationID] ON [dbo].[BusinessOperationQualifier] ([BusinessOperationID] ASC);

CREATE NONCLUSTERED INDEX [IX_CertificationProductLink_DocumentRelationshipID] ON [dbo].[CertificationProductLink] ([DocumentRelationshipID] ASC);

CREATE NONCLUSTERED INDEX [IX_ComplianceAction_DocumentRelationshipID] ON [dbo].[ComplianceAction] ([DocumentRelationshipID] ASC);

CREATE NONCLUSTERED INDEX [IX_ContactParty_AddressID] ON [dbo].[ContactParty] ([AddressID] ASC);

CREATE NONCLUSTERED INDEX [IX_ContactParty_OrganizationID] ON [dbo].[ContactParty] ([OrganizationID] ASC);

CREATE NONCLUSTERED INDEX [IX_ContactPartyTelecom_ContactPartyID] ON [dbo].[ContactPartyTelecom] ([ContactPartyID] ASC);

CREATE NONCLUSTERED INDEX [IX_Document_DocumentCode_EffectiveTime] ON [dbo].[Document] (
	[DocumentCode] ASC
	,[EffectiveTime] DESC
	);

CREATE NONCLUSTERED INDEX [IX_Document_DocumentGUID] ON [dbo].[Document] ([DocumentGUID] ASC);

CREATE NONCLUSTERED INDEX [IX_Document_DocumentGUID_Ingredient] ON [dbo].[Document] ([DocumentGUID] ASC);

CREATE NONCLUSTERED INDEX [IX_Document_DocumentGUID_SectionContent] ON [dbo].[Document] ([DocumentGUID] ASC);

CREATE NONCLUSTERED INDEX [IX_Document_SetGUID] ON [dbo].[Document] ([SetGUID] ASC);

CREATE NONCLUSTERED INDEX [IX_Document_SetGUID_Ingredient] ON [dbo].[Document] ([SetGUID] ASC);

CREATE NONCLUSTERED INDEX [IX_DocumentAuthor_DocumentID] ON [dbo].[DocumentAuthor] ([DocumentID] ASC);

CREATE NONCLUSTERED INDEX [IX_DocumentAuthor_OrganizationID] ON [dbo].[DocumentAuthor] ([OrganizationID] ASC);

CREATE NONCLUSTERED INDEX [IX_DocumentRelationship_ChildOrganizationID] ON [dbo].[DocumentRelationship] ([ChildOrganizationID] ASC);

CREATE NONCLUSTERED INDEX [IX_DocumentRelationship_DocumentID] ON [dbo].[DocumentRelationship] ([DocumentID] ASC);

CREATE NONCLUSTERED INDEX [IX_DocumentRelationship_ParentOrganizationID] ON [dbo].[DocumentRelationship] ([ParentOrganizationID] ASC);

CREATE NONCLUSTERED INDEX [IX_DocumentRelationshipIdentifier_DocumentRelationshipID] ON [dbo].[DocumentRelationshipIdentifier] ([DocumentRelationshipID] ASC);

CREATE NONCLUSTERED INDEX [IX_DocumentRelationshipIdentifier_DocumentRelationshipID_OrganizationIdentifierID] ON [dbo].[DocumentRelationshipIdentifier] ([DocumentRelationshipID] ASC);

CREATE NONCLUSTERED INDEX [IX_DocumentRelationshipIdentifier_OrganizationIdentifierID] ON [dbo].[DocumentRelationshipIdentifier] ([OrganizationIdentifierID] ASC);

CREATE NONCLUSTERED INDEX [IX_DocumentRelationshipIdentifier_OrganizationIdentifierID_DocumentRelationshipID] ON [dbo].[DocumentRelationshipIdentifier] ([OrganizationIdentifierID] ASC);

CREATE UNIQUE NONCLUSTERED INDEX [UX_DocumentRelationshipIdentifier_Unique] ON [dbo].[DocumentRelationshipIdentifier] (
	[DocumentRelationshipID] ASC
	,[OrganizationIdentifierID] ASC
	);

CREATE NONCLUSTERED INDEX [IX_EquivalentEntity_ProductID] ON [dbo].[EquivalentEntity] ([ProductID] ASC);

CREATE NONCLUSTERED INDEX [IX_FacilityProductLink_DocumentRelationshipID] ON [dbo].[FacilityProductLink] ([DocumentRelationshipID] ASC);

CREATE NONCLUSTERED INDEX [IX_FacilityProductLink_ProductID] ON [dbo].[FacilityProductLink] ([ProductID] ASC);

CREATE NONCLUSTERED INDEX [IX_GenericMedicine_ProductID] ON [dbo].[GenericMedicine] ([ProductID] ASC);

CREATE NONCLUSTERED INDEX [IX_Ingredient_ClassCode] ON [dbo].[Ingredient] ([ClassCode] ASC);

CREATE NONCLUSTERED INDEX [IX_Ingredient_IngredientSubstanceID] ON [dbo].[Ingredient] ([IngredientSubstanceID] ASC);

CREATE NONCLUSTERED INDEX [IX_Ingredient_ProductID] ON [dbo].[Ingredient] ([ProductID] ASC);

CREATE NONCLUSTERED INDEX [IX_IngredientSubstance_SubstanceName] ON [dbo].[IngredientSubstance] ([SubstanceName] ASC);

CREATE NONCLUSTERED INDEX [IX_IngredientSubstance_UNII] ON [dbo].[IngredientSubstance] ([UNII] ASC);

CREATE NONCLUSTERED INDEX [IX_LegalAuthenticator_DocumentID] ON [dbo].[LegalAuthenticator] ([DocumentID] ASC);

CREATE NONCLUSTERED INDEX [IX_MarketingCategory_ApplicationOrMonographIDValue] ON [dbo].[MarketingCategory] ([ApplicationOrMonographIDValue] ASC);

CREATE NONCLUSTERED INDEX [IX_NCTLink_NCTNumber] ON [dbo].[NCTLink] ([NCTNumber] ASC);

CREATE NONCLUSTERED INDEX [IX_NCTLink_SectionID] ON [dbo].[NCTLink] ([SectionID] ASC);

CREATE NONCLUSTERED INDEX [IX_ObservationMedia_SectionID] ON [dbo].[ObservationMedia] ([SectionID] ASC);

CREATE NONCLUSTERED INDEX [IX_Organization_OrganizationName] ON [dbo].[Organization] ([OrganizationName] ASC);

CREATE NONCLUSTERED INDEX [IX_OrganizationIdentifier_IdentifierValue_on_IdentifierType] ON [dbo].[OrganizationIdentifier] (
	[IdentifierValue] ASC
	,[IdentifierType] ASC
	);

CREATE NONCLUSTERED INDEX [IX_OrganizationIdentifier_OrganizationID] ON [dbo].[OrganizationIdentifier] ([OrganizationID] ASC);

CREATE NONCLUSTERED INDEX [IX_OrganizationTelecom_OrganizationID] ON [dbo].[OrganizationTelecom] ([OrganizationID] ASC);

CREATE NONCLUSTERED INDEX [IX_PackageIdentifier_PackagingLevelID] ON [dbo].[PackageIdentifier] ([PackagingLevelID] ASC);

CREATE NONCLUSTERED INDEX [IX_PackagingLevel_PackageCode] ON [dbo].[PackagingLevel] ([PackageCode] ASC);

CREATE NONCLUSTERED INDEX [IX_PackagingLevel_ProductID] ON [dbo].[PackagingLevel] ([ProductID] ASC);

CREATE NONCLUSTERED INDEX [IX_PackagingLevel_ProductInstanceID] ON [dbo].[PackagingLevel] ([ProductInstanceID] ASC);

CREATE NONCLUSTERED INDEX [IX_Product_ProductName] ON [dbo].[Product] ([ProductName] ASC);

CREATE NONCLUSTERED INDEX [IX_Product_SectionID] ON [dbo].[Product] ([SectionID] ASC);

CREATE NONCLUSTERED INDEX [IX_ProductIdentifier_IdentifierValue_on_IdentifierType] ON [dbo].[ProductIdentifier] (
	[IdentifierValue] ASC
	,[IdentifierType] ASC
	);

CREATE NONCLUSTERED INDEX [IX_ProductIdentifier_ProductID] ON [dbo].[ProductIdentifier] ([ProductID] ASC);

CREATE NONCLUSTERED INDEX [IX_RelatedDocument_ReferencedDocumentGUID] ON [dbo].[RelatedDocument] ([ReferencedDocumentGUID] ASC);

CREATE NONCLUSTERED INDEX [IX_RelatedDocument_ReferencedSetGUID] ON [dbo].[RelatedDocument] ([ReferencedSetGUID] ASC);

CREATE NONCLUSTERED INDEX [IX_RelatedDocument_SourceDocumentID] ON [dbo].[RelatedDocument] ([SourceDocumentID] ASC);

CREATE NONCLUSTERED INDEX [IX_RenderedMedia_DocumentID] ON [dbo].[RenderedMedia] ([DocumentID] ASC);

CREATE NONCLUSTERED INDEX [IX_RenderedMedia_ObservationMediaID] ON [dbo].[RenderedMedia] ([ObservationMediaID] ASC);

CREATE NONCLUSTERED INDEX [IX_RenderedMedia_SectionTextContentID] ON [dbo].[RenderedMedia] ([SectionTextContentID] ASC);

CREATE NONCLUSTERED INDEX [IX_ResponsiblePersonLink_ProductID] ON [dbo].[ResponsiblePersonLink] ([ProductID] ASC);

CREATE NONCLUSTERED INDEX [IX_ResponsiblePersonLink_ResponsiblePersonOrgID] ON [dbo].[ResponsiblePersonLink] ([ResponsiblePersonOrgID] ASC);

CREATE NONCLUSTERED INDEX [IX_Section_DocumentID] ON [dbo].[Section] ([DocumentID] ASC);

CREATE NONCLUSTERED INDEX [IX_Section_DocumentID_SectionContent] ON [dbo].[Section] ([DocumentID] ASC);

CREATE NONCLUSTERED INDEX [IX_Section_SectionCode] ON [dbo].[Section] ([SectionCode] ASC);

CREATE NONCLUSTERED INDEX [IX_Section_SectionCode_on_DocumentID] ON [dbo].[Section] (
	[SectionCode] ASC
	,[DocumentID] ASC
	);

CREATE NONCLUSTERED INDEX [IX_Section_SectionGUID] ON [dbo].[Section] ([SectionGUID] ASC);

CREATE NONCLUSTERED INDEX [IX_Section_SectionGUID_Ingredient] ON [dbo].[Section] ([SectionGUID] ASC);

CREATE NONCLUSTERED INDEX [IX_Section_StructuredBodyID] ON [dbo].[Section] ([StructuredBodyID] ASC);

CREATE NONCLUSTERED INDEX [IX_SectionExcerptHighlight_SectionID] ON [dbo].[SectionExcerptHighlight] ([SectionID] ASC);

CREATE NONCLUSTERED INDEX [IX_SectionHierarchy_ChildSectionID] ON [dbo].[SectionHierarchy] ([ChildSectionID] ASC);

CREATE NONCLUSTERED INDEX [IX_SectionHierarchy_ParentSectionID] ON [dbo].[SectionHierarchy] ([ParentSectionID] ASC);

CREATE NONCLUSTERED INDEX [IX_SectionTextContent_ContentType_on_SectionID] ON [dbo].[SectionTextContent] (
	[ContentType] ASC
	,[SectionID] ASC
	);

CREATE NONCLUSTERED INDEX [IX_SectionTextContent_ParentSectionTextContentID] ON [dbo].[SectionTextContent] ([ParentSectionTextContentID] ASC);

CREATE NONCLUSTERED INDEX [IX_SectionTextContent_SectionID] ON [dbo].[SectionTextContent] ([SectionID] ASC);

CREATE NONCLUSTERED INDEX [IX_SectionTextContent_SectionID_SectionContent] ON [dbo].[SectionTextContent] (
	[SectionID] ASC
	,[SequenceNumber] ASC
	);

CREATE NONCLUSTERED INDEX [IX_SpecializedKind_ProductID] ON [dbo].[SpecializedKind] ([ProductID] ASC);

CREATE NONCLUSTERED INDEX [IX_StructuredBody_DocumentID] ON [dbo].[StructuredBody] ([DocumentID] ASC);

CREATE NONCLUSTERED INDEX [IX_TextList_SectionTextContentID] ON [dbo].[TextList] ([SectionTextContentID] ASC);

CREATE NONCLUSTERED INDEX [IX_TextListItem_TextListID_SequenceNumber] ON [dbo].[TextListItem] (
	[TextListID] ASC
	,[SequenceNumber] ASC
	);

CREATE NONCLUSTERED INDEX [IX_TextTable_SectionTextContentID] ON [dbo].[TextTable] ([SectionTextContentID] ASC);

CREATE NONCLUSTERED INDEX [IX_TextTableCell_TextTableRowID_SequenceNumber] ON [dbo].[TextTableCell] (
	[TextTableRowID] ASC
	,[SequenceNumber] ASC
	);

CREATE NONCLUSTERED INDEX [IX_TextTableColumn_ColGroupSequenceNumber] ON [dbo].[TextTableColumn] (
	[TextTableID] ASC
	,[ColGroupSequenceNumber] ASC
	);

CREATE NONCLUSTERED INDEX [IX_TextTableColumn_TextTableID_SequenceNumber] ON [dbo].[TextTableColumn] (
	[TextTableID] ASC
	,[SequenceNumber] ASC
	);

CREATE NONCLUSTERED INDEX [IX_TextTableRow_TextTableID_SequenceNumber] ON [dbo].[TextTableRow] (
	[TextTableID] ASC
	,[SequenceNumber] ASC
	);