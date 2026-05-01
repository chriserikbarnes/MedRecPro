---
name: table-parser-data-dictionary
description: >
  Standardized data dictionary for the tmp_FlattenedStandardizedTable schema (38
  data columns, fixed width) and the downstream tmp_FlattenedAdverseEventTable
  (Stage 5 AE denormalization). Defines strict per-TableCategory contracts for
  every parser column — same column name, different documented meaning depending
  on the table type being parsed (AdverseEvent, PK, DrugInteraction, Efficacy) —
  plus the calculation contract (RR, DNRR, 95% CI, log companions) for the AE
  denormalized table. Use this skill when building or extending the table parser,
  writing ParseRule patterns, normalizing values, classifying TableCategory,
  triaging DoseRegimen content, defining PrimaryValueType enums, writing cross-
  table comparison queries, training ML.NET models, or implementing the Phase 2
  Stage 5 AE denormalization service. If the task involves SPL table parsing or
  AE risk-statistic computation in any capacity, read this skill first.
---

# Table Parser Data Dictionary

## Design Principle

**Fixed columns, strict context-dependent definitions.**

The schema is 38 data columns wide and stays that way. Columns are not widened to
accommodate edge cases — instead, each column's legal values and semantic role are
locked to the TableCategory of the row. A column that is NULL for one table type
may be mandatory for another. A column that means "MedDRA SOC" in AdverseEvent
tables means nothing in PK tables.

This produces a skinny, JOIN-friendly table where cross-table comparison is a
WHERE clause — not a reinterpretation exercise.

---

## Reference Files

| File                               | Read When                                        |
|------------------------------------|--------------------------------------------------|
| `references/column-contracts.md`   | Building parser logic for any column; writing SQL queries; validating output. Also contains the contract for `tmp_FlattenedAdverseEventTable` (Stage 5). |
| `references/normalization-rules.md`| Cleaning dirty columns: DoseRegimen triage, PrimaryValueType enum, Unit scrub, ParameterCategory SOC mapping, ParameterName cleanup, TreatmentArm cleanup. Also contains §7 (Stage 5 AE denormalization formulas: comparator pairing, Katz RR + Haldane correction, log-linear DNRR, IsPlaceboControlled trial-design classification). |
| `references/table-types.md`        | Classifying tables; understanding per-type column expectations; writing comparison keys |

---

## Schema at a Glance (38 columns, 5 groups)

### Provenance (8) — where did this value come from?

```
DocumentGUID · LabelerName · ProductTitle · VersionNumber
TextTableID · Caption · SourceRowSeq · SourceCellSeq
```

### Classification (4) — what kind of table is this?

```
TableCategory · ParentSectionCode · ParentSectionTitle · SectionTitle
```

### Observation Context (11) — what was measured, in whom, under what conditions?

```
ParameterName · ParameterCategory · ParameterSubtype
TreatmentArm · ArmN · StudyContext · DoseRegimen
Population · Timepoint · Time · TimeUnit
```

### Decomposed Values (10) — the numbers

```
RawValue · PrimaryValue · PrimaryValueType
SecondaryValue · SecondaryValueType
LowerBound · UpperBound · BoundType
PValue · Unit
```

### Validation (5) — how confident is the parser?

```
ParseConfidence · ParseRule · FootnoteMarkers · FootnoteText · ValidationFlags
```

---

## The Three Dirty Columns

Three columns need normalization — not schema changes — to reach distinct state.
Full rules in `references/normalization-rules.md`.

### DoseRegimen (still holding 4 content types after Population/Timepoint split)

```
CONTENT CURRENTLY IN DOSEREGIMEN        BELONGS?    ROUTE TO
────────────────────────────────────    ────────    ──────────────────
Actual dose (738 rows)                  ✓ YES       Keep
Co-admin drug names (760 rows)          ✗ NO        → ParameterSubtype
PK sub-parameters (115 rows)            ✗ NO        → ParameterSubtype
Residual population/timepoint (192)     ✗ NO        → Population / Timepoint
```

### PrimaryValueType (enum mixes statistic type with data format)

The old enum has 10 values that conflate three concepts. The tightened enum has
15 values that each answer one question: "What does this number represent?"

```
OLD → NEW MAPPING (key changes)
"Mean"                    → ArithmeticMean / GeometricMean / GeometricMeanRatio
"Percentage"              → Percentage  (Unit carries the "%" format)
"RelativeRiskReduction"   → HazardRatio / OddsRatio / RelativeRisk
"MeanPercentChange"       → PercentChange
"Numeric"                 → [context-resolved or left as Numeric + flag]
```

### Unit (~15% leaked column headers instead of real units)

```
Real units (≤15 chars):     4,888 rows  → Keep
Leaked headers (>30 chars):   923 rows  → NULL + flag UNIT_HEADER_LEAK
Ambiguous (16–30 chars):      173 rows  → Extract real unit or NULL
```

---

## Context-Dependent Column Roles (Summary)

The full contracts are in `references/column-contracts.md`. Here is the key
column whose meaning shifts most by TableCategory:

### ParameterSubtype — the flexible qualifier

```
TABLECATEGORY        PARAMETERSUBTYPE MEANS             EXAMPLES
───────────────────  ──────────────────────────────     ────────────────────────
AdverseEvent         Severity or causality qualifier    serious, non_serious,
                                                        leading_to_discontinuation
PK                   PK statistic qualifier             CV(%), steady_state,
                                                        single_dose, AUC(0-inf)
DrugInteraction      Co-administered drug name          Rifampin, Efavirenz,
                                                        Fluconazole, Midazolam
Efficacy             Analysis population                ITT, mITT, Per Protocol
```

This is the column that absorbs what would otherwise require schema widening.
Its meaning is strictly defined by the TableCategory value on the same row.

---

## Cross-Table Comparison Keys

```
TABLE CATEGORY        JOIN KEY (columns that must match)
────────────────────  ──────────────────────────────────────────────────
AdverseEvent          ParameterName + TreatmentArm + DoseRegimen
PK                    ParameterName + DoseRegimen + Population + Timepoint
                      + PrimaryValueType + Unit
DrugInteraction       ParameterName + ParameterSubtype + TreatmentArm
Efficacy              ParameterName + TreatmentArm + PrimaryValueType
```

Note that for DrugInteraction, ParameterSubtype (= co-administered drug) is part
of the comparison key. This is what makes DDI queries work without a dedicated
CoAdministeredDrug column.

---

## Stage 5: tmp_FlattenedAdverseEventTable (AE Denormalization)

Downstream of the parser pipeline, Stage 5 produces a denormalized AE-only
projection where each row already carries pre-computed Relative Risk (RR),
Dose-Normalized RR (DNRR), 95% CI bounds, and PERSISTED log-scale companions —
so visualizations bind without runtime statistics.

```
SCHEMA AT A GLANCE (31 columns)
─────────────────────────────────────────────────────────────────
Surrogate PK (1)         tmp_FlattenedAdverseEventTableID
Source projection (10)   tmp_FlattenedStandardizedTableID (FK) ·
                         DocumentGUID · UNII · ParameterName ·
                         ParameterCategory · ArmN · Dose ·
                         DoseUnit · PrimaryValue · PrimaryValueType
Comparator (4)           TreatmentArm · ComparatorArm · ComparatorN ·
                         IsPlaceboControlled
Event counts (2)         EventsTreatment · EventsComparator
Risk stats (6)           RR · DNRR · RRLowerBound · RRUpperBound ·
                         DNRRLowerBound · DNRRUpperBound
Log companions (6)       LogRR · LogRRLowerBound · LogRRUpperBound ·
[PERSISTED computed]     LogDNRR · LogDNRRLowerBound · LogDNRRUpperBound
Calculation provenance(2) CalculationMethod · CalculationFlags
```

**Key contracts** (full detail in `references/column-contracts.md` §AE Denormalization
and `references/normalization-rules.md` §7):

- **PrimaryValueType is copied verbatim from source** — never derived. Stats
  operate only on like-typed pairs (Percentage-vs-Percentage or Numeric-vs-Numeric).
- **`IsPlaceboControlled` is a Document-level trial-design flag**, not a row-level
  pairing artifact. Set to `1` only when document arms are exactly drug + placebo.
  Mixed placebo+active, active-only, stepped-dose, and single-arm trials all get `0`.
- **The `Log*` columns are SQL Server PERSISTED computed columns**, materialized
  on disk and auto-maintained. `CASE WHEN > 0 THEN LOG(...)` guards prevent
  `LOG(0)` / `LOG(NULL)` errors.
- **D_ref for DNRR is per-study-group**: `MIN(Dose) WHERE Dose > 0` over
  `(DocumentGUID, ParameterName, ParameterSubtype)`. The reference-dose row
  itself gets DNRR = NULL with flag `IS_REFERENCE_DOSE`.

**Status:** Phase 1 (DDL at `MedRecPro/SQL/MedRecPro-Table-tmp_FlattenedAdverseEventTable.sql`)
is shipped. Phase 2 (the population service `AdverseEventDenormalizationService`,
`RelativeRiskCalculator` utility, EF entity, DTO, orchestrator hook, DI registration)
is planned.
