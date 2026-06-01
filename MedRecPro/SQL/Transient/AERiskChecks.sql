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