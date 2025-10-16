
select * from [dbo].[DocumentAuthor]
select * from [dbo].[DocumentRelationship]
select * from [dbo].[DocumentRelationshipIdentifier]
select * from [dbo].[Organization]
select * from [dbo].[OrganizationIdentifier]

-- Check if business operations were created
SELECT * FROM [dbo].[BusinessOperation]

-- Check if product links were created
SELECT * FROM [dbo].[FacilityProductLink]

-- Verify the relationship structure
SELECT 
    dr.DocumentRelationshipID,
    dr.ParentOrganizationID,
    dr.ChildOrganizationID,
    dr.RelationshipType,
    dr.RelationshipLevel,
    COUNT(bo.BusinessOperationID) as BusinessOpCount
FROM [dbo].[DocumentRelationship] dr
LEFT JOIN [dbo].[BusinessOperation] bo ON dr.DocumentRelationshipID = bo.DocumentRelationshipID
GROUP BY dr.DocumentRelationshipID, dr.ParentOrganizationID, dr.ChildOrganizationID, dr.RelationshipType, dr.RelationshipLevel