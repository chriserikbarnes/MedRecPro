Use MedRecLocal

select a.*
, b.TextTableID
from [dbo].[tmp_FlattenedAdverseEventRiskTable] a
inner join [dbo].[tmp_FlattenedStandardizedTable] b
on a.tmp_FlattenedStandardizedTableID = b.tmp_FlattenedStandardizedTableID
where a.[Population] is not null
--and a.[Population] = 'Adult and'
order by SubstanceName

Use MedRecLocal

select distinct a.ProductName
from [dbo].[tmp_FlattenedAdverseEventRiskTable] a
inner join [dbo].[tmp_FlattenedStandardizedTable] b
on a.tmp_FlattenedStandardizedTableID = b.tmp_FlattenedStandardizedTableID
where a.[Population] is not null
--and a.[Population] = 'Adult and'
order by SubstanceName

select * from [dbo].[tmp_FlattenedStandardizedTable]
where DocumentGUID = '20dc2455-4d48-4183-b1ad-29a2686dcd2a'


select * from [dbo].[tmp_FlattenedStandardizedTable]
where TextTableID not in
(select b.TextTableID
from [dbo].[tmp_FlattenedAdverseEventRiskTable] a
inner join [dbo].[tmp_FlattenedStandardizedTable] b
on a.tmp_FlattenedStandardizedTableID = b.tmp_FlattenedStandardizedTableID)
and ArmN > 0 and PrimaryValue is not null

select * from [dbo].[tmp_FlattenedStandardizedTable]
where TextTableID not in
(select b.TextTableID
from [dbo].[tmp_FlattenedAdverseEventRiskTable] a
inner join [dbo].[tmp_FlattenedStandardizedTable] b
on a.tmp_FlattenedStandardizedTableID = b.tmp_FlattenedStandardizedTableID)
and ArmN > 0 and PrimaryValue is not null and TableCategory <> 'PK'
and TableCategory <> 'EFFICACY'

select distinct TextTableID, ParentSectionCode from [dbo].[tmp_FlattenedStandardizedTable]
where TextTableID not in
(select b.TextTableID
from [dbo].[tmp_FlattenedAdverseEventRiskTable] a
inner join [dbo].[tmp_FlattenedStandardizedTable] b
on a.tmp_FlattenedStandardizedTableID = b.tmp_FlattenedStandardizedTableID)
and ArmN > 0
and PrimaryValue is not null
and TableCategory <> 'PK'
and ParentSectionCode is not null
and TableCategory <> 'EFFICACY'


SELECT
    (
        SELECT t.TextTableID, t.ParentSectionCode
        FOR JSON PATH, WITHOUT_ARRAY_WRAPPER
    ) AS JsonLine
FROM
(
    SELECT DISTINCT TextTableID, ParentSectionCode
    FROM [dbo].[tmp_FlattenedStandardizedTable]
    WHERE TextTableID NOT IN
    (
        SELECT b.TextTableID
        FROM [dbo].[tmp_FlattenedAdverseEventRiskTable] a
        INNER JOIN [dbo].[tmp_FlattenedStandardizedTable] b
            ON a.tmp_FlattenedStandardizedTableID = b.tmp_FlattenedStandardizedTableID
    )
    AND ArmN > 0
    AND PrimaryValue IS NOT NULL
    AND TableCategory <> 'PK'
    AND ParentSectionCode IS NOT NULL
    AND TableCategory <> 'EFFICACY'
) AS t;