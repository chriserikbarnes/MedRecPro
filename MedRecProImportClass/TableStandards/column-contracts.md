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
| Population          | O   | Sub-population when stratified         | Pediatric, Elderly                 |
| Timepoint           | N   | NULL — AE tables are cumulative        |                                    |
| Time / TimeUnit     | N   | NULL                                    |                                    |
| PrimaryValue       | R   | Incidence (%) or integer count         |                                    |
| PrimaryValueType   | R   | How the value was reported             | `Proportion`, `Count`              |
| SecondaryValue     | O   | Count (from n_pct: n=count, %=PV)     |                                    |
| SecondaryValueType | O   | What SecondaryValue is                 | `Count`                            |
| LowerBound/Upper   | O   | CI for incidence difference (rare)     |                                    |
| BoundType          | O   | CI level if bounds present             | `95CI`                             |
| PValue             | O   | Comparison p-value                     |                                    |
| Unit               | E   | `%` or NULL (when count only)          |                                    |

**Comparison key:** `ParameterName + TreatmentArm + DoseRegimen`

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
| Population          | O   | Subject characteristics when stratified| Healthy Volunteers, Renal Impairment |
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
| Population          | O   | Usually Healthy Volunteers (implicit)  |                                    |
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
| Population          | O   | Analysis population when stratified    | ITT, mITT (alternative to Subtype) |
| Timepoint           | O   | Assessment time                        | At Week 24, At 2 years             |
| Time                | O   | Numeric timepoint                      | 24.0                               |
| TimeUnit            | O   | Unit for Time                          | weeks, months, years               |
| PrimaryValue       | R   | Risk measure or rate                   |                                    |
| PrimaryValueType   | R   | What kind of measure                   | `HazardRatio`, `OddsRatio`, `RelativeRisk`, `RiskDifference`, `Proportion`, `Median` |
| SecondaryValue     | N   | NULL (efficacy reports CI, not SD)     |                                    |
| SecondaryValueType | N   | NULL                                    |                                    |
| LowerBound/Upper   | E   | 95% CI bounds                          |                                    |
| BoundType          | E   | CI level                               | `95CI`                             |
| PValue             | O   | Significance for comparison            |                                    |
| Unit               | O   | `%` for proportions; NULL for ratios   |                                    |

**Comparison key:** `ParameterName + TreatmentArm + PrimaryValueType`

---

### Dosing

Tables of recommended doses, titration schedules, dose adjustments.

| Column             | Req | Contains                               | Legal Values / Examples           |
|--------------------|-----|----------------------------------------|-----------------------------------|
| ParameterName      | R   | Dose descriptor or indication          | Starting Dose, Maintenance Dose, Titration Step, Renal Adjustment |
| ParameterCategory  | N   | NULL                                    |                                    |
| ParameterSubtype   | O   | Adjustment context                     | `renal`, `hepatic`, `weight_based`, `CYP2D6_poor_metabolizer` |
| TreatmentArm       | O   | Drug name (usually implicit from doc)  |                                    |
| ArmN               | N   | NULL                                    |                                    |
| StudyContext        | N   | NULL                                    |                                    |
| DoseRegimen        | E   | The actual dose amount                 | 20 mg, 5 mg/kg/day, 100 mg BID   |
| Population          | E   | Who this dose is for                   | Adult, Pediatric, Elderly, Renal Impairment (Severe) |
| Timepoint           | O   | Titration step timing                  | Week 1, Week 2, After 2 weeks     |
| Time                | O   | Numeric timing                         | 1.0, 2.0                          |
| TimeUnit            | O   | Unit for Time                          | weeks                              |
| PrimaryValue       | O   | Dose amount (when numeric)             |                                    |
| PrimaryValueType   | O   | Always prescriptive                    | `Numeric` (doses are not statistics) |
| SecondaryValue     | N   | NULL                                    |                                    |
| SecondaryValueType | N   | NULL                                    |                                    |
| LowerBound/Upper   | O   | Dose range                             |                                    |
| BoundType          | O   | Range type                             | `Range`                            |
| PValue             | N   | NULL                                    |                                    |
| Unit               | O   | mg, mg/kg, mg/day, mg/m²              |                                    |

**Comparison key:** `ParameterName + Population + DoseRegimen`

---

### BMD

Tables of bone mineral density measurements at anatomical sites.

| Column             | Req | Contains                               | Legal Values / Examples           |
|--------------------|-----|----------------------------------------|-----------------------------------|
| ParameterName      | R   | Anatomical measurement site            | Lumbar Spine, Femoral Neck, Total Hip, Trochanter |
| ParameterCategory  | N   | NULL                                    |                                    |
| ParameterSubtype   | N   | NULL                                    |                                    |
| TreatmentArm       | R   | Drug vs Placebo                        |                                    |
| ArmN               | E   | N per arm                              |                                    |
| StudyContext        | O   | Study name                             |                                    |
| DoseRegimen        | O   | Dose if multiple doses studied         |                                    |
| Population          | O   | Usually postmenopausal (implicit)      |                                    |
| Timepoint           | E   | Assessment time                        | 6 months, 12 months, 24 months    |
| Time                | E   | Numeric timepoint                      | 6.0, 12.0, 24.0                   |
| TimeUnit            | E   | Unit for Time                          | months                             |
| PrimaryValue       | R   | % change from baseline or BMD          |                                    |
| PrimaryValueType   | R   | What kind of value                     | `PercentChange`, `ArithmeticMean`  |
| SecondaryValue     | O   | SD                                     |                                    |
| SecondaryValueType | O   | Dispersion type                        | `SD`                               |
| LowerBound/Upper   | O   | 95% CI                                 |                                    |
| BoundType          | O   | CI level                               | `95CI`                             |
| PValue             | O   | Drug vs placebo comparison             |                                    |
| Unit               | E   | `%` or `g/cm²`                         |                                    |

**Comparison key:** `ParameterName + TreatmentArm + Timepoint`

---

### TissueDistribution

Tables of drug concentration across body tissues and fluids.

| Column             | Req | Contains                               | Legal Values / Examples           |
|--------------------|-----|----------------------------------------|-----------------------------------|
| ParameterName      | R   | Tissue or fluid name                   | Lung, Sputum, CSF, Liver, Tonsil  |
| ParameterCategory  | N   | NULL                                    |                                    |
| ParameterSubtype   | N   | NULL                                    |                                    |
| TreatmentArm       | O   | Drug name (usually single drug)        |                                    |
| ArmN               | O   | N                                      |                                    |
| StudyContext        | N   | NULL                                    |                                    |
| DoseRegimen        | E   | Dose administered                      |                                    |
| Population          | O   | Subject description                    |                                    |
| Timepoint           | E   | Hours post-dose                        | 2h, 4h, 24h, 48h                  |
| Time                | E   | Numeric time                           | 2.0, 4.0, 24.0                    |
| TimeUnit            | E   | Unit                                   | hours                              |
| PrimaryValue       | R   | Concentration                          |                                    |
| PrimaryValueType   | R   | Statistic                              | `ArithmeticMean`, `GeometricMean`  |
| SecondaryValue     | E   | Dispersion                             |                                    |
| SecondaryValueType | E   | Dispersion type                        | `SD`, `CV`                         |
| LowerBound/Upper   | N   | NULL                                    |                                    |
| BoundType          | N   | NULL                                    |                                    |
| PValue             | N   | NULL                                    |                                    |
| Unit               | R   | Concentration unit                     | mcg/mL, mcg/g, ng/g               |

**Comparison key:** `ParameterName + DoseRegimen + Timepoint + Unit`

---

### TextDescriptive

Tables containing only text — dosing instructions, regimen descriptions, etc.

| Column             | Req | Contains                               | Legal Values / Examples           |
|--------------------|-----|----------------------------------------|-----------------------------------|
| ParameterName      | E   | Description or label                   | Varies widely                      |
| ParameterCategory  | N   | NULL                                    |                                    |
| ParameterSubtype   | N   | NULL                                    |                                    |
| TreatmentArm       | O   | Drug name if applicable                |                                    |
| ArmN               | N   | NULL                                    |                                    |
| DoseRegimen        | O   | Dose if mentioned                      |                                    |
| Population          | O   | Population if mentioned                |                                    |
| Timepoint           | N   | NULL                                    |                                    |
| PrimaryValue       | N   | NULL                                    |                                    |
| PrimaryValueType   | R   | Always text                            | `Text`                             |
| RawValue           | R   | The full text content                  |                                    |
| All value columns  | N   | NULL                                    |                                    |

**Comparison key:** N/A — text tables are not numerically comparable.

---

## Enum Definitions (All Columns)

### TableCategory

```
AdverseEvent · PK · DrugInteraction · Efficacy · Dosing · BMD ·
TissueDistribution · Demographic · Laboratory · TextDescriptive · Unclassified
```

### PrimaryValueType (canonical, tightened)

```
ArithmeticMean · GeometricMean · GeometricMeanRatio · Median ·
Proportion · Count · PercentChange · HazardRatio · OddsRatio ·
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
