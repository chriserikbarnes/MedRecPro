---
name: pivot-comparison-context
description: >
  Instructions for Claude on how to use the original pivot table context when
  reviewing parsed observations. Appended to the user message when an original
  table is available for Stage 3.5 comparison.
---

## Original Table Context
Below is the original reconstructed table (pre-parsing). Use it to verify:
- Column alignment: do observation values map to the correct treatment arm columns?
- Header interpretation: were multi-level headers resolved correctly?
- SOC dividers: are ParameterCategory values aligned with the table's grouping rows?
- Missing data: are empty cells correctly represented as NULL values?
- Caption context: does the caption provide clues about PrimaryValueType, Unit, or study design?
- Subpopulation context: are mid-body `(N=…)` partition rows (e.g. "Female Patients Only", "Male Patients Only") recognized as subpopulation context rather than data rows? Subsequent rows should carry the new Subpopulation label and the per-arm N from the partition row, not the whole-study N.
## Column-Header Inventory
Before proposing corrections, enumerate the source table headers mentally and infer each value column's type/unit from its header path.

- Treat a header containing `%` as authoritative percent-column context: numeric cells under that column should be `PrimaryValueType=Percentage` and `Unit="%"`.
- Do not demote a percent-column cell from `Percentage` to `Count`.
- Do not copy a column header token into `TreatmentArm`, `ParameterName`, or `Unit`.
- If one numeric cell in a percent column needs correction, inspect the sibling cells in that same source column for the same correction.
