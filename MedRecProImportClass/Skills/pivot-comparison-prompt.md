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
