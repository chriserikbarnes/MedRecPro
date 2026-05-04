# Column Contracts

Each column has a strict definition that depends on the TableCategory of the row.
This file is the single source of truth for "what goes where."

Organization: Provenance and Classification columns are context-free (same meaning
regardless of table type). Observation Context and Value columns are context-
dependent — their contracts are defined per TableCategory.

---

## Context-Free Columns

These columns mean the same thing in every row regardless of TableCategory.

### Provenance (8 columns)

| Column          | Type             | Content                                    |
|-----------------|------------------|--------------------------------------------|
| DocumentGUID    | UNIQUEIDENTIFIER | SPL document identity                      |
| LabelerName     | NVARCHAR(500)    | Manufacturer name                          |
| ProductTitle    | NVARCHAR(500)    | Drug product name as on label              |
| VersionNumber   | INT              | SPL version (higher = more recent)         |
| TextTableID     | INT              | Table identity within parse run            |
| Caption         | NVARCHAR(MAX)    | Table caption/title (parse signal + audit) |
| SourceRowSeq    | INT              | Row position in source HTML table          |
| SourceCellSeq   | INT              | Column position in source HTML table       |

### Classification (4 columns)

| Column             | Type           | Content                                   |
|--------------------|----------------|-------------------------------------------|
| TableCategory      | NVARCHAR(100)  | Table type enum (see table-types.md)      |
| ParentSectionCode  | NVARCHAR(50)   | LOINC section code from SPL structure     |
| ParentSectionTitle | NVARCHAR(1000) | Human-readable parent section name        |
| SectionTitle       | NVARCHAR(1000) | Human-readable sub-section name           |

### Validation (5 columns)

| Column          | Type           | Content                                    |
|-----------------|----------------|--------------------------------------------|
| ParseConfidence | FLOAT          | Parser certainty [0–1]. <0.7 → Tier 2     |
| ParseRule       | NVARCHAR(100)  | Which rule extracted the value              |
| FootnoteMarkers | NVARCHAR(500)  | Superscript refs stripped from RawValue     |
| FootnoteText    | NVARCHAR(MAX)  | Resolved footnote content                  |
| ValidationFlags | NVARCHAR(1000) | Post-parse QA flags (pipe-delimited)       |

### Value Extraction (always same extraction semantics)

| Column             | Type           | Content                                  |
|--------------------|----------------|------------------------------------------|
| RawValue           | NVARCHAR(2000) | Verbatim cell text. Never modify.        |
| PrimaryValue       | FLOAT          | Central numeric result from RawValue     |
| SecondaryValue     | FLOAT          | Spread or count measure from RawValue    |
| LowerBound         | FLOAT          | Low end of CI or range                   |
| UpperBound         | FLOAT          | High end of CI or range                  |
| PValue             | FLOAT          | Statistical p-value [0–1]                |

---

## Context-Dependent Columns by TableCategory

For each TableCategory, the tables below define: what each context column
contains, whether it's required/expected/optional/null, and legal values.

Legend: **R** = Required (flag if missing), **E** = Expected (usually populated),
**O** = Optional (populated when data stratifies on this), **N** = NULL (not
applicable for this table type).

---

### AdverseEvent

Tables reporting incidence/frequency of adverse events across treatment arms.

| Column             | Req | Contains                               | Legal Values / Examples           |
|--------------------|-----|----------------------------------------|-----------------------------------|
| ParameterName      | R   | MedDRA Preferred Term                  | Nausea, Dizziness, Rash           |
| ParameterCategory  | E   | Canonical MedDRA SOC                   | See SOC mapping in normalization  |
| ParameterSubtype   | O   | Severity or causality qualifier        | `serious`, `non_serious`, `leading_to_discontinuation`, `dose_related` |
| TreatmentArm       | R   | Drug name or Placebo                   | Decitabine, Placebo               |
| ArmN               | E   | Integer sample size for arm            | Any positive integer               |
| StudyContext        | O   | Study name (multi-study tables only)   | SPRING-2, SINGLE                   |
| DoseRegimen        | O   | Dose level when table stratifies       | 20 mg, 50 mg once daily           |
| Dose                | O   | Numeric dose value (0.0 for placebo)   | 20, 50, 0.0                       |
| DoseUnit            | O   | Normalized dose unit                   | mg, mg/d                           |
| Population          | O   | Caption-level whole-table population   | Adult Healthy Volunteers, Pediatric|
| Subpopulation       | O   | In-table partition (mid-body N=row)    | Female Patients Only, Male Patients Only |
| Timepoint           | N   | NULL — AE tables are cumulative        |                                    |
| Time / TimeUnit     | N   | NULL                                    |                                    |
| PrimaryValue       | R   | Incidence (%) or integer count         |                                    |
| PrimaryValueType   | R   | How the value was reported             | `Percentage`, `Count`              |
| SecondaryValue     | O   | Count (from n_pct: n=count, %=PV)     |                                    |
| SecondaryValueType | O   | What SecondaryValue is                 | `Count`                            |
| LowerBound/Upper   | O   | CI for incidence difference (rare)     |                                    |
| BoundType          | O   | CI level if bounds present             | `95CI`                             |
| PValue             | O   | Comparison p-value                     |                                    |
| Unit               | E   | `%` or NULL (when count only)          |                                    |

**Comparison key:** `ParameterName + TreatmentArm + DoseRegimen + StudyContext + Population + Subpopulation`
(StudyContext / Population / Subpopulation are normalized: trim, collapse whitespace,
ToUpperInvariant; null/empty/whitespace share one bucket.)

**Population vs. Subpopulation:** orthogonal grains. Population is caption-level
("Adult Healthy Volunteers" — applies to the whole table); Subpopulation is a
within-table partition introduced by mid-body `(N=…)` rows ("Female Patients
Only" — applies only to subsequent rows in that section). Both can be set on the
same row.

---

### PK

Tables reporting pharmacokinetic parameters.

| Column             | Req | Contains                               | Legal Values / Examples           |
|--------------------|-----|----------------------------------------|-----------------------------------|
| ParameterName      | R   | PK parameter name                      | Cmax, AUC0-24, t½, CL/F, Vss    |
| ParameterCategory  | N   | NULL                                    |                                    |
| ParameterSubtype   | O   | PK statistic qualifier OR condition    | `CV(%)`, `steady_state`, `single_dose`, `fasted`, `fed` |
| TreatmentArm       | O   | Drug name (often implicit from doc)    | Drug name                          |
| ArmN               | O   | N per dose group                       |                                    |
| StudyContext        | O   | Study name if multi-study              |                                    |
| DoseRegimen        | E   | Actual dose: amount + route + freq     | 50 mg oral once daily, 100 mg IV  |
| Dose                | E   | Numeric dose value                     | 50, 100, 0.5                       |
| DoseUnit            | E   | Normalized dose unit                   | mg, mg/d, mg/kg                    |
| Population          | O   | Subject characteristics when stratified| Healthy Volunteers, Renal Impairment |
| Subpopulation       | O   | In-table partition (rare for PK)       |                                    |
| Timepoint           | O   | When measured                          | Single Dose, Steady State, Day 14 |
| Time                | O   | Numeric timepoint                      | 14.0                               |
| TimeUnit            | O   | Unit for Time                          | days, hours                        |
| PrimaryValue       | R   | PK parameter value                     |                                    |
| PrimaryValueType   | R   | Statistic used to summarize            | `GeometricMean`, `ArithmeticMean`, `Median` |
| SecondaryValue     | E   | Dispersion measure                     |                                    |
| SecondaryValueType | E   | Which dispersion                       | `SD`, `CV`                         |
| LowerBound/Upper   | O   | CI bounds                              |                                    |
| BoundType          | O   | CI level                               | `90CI` (bioequivalence standard)   |
| PValue             | N   | NULL                                    |                                    |
| Unit               | R   | Concentration, time, clearance, volume | mcg/mL, ng·h/mL, h, L/kg, mL/min/kg |

**Comparison key:** `ParameterName + DoseRegimen + Population + Timepoint + PrimaryValueType + Unit`

---

### DrugInteraction

Tables showing effect of co-administered drugs on PK parameters (or vice versa).

| Column             | Req | Contains                               | Legal Values / Examples           |
|--------------------|-----|----------------------------------------|-----------------------------------|
| ParameterName      | R   | PK parameter name                      | Cmax, AUC, t½                     |
| ParameterCategory  | N   | NULL                                    |                                    |
| ParameterSubtype   | R   | **Co-administered drug name**          | Rifampin, Efavirenz, Fluconazole, Midazolam, Carbamazepine |
| TreatmentArm       | E   | Index drug (the drug being studied)    | Dolutegravir, Azithromycin         |
| ArmN               | O   | N per interaction study                |                                    |
| StudyContext        | O   | Study label                            |                                    |
| DoseRegimen        | E   | Dose of index drug                     | 50 mg once daily                   |
| Dose                | E   | Numeric dose value                     | 50                                 |
| DoseUnit            | E   | Normalized dose unit                   | mg/d                               |
| Population          | O   | Usually Healthy Volunteers (implicit)  |                                    |
| Subpopulation       | O   | In-table partition (rare for DDI)      |                                    |
| Timepoint           | N   | NULL                                    |                                    |
| Time / TimeUnit     | N   | NULL                                    |                                    |
| PrimaryValue       | R   | Geometric mean ratio (~1.0 = no effect)|                                    |
| PrimaryValueType   | R   | Always ratio-type                      | `GeometricMeanRatio`               |
| SecondaryValue     | N   | NULL (DDI reports CI, not SD)          |                                    |
| SecondaryValueType | N   | NULL                                    |                                    |
| LowerBound/Upper   | R   | 90% CI bounds                          |                                    |
| BoundType          | R   | Always 90% CI                          | `90CI`                             |
| PValue             | N   | NULL                                    |                                    |
| Unit               | O   | `ratio` or NULL (dimensionless)        |                                    |

**Comparison key:** `ParameterName + ParameterSubtype + TreatmentArm`

**Critical:** In DrugInteraction rows, ParameterSubtype IS the co-administered
drug name. This is the column's strict definition for this TableCategory.
The "no effect" boundary is 1.00. Standard bioequivalence window: (0.80, 1.25).

---

### Efficacy

Tables reporting comparative efficacy outcomes with risk measures and CIs.

| Column             | Req | Contains                               | Legal Values / Examples           |
|--------------------|-----|----------------------------------------|-----------------------------------|
| ParameterName      | R   | Clinical endpoint name                 | Overall Survival, ORR, PFS, CR    |
| ParameterCategory  | N   | NULL                                    |                                    |
| ParameterSubtype   | O   | Analysis population                    | `ITT`, `mITT`, `Per_Protocol`, `Safety` |
| TreatmentArm       | R   | Drug vs comparator                     | VcR-CAP, R-CHOP                    |
| ArmN               | E   | N per arm                              |                                    |
| StudyContext        | O   | Study name                             | SPRING-2, SINGLE                   |
| DoseRegimen        | O   | Dose if dose-response studied          |                                    |
| Dose                | O   | Numeric dose value (0.0 for placebo)   |                                    |
| DoseUnit            | O   | Normalized dose unit                   |                                    |
| Population          | O   | Analysis population when stratified    | ITT, mITT (alternative to Subtype) |
| Subpopulation       | O   | In-table partition (rare for Efficacy) |                                    |
| Timepoint           | O   | Assessment time                        | At Week 24, At 2 years             |
| Time                | O   | Numeric timepoint                      | 24.0                               |
| TimeUnit            | O   | Unit for Time                          | weeks, months, years               |
| PrimaryValue       | R   | Risk measure or rate                   |                                    |
| PrimaryValueType   | R   | What kind of measure                   | `HazardRatio`, `OddsRatio`, `RelativeRisk`, `RiskDifference`, `Percentage`, `Median` |
| SecondaryValue     | N   | NULL (efficacy reports CI, not SD)     |                                    |
| SecondaryValueType | N   | NULL                                    |                                    |
| LowerBound/Upper   | E   | 95% CI bounds                          |                                    |
| BoundType          | E   | CI level                               | `95CI`                             |
| PValue             | O   | Significance for comparison            |                                    |
| Unit               | O   | `%` for percentages; NULL for ratios   |                                    |

**Comparison key:** `ParameterName + TreatmentArm + PrimaryValueType`

---

## Enum Definitions (All Columns)

### TableCategory

```
AdverseEvent · PK · DrugInteraction · Efficacy · SKIP
```

### PrimaryValueType (canonical, tightened)

```
ArithmeticMean · GeometricMean · GeometricMeanRatio · Median ·
Percentage · Count · PercentChange · HazardRatio · OddsRatio ·
RelativeRisk · RiskDifference · LSMean · Numeric · Text · PValue
```

### SecondaryValueType

```
SD · CV · Count
```

### BoundType

```
90CI · 95CI · 99CI · CI · Range · SD · IQR
```

### ParseRule

```
empty_or_na · pvalue · frac_pct · n_pct · caption_mean_sd ·
value_cv · value_plusminus · value_ci · rr_ci · diff_ci ·
range_to · percentage · plain_number · text_descriptive ·
plain_number+caption · value_ci+caption
```

---

## tmp_FlattenedAdverseEventTable (Stage 5 AE Denormalization)

Downstream denormalized AE-only projection of `tmp_FlattenedStandardizedTable`.
Each row carries pre-computed risk statistics (RR, DNRR, 95% CI) plus PERSISTED
log-scale companions so visualizations bind without runtime stats. The contract
below applies only to this table — it does **not** override any contract above.

For the calculation rules (comparator pairing, Katz log-method, Haldane-Anscombe
correction, log-linear DNRR, IsPlaceboControlled trial-design classification),
see `normalization-rules.md` §7.

### Surrogate Key

| Column | Type | Content |
|--------|------|---------|
| tmp_FlattenedAdverseEventTableID | INT IDENTITY | Surrogate PK (required for EF Core change tracking) |

### Source Projection (copied verbatim from `tmp_FlattenedStandardizedTable`)

| Column | Req | Contains | Notes |
|--------|-----|----------|-------|
| tmp_FlattenedStandardizedTableID | R | FK to source row | No FK constraint — source is truncate/rebuilt |
| DocumentGUID | E | SPL document identity | |
| UNII | E | Active ingredient code(s) | May be `+`-concatenated |
| ParameterName | R | MedDRA Preferred Term | AE term, e.g. `Nausea` |
| ParameterCategory | E | Canonical MedDRA SOC | e.g. `Nervous System Disorders` |
| ArmN | R | Treatment arm sample size | Required for RR computation |
| Dose | E | Numeric dose for treatment arm | 0 for placebo (excluded from output as comparator) |
| DoseUnit | E | Normalized dose unit | `mg`, `mg/d`, `mg/kg` |
| PrimaryValue | R | Source value (incidence % or count) | Required for RR computation |
| PrimaryValueType | R | What PrimaryValue represents | **Copied verbatim — never derived.** Computation requires like-typed comparator. |
| StudyContext | O | Colspan-derived study context | e.g. `Adults`, `Children and Adolescents`. Participates in comparator group key. |
| Population | O | Caption-derived whole-table population | e.g. `Adult Healthy Volunteers`. Participates in comparator group key. |
| Subpopulation | O | In-table partition | e.g. `Female Patients Only`. Participates in comparator group key. Detected from mid-body N= rows by AE parsers. |

### Comparator Metadata (Phase 2 service populates)

| Column | Req | Contains | Legal Values |
|--------|-----|----------|--------------|
| TreatmentArm | R | Drug name from source row | |
| ComparatorArm | E | Name of the comparator row this RR is calculated against | NULL when no comparator could be paired |
| ComparatorN | E | Sample size for the comparator arm | |
| IsPlaceboControlled | R | **Trial-design flag (Document-level)** | `1` only when document arms = drug + placebo (no active comparator); `0` for mixed placebo+active, active-only, stepped-dose, or single-arm. Same value on every row for a given DocumentGUID. |

### Derived Event Counts (Phase 2 service populates)

| Column | Req | Contains | Notes |
|--------|-----|----------|-------|
| EventsTreatment | E | Event count derived from PrimaryValue | `a` in 2x2; for `Percentage`: `ArmN x PrimaryValue / 100`. For `Numeric` (count): `PrimaryValue` directly. |
| EventsComparator | E | Comparator event count | `c` in 2x2 |

### Risk Statistics (point estimates)

| Column | Req | Contains | Method |
|--------|-----|----------|--------|
| RR | E | Relative Risk | Katz: `(a/n1) / (c/n2)` |
| DNRR | O | Dose-Normalized RR | Log-linear with intra-study reference dose: `exp(ln(RR) / ln(Dose / D_ref))` |

### 95% CI Bounds (linear scale)

| Column | Req | Contains |
|--------|-----|----------|
| RRLowerBound | E | `exp(ln(RR) - 1.96 x SE(logRR))` |
| RRUpperBound | E | `exp(ln(RR) + 1.96 x SE(logRR))` |
| DNRRLowerBound | O | Log-bound divided by `ln(Dose / D_ref)`, then exponentiated |
| DNRRUpperBound | O | Same |

### Log-Scale Companions (PERSISTED computed columns)

| Column | Computed As | Notes |
|--------|-------------|-------|
| LogRR | `CASE WHEN RR > 0 THEN LOG(RR) END PERSISTED` | Materialized; auto-maintained |
| LogRRLowerBound | `CASE WHEN RRLowerBound > 0 THEN LOG(RRLowerBound) END PERSISTED` | |
| LogRRUpperBound | `CASE WHEN RRUpperBound > 0 THEN LOG(RRUpperBound) END PERSISTED` | |
| LogDNRR | `CASE WHEN DNRR > 0 THEN LOG(DNRR) END PERSISTED` | |
| LogDNRRLowerBound | `CASE WHEN DNRRLowerBound > 0 THEN LOG(DNRRLowerBound) END PERSISTED` | |
| LogDNRRUpperBound | `CASE WHEN DNRRUpperBound > 0 THEN LOG(DNRRUpperBound) END PERSISTED` | |

The `CASE WHEN > 0` guards prevent `LOG(0)` and `LOG(NULL)` errors. `DNRR` may
be < 1 for protective effects, which is mathematically valid (`LOG(0.5) ≈ -0.693`);
the guard is strictly for zero/null safety.

### Calculation Provenance

| Column | Req | Contains | Legal Values |
|--------|-----|----------|--------------|
| CalculationMethod | E | Method label | `KATZ_LOG`, `HALDANE_ANSCOMBE` |
| CalculationFlags | O | Semicolon-delimited audit | See flag dictionary below |

### CalculationFlags Dictionary

| Flag | Meaning |
|------|---------|
| `ZERO_CELL_CORRECTED` | Treatment or comparator events count was 0; Haldane-Anscombe continuity correction applied for the SE step |
| `PLACEBO_COMPARATOR` | Comparator was a placebo/sham/vehicle arm or had `Dose = 0` |
| `ACTIVE_COMPARATOR` | Comparator was an active-control drug (different UNII than index drug) |
| `LOW_DOSE_COMPARATOR` | Comparator was the lowest non-zero `Dose` in the group (stepped-dose fallback) |
| `NO_COMPARATOR` | Single-arm trial; no comparator could be paired. RR/CI/DNRR are NULL |
| `UNCOMPARABLE_VALUE_TYPE` | Both rows share a `PrimaryValueType` that doesn't yield event counts (e.g. both `Mean`); RR not computable |
| `MIXED_VALUE_TYPES` | Treatment and comparator have different `PrimaryValueType`; calculation requires like-typed pairs |
| `IS_REFERENCE_DOSE` | This row's `Dose` equals the group's `D_ref`, so DNRR denominator `ln(1) = 0`; DNRR is NULL |
| `NO_DOSE_RANGE` | Only one non-zero `Dose` exists in the group; DNRR is undefined |

### Indexes

```
PK_tmp_FlattenedAdverseEventTable                clustered on tmp_FlattenedAdverseEventTableID
IX_FAE_DocumentGUID                              nonclustered on DocumentGUID
IX_FAE_UNII                                      nonclustered on UNII
IX_FAE_ParameterName                             nonclustered on ParameterName
IX_FAE_ParameterCategory                         nonclustered on ParameterCategory
IX_FAE_SourceID                                  nonclustered on tmp_FlattenedStandardizedTableID
```

### Visualization Field-Shape Mapping

```
ae_explorer.jsx               rr_heatmap.jsx
─────────────────────         ───────────────────────
drug → ProductTitle*          drug → ProductTitle*
sys  → ParameterCategory      sys  → ParameterCategory
ae   → ParameterName          ae   → ParameterName
rr   → RR                     rr   → RR
lo   → RRLowerBound           lo   → RRLowerBound
hi   → RRUpperBound           hi   → RRUpperBound
n    → ArmN                   n    → ArmN
                              (clustering on LogRR — no runtime Math.log)
```

\* `ProductTitle` and `cls` (drug class) are not yet sourced into this table; downstream
joins or enrichment may add them in a future iteration.
