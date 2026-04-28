# Normalization Rules

Deterministic (Tier 1) rules for the three dirty columns, plus cleanup rules
for ParameterCategory, ParameterName, and TreatmentArm. ML.NET (Tier 2)
guidance included for ambiguous cases.

---

## 0. NULL Preservation Rule (governs every rule below)

Every rule in this document that prescribes `<column> = NULL` is a **routing**
or **header-echo** rule — not a license to erase data. Any actor applying
these rules (deterministic parser, ML.NET Tier 2, or Claude Stage 3.5
correction) must preserve valid values:

- A value may be set to NULL **only** when one of these holds:
  1. **Routed.** The content is being moved into another column in the same
     operation (e.g. DoseRegimen → ParameterSubtype). The destination write
     must happen atomically with the NULL.
  2. **Header / caption echo.** The value is literally a column header, row
     label, or caption fragment that leaked into a data column and matches
     one of the explicit echo patterns below.
  3. **Schema-invalid for TableCategory.** The column is defined as NULL for
     this table type per `column-contracts.md`
     (e.g. ParameterCategory outside AdverseEvent).

- Any value that is parseable, meaningful, and conforms to its column
  contract is **kept as-is**. A mildly suboptimal value is always preferable
  to a NULL that deletes it. When in doubt, leave the value alone.

- Enum columns (`PrimaryValueType`, `SecondaryValueType`, `BoundType`) are
  corrected **to another enum member**, never to NULL.

This rule exists because prior Claude corrections occasionally nulled
perfectly good parsed values that were merely unusual or hard to classify —
destroying information the downstream pipeline depended on.

---

## 1. DoseRegimen Triage

After Population and Timepoint are already split to their own columns,
DoseRegimen still holds content that belongs elsewhere.

### Triage Algorithm (priority order, first match wins)

```
PRI  TEST                                                    ACTION
───  ──────────────────────────────────────────────────────  ──────────────────
1    PK sub-param dictionary match (case-insensitive):       → ParameterSubtype
       CV(%), AUC*, Cmax*, Tmax*, t1/2*, CL/F*, V/F,         DoseRegimen = NULL
       Vss, Vd, Clearance*, half-life*, ke, MRT,              Flag PK_SUBPARAM_ROUTED
       Serum (AUC|T1/2|Cmax)*, *elimination half-life*,
       *clearance (CL*, *volume of distribution*

     SPECIAL CASE: If DoseRegimen value IS a PK parameter
     name (e.g., AUC(0-inf)) AND differs from ParameterName,
     it may be the ACTUAL ParameterName (table had flipped
     row/column headers). Evaluate whether to swap into
     ParameterName instead.

2    Actual dose regex:                                      → Keep in DoseRegimen ✓
       \d+\.?\d*\s*(mg|mcg|µg|g|mL|units?|IU)
       optionally followed by /(kg|m2|day|dose)
       or (oral|IV|SC|IM|BID|QD|once|twice|daily)

3    Drug name dictionary match AND                          → ParameterSubtype
     TableCategory ∈ {DrugInteraction, PK}                    DoseRegimen = NULL
                                                              Flag COADMIN_ROUTED

4    Residual population pattern:                            → Population
       (adult|pediatric|elderly|renal|hepatic|child|           DoseRegimen = NULL
        healthy|volunteer|neonat|\d+-\d+\s*kg)

5    Residual timepoint pattern:                             → Timepoint
       (day|week|month|cycle|baseline|steady|                  DoseRegimen = NULL
        single.dose|pre-?dose|visit)

6    Column header echo:                                     → DoseRegimen = NULL
       "Co-administered Drug" (literal)                        Flag ROW_TYPE=HEADER

7    No match                                                → Keep in DoseRegimen
                                                              Flag DOSE_SEMANTIC_AMBIG
```

### ML.NET Features for Ambiguous DoseRegimen (Tier 2)

```
table_category          string   TableCategory (categorical)
token_count             int      Whitespace-delimited tokens
char_length             int      Character length
contains_digit          bool     Any digit present
contains_dose_unit      bool     Contains mg/mcg/g/mL/units
is_in_drug_dictionary   bool     Matches drug name dictionary
is_in_pk_dictionary     bool     Matches PK parameter pattern
sibling_dose_mode       string   Most common triage class in same table
caption_keywords        string   Tokenized Caption text
```

---

## 1.5 PK ParameterName / ParameterSubtype Enforcement

The PK column contract (see `column-contracts.md` → PK) states:
- **`ParameterName` (R)** must hold a canonical PK parameter name (Cmax, AUC0-24, t½, CL/F, Vss, …).
- **`ParameterSubtype` (O)** must hold only a short PK qualifier (`CV(%)`, `steady_state`, `single_dose`, `fasted`, `fed`).

PK terms must NEVER appear in `ParameterSubtype`. Other content — drug names,
populations, doses, study labels — displaced into `ParameterName` by parser
ambiguity must be routed to its contract-assigned column rather than dropped.

This enforcement runs in Stage 3.25 Phase 2 as `applyPkCanonicalization`,
after `extractUnitFromParameterSubtype` (so parenthesized units are already
stripped) and before `normalizeUnit`.

### Ordered Pipeline

```
STEP  ACTION
────  ────────────────────────────────────────────────────────────────
1     Fast path: Name canonicalizes → normalize and fall through.
        Flag: PK_NAME_CANONICALIZED (when value changed)

2     Rescue PK term when Name does NOT canonicalize:
        2a: TryExtractCanonicalFromPhrase on Subtype.
            - Bare token (no whitespace/commas) → flag PK_NAME_SUBTYPE_SWAPPED
            - Descriptive phrase → flag PK_NAME_FROM_PHRASE
            Route displaced Name via routeOrParkNameContent.
            Subtype replaced by detected qualifier (or null).
        2b: TryExtractCanonicalFromPhrase on Name (Name IS the phrase).
            Route displaced Name only if Name had more than the canonical.
            Flag: PK_NAME_FROM_PHRASE

3     Subtype scrub: if Subtype still holds a PK term, demote/scrub it.
        IsPkParameter(Subtype) → null, flag PK_SUBTYPE_SCRUBBED
        ContainsPkParameter(Subtype) → reduce to residual qualifier,
                                       flag PK_SUBTYPE_SCRUBBED

4     PD marker flag (unchanged): IPA / VASP-PRI / etc. flagged but preserved.
        Flag: PK_NON_PK_MARKER_DETECTED
```

### routeOrParkNameContent — Displaced Name Routing

First-match-wins ordered decision tree for preserving content displaced from
`ParameterName`. Honors NULL Preservation Rule §0.

```
ORDER  TEST                                              ACTION
─────  ────────────────────────────────────────────────  ────────────────────
i      PopulationDetector.TryMatchLabel matches          Population = canonical
         (dictionary OR regex second pass — age ranges,   Flag: PK_POPULATION_ROUTED
         renal bands, trimesters, infants-birth-to-N)         or PK_POPULATION_ROUTED_REGEX

ii     DoseExtractor.Extract yields a dose AND the       TreatmentArm = drug prefix
       non-dose prefix is a known drug name              DoseRegimen = dose fragment
                                                          Dose, DoseUnit populated
                                                          Flag: PK_NAME_ROUTED_ARM

iii    isDrugName(Name) (no dose present)                TreatmentArm = Name
                                                          Flag: PK_NAME_ROUTED_ARM

iv     DoseExtractor yields a dose AND no drug prefix    DoseRegimen = Name
                                                          Flag: PK_NAME_ROUTED_DOSE

v      Name matches _headerEchoSet ("Population          No column written
       Estimates", "Values", "Estimate", etc.)            Flag: PK_NAME_ECHO_DROPPED
                                                          (NULL Preservation §0.2
                                                           header-echo carve-out)

vi     StudyContext empty                                StudyContext = Name
                                                          Flag: PK_NAME_PARKED_CTX

vii    (last resort — StudyContext occupied)             No column written
                                                          Flag: PK_NAME_DROPPED_UNCLASSIFIED
```

### Dictionary Aliases — Extensions added for the enforcement pass

| Canonical | New aliases |
|-----------|-------------|
| AUC       | `Total AUC`, `Overall AUC` |
| Cmax      | `Peak concentration`, `Peak Concentrations`, `Peak concentration at steady state` |
| Tmax      | `TPEAK`, `T peak`, `T-peak`, `Time of Peak` |
| Ctrough   | `C0h`, `C 0h`, `C0`, `Concentration at 0 hours`, `Predose Concentration` |
| Vss       | `Volume of Distribution at Steady State` (and lowercase/hyphenated variants) |
| AUC0-inf  | `AUC0 to ∞`, `AUC0 to inf`, `AUC 0 to ∞`, `AUC 0 to inf` (space-word variants) |
| AUC0-24   | `AUC0 to 24`, `AUC 0 to 24`, `AUC0 to 24h` |
| AUC0-12   | `AUC0 to 12`, `AUC 0 to 12` |
| AUC0-t    | `AUC0 to t`, `AUC 0 to t` |

### PopulationDetector Regex Second Pass

Pattern second-pass runs after the strict dictionary lookup misses. Anchored
`^...$` where sensible to prevent over-matching arbitrary prose.

| Pattern                                                         | Canonical form         |
|-----------------------------------------------------------------|------------------------|
| `^\s*(?<lo>\d+)\s*(?:to|[-–])\s*(?<hi>\d+)\s*years?\s*$`       | `Ages {lo}-{hi} Years` |
| `^\s*Infants?\s+(?:from\s+)?Birth\s+to\s+(?<hi>\d+)\s+(?<unit>Months?|Years?)\s*$` | `Infants Birth to {hi} {Unit}` |
| `^\s*(?<band>Normal|Mild|Moderate|Severe|ESRD|End[\s-]Stage)\b.*?Creatinine\s+Clearance` | `{band} Renal Function` |
| `^\s*(?<ord>1st|2nd|3rd|First|Second|Third)\s+Trimester`        | `{Ord} Trimester`      |

`ESRD` is preserved all-caps; other bands are title-cased (`Normal`, `Mild`,
`Moderate`, `Severe`, `End-Stage`).

### Validation Flags Added by §1.5

- `COL_STD:PK_NAME_FROM_PHRASE` — descriptive phrase decomposed into canonical + qualifier
- `COL_STD:PK_NAME_ROUTED_ARM` — Name was a drug name / drug+dose compound
- `COL_STD:PK_NAME_ROUTED_DOSE` — Name was a pure dose regimen
- `COL_STD:PK_NAME_ECHO_DROPPED` — Name matched a known column-header echo
- `COL_STD:PK_NAME_PARKED_CTX` — Name preserved into StudyContext as rescue
- `COL_STD:PK_NAME_DROPPED_UNCLASSIFIED` — last-resort drop; **0 on clean data**
- `COL_STD:PK_SUBTYPE_SCRUBBED` — Subtype held a PK term while Name was already canonical
- `COL_STD:PK_POPULATION_ROUTED_REGEX` — population matched by regex second pass (vs dictionary)

---

## 2. PrimaryValueType Enum Migration

### From Old to New

```
OLD VALUE                →  NEW VALUE               CONDITION
────────────────────────    ──────────────────────   ─────────────────────────
"Mean"                   →  GeometricMean            IF TableCategory ∈ {PK, DrugInteraction}
                                                       and Caption lacks "arithmetic"
                         →  GeometricMeanRatio        IF TableCategory = DrugInteraction
                                                       and bounds present
                         →  ArithmeticMean            IF Caption has "arithmetic"
                                                       or TableCategory = AdverseEvent
                                                       or DEFAULT
"Median"                 →  Median                    Direct
"Percentage"             →  Percentage                Direct (Unit should be "%")
"Count"                  →  Count                     Direct
"MeanPercentChange"      →  PercentChange             Direct
"RelativeRiskReduction"  →  HazardRatio               IF Caption has "hazard"
                         →  OddsRatio                  IF Caption has "odds"
                         →  RelativeRisk               DEFAULT
"RiskDifference"         →  RiskDifference             Direct
"Text"                   →  Text                       Direct
"PValue"                 →  PValue                     Direct
"Numeric"                →  [RESOLVE — SEE BELOW]
```

### Resolving "Numeric" (2,213 rows — 22% of data)

"Numeric" means the parser could not determine the statistical meaning. Resolve
with context in this priority order:

```
CONDITION                                              ASSIGN
──────────────────────────────────────────────────     ──────────────────
TableCategory = AdverseEvent AND Unit = "%"            Percentage
TableCategory = AdverseEvent AND Unit NULL AND int     Count
TableCategory = PK                                     ArithmeticMean (unless caption has "geometric")
TableCategory = DrugInteraction                        GeometricMeanRatio (unless caption has "geometric")
TableCategory = Efficacy AND bounds present            HazardRatio
Caption contains "geometric mean" (case-insensitive)   GeometricMean
Caption contains "arithmetic mean"                     ArithmeticMean
Caption contains "LS mean" or "least square"           LSMean
Caption contains "mean" (generic)                      ArithmeticMean
Caption contains "median"                              Median
STILL UNRESOLVED                                       Numeric
                                                       Flag VALUETYPE_UNRESOLVED
```

### ML.NET Features for PrimaryValueType (Tier 2)

```
table_category          string   TableCategory (categorical)
unit_value              string   Unit column (categorical)
unit_is_percent         bool     Unit = "%"
caption_tokens          string   Tokenized Caption
has_bounds              bool     LowerBound is not NULL
bound_type              string   BoundType value
parameter_name          string   ParameterName (categorical)
value_magnitude         float    log10(abs(PrimaryValue) + 1)
value_is_integer        bool     PrimaryValue == floor(PrimaryValue)
parse_rule              string   ParseRule (categorical)
sibling_pvt_mode        string   Most common PrimaryValueType in same table
section_code            string   ParentSectionCode
```

---

## 3. Unit Scrub

### Rules (priority order)

```
PRI  TEST                                                 ACTION
───  ──────────────────────────────────────────────────   ──────────────────
1    Exact match in KnownUnitDictionary                   → Keep ✓

2    len > 30 AND NOT in KnownUnitDictionary              → Unit = NULL
                                                            Flag UNIT_HEADER_LEAK

3    Contains drug name (dictionary match)                 → Unit = NULL
                                                            Flag UNIT_HEADER_LEAK

4    Contains keywords (case-insensitive):                 → Unit = NULL
       Regimen|Dosage|Patients|Titration|Starting|          Flag UNIT_HEADER_LEAK
       Recommended|Duration|TAKING|Tablets|Injection|
       Therapy|Combination|Divided

5    Extractable real unit inside verbose description:     → Unit = extracted unit
       "Drug Delivery Rate (mcg/kg/min)" → "mcg/kg/min"    Flag UNIT_NORMALIZED

6    Normalize variant spellings:                          → Normalize
       mcg h/mL   → mcg·h/mL                                Flag UNIT_NORMALIZED
       nghr/mL    → ng·h/mL
       L/kghr     → L/kg/h
       mcgh/mL    → mcg·h/mL
       hr         → h
       pp         → percentage points
```

### Known Unit Dictionary

```
%  %CV  h  hr  min  days  weeks  months  years
mg  mcg  µg  g  kg
mcg/mL  ng/mL  pg/mL  µg/mL  mg/L  ng/dL
mcg·h/mL  ng·h/mL  µg·h/mL  pg·h/mL
mL/min  mL/min/kg  L/h  L/h/kg  mL/h/kg
L  mL  L/kg
mcg/kg/min  mg/h  IU/mL
mg/kg  mcg/kg  mg/m²  mg/kg/day
ratio  g/cm²  beats/min  mmHg  mEq/L  mOsm/kg
percentage points  subjects  events  patients
```

---

## 4. ParameterCategory Canonical SOC Mapping

Apply AFTER: HTML decode → OCR artifact repair → trim → case-insensitive lookup.

**Only applies when TableCategory = AdverseEvent.**
For all other table types, ParameterCategory should be NULL or passed through
without SOC normalization.

### OCR Artifact Repair

Before lookup, collapse isolated single characters: `"Ear D i sorders"` → `"Ear Disorders"`

### Canonical Map (key entries — case-insensitive match)

```
CANONICAL                                  ← VARIANTS
─────────────────────────────────────────  ─────────────────────────────────
Blood and Lymphatic System Disorders       blood and lymphatic system disorders · hematologic
Cardiac Disorders                          cardiac disorders
Ear and Labyrinth Disorders                ear disorders · ear and labyrinth disorders
Eye Disorders                              eye disorders · eye disorders (other than field or acuity changes) · special senses
Gastrointestinal Disorders                 gastrointestinal disorders · gastrointestinal · digestive system · gastro - intestinal system disorders
General Disorders                          general disorders · general disorders and administration site conditions · body as a whole
Hepatobiliary Disorders                    hepatobiliary disorders · liver and biliary system disorders
Infections and Infestations                infections and infestations · resistance mechanism disorders
Metabolism and Nutrition Disorders          metabolism and nutrition disorders · metabolic and nutritional
Musculoskeletal Disorders                  musculoskeletal and connective tissue disorders · musculo-skeletal system disorders
Nervous System Disorders                   nervous system disorders · nervous system · central & peripheral nervous system disorders · cns
Psychiatric Disorders                      psychiatric disorders · psychiatric
Renal and Urinary Disorders                renal and urinary disorders · urogenital system
Respiratory Disorders                      respiratory, thoracic and mediastinal disorders · respiratory system
Skin and Subcutaneous Tissue Disorders     skin and subcutaneous tissue disorders · skin and subcutaneous tissues disorders · skin · dermatologic
Vascular Disorders                         vascular disorders · cardiovascular
```

### Dictionary Lookup for NULL ParameterCategory

When ParameterCategory is NULL after the canonical SOC map lookup (i.e., the
source table lacked SOC divider rows), `AeParameterCategoryDictionaryService`
attempts a deterministic resolution:

1. Case-insensitive lookup of ParameterName against a static dictionary of
   698 unambiguous ParameterName → canonical SOC mappings.
2. If found, ParameterCategory is set and `DICT:SOC_RESOLVED` flag appended.
3. Only fires for TableCategory = AdverseEvent and NULL/whitespace ParameterCategory.
4. Never overwrites an existing (non-NULL) ParameterCategory.

This runs in Stage 3.25 Phase 2 (Content Normalization), after
`normalizeParameterCategory` normalizes existing non-NULL values.

**Tier 2 (ML.NET):** For unmatched variants, token Jaccard + Levenshtein ratio
against canonical SOC names. Accept if both > 0.6.

---

## 5. ParameterName Cleanup

Route non-parameter content out of ParameterName.

```
PRI  TEST                                                 ACTION
───  ──────────────────────────────────────────────────   ──────────────────
1    Starts with "Table \d" or contains "Table \d."       → Flag ROW_TYPE=CAPTION
     or contains "Pharmacokinetic Parameters" or            ParameterName = NULL
     "Geometric Mean Ratio (90% CI)" or
     "Drug Interactions:" (len > 60)

2    Exact match: ^[nN]$ or ^[nN]\s*\(                    → Flag ROW_TYPE=HEADER
                                                            ParameterName = NULL

3    Bare integer matching common dose level AND           → Move to DoseRegimen
     TableCategory = PK                                     Flag PARAM_WAS_DOSE
     Set: {5,10,15,20,25,30,40,50,100,150,200,
           250,300,400,500,600,800,1200,1600,2400,3600}

4    TableCategory = DrugInteraction AND value matches     → Move to ParameterSubtype
     drug name dictionary AND value is NOT a PK param        Flag COADMIN_ROUTED

5    HTML entities (&gt; &lt; &amp;)                       → Decode
                                                            Flag HTML_ENTITY_DECODED

6    OCR spacing artifacts (single chars with spaces)      → Collapse
```

---

## 6. TreatmentArm Cleanup

Route non-arm content out of TreatmentArm.

```
PRI  TEST                                                 ACTION
───  ──────────────────────────────────────────────────   ──────────────────
1    Contains "Number"+"Patients" or "Percent"+"Subjects"  → TreatmentArm = NULL
     or "Percentage"+"Reporting" (~884 rows)                 Flag ARM_WAS_HEADER

2    Regex: \[?\s*[Nn]\s*=\s*(\d+)\s*\]?                  → Extract → ArmN
                                                            Strip from TreatmentArm

3    Embedded dose: (\d+\.?\d*\s*(mg|mcg|g)[/\w]*)         → Extract → DoseRegimen
     when arm = "150 mg/d [N=302]"                           Strip from TreatmentArm
                                                            Flag DOSE_EXTRACTED

4    Value ∈ {Comparison, Treatment, PD, SAD}              → TreatmentArm = NULL
                                                            Flag ARM_WAS_GENERIC

5    Study name (all-caps short or known set):             → Move to StudyContext
     SPRING-2, SINGLE, SAILING, etc.                         Flag ARM_WAS_STUDY
```

---

## Validation Flags — Complete Catalog

```
FLAG                        TRIGGER
──────────────────────────  ──────────────────────────────────────────
PCT_CHECK:PASS              Percentage value in [0,100] — OK
PCT_CHECK:WARN:{value}      Possible percentage (0–1) not converted
CAPTION_HINT:{text}         Caption influenced PrimaryValueType
CAPTION_REINTERPRET:{rule}  ParseRule overridden by caption context
UNIT_HEADER_LEAK            Unit was a leaked column header → cleaned
UNIT_NORMALIZED             Verbose unit → standard abbreviation
DOSE_SEMANTIC_AMBIG         DoseRegimen content couldn't be triaged
DOSE_EXTRACTED              Dose extracted from TreatmentArm
PK_SUBPARAM_ROUTED          PK sub-param moved from DoseRegimen → ParameterSubtype
COADMIN_ROUTED              Co-admin drug moved to ParameterSubtype
POPULATION_EXTRACTED        Population extracted from DoseRegimen or Arm
TIMEPOINT_EXTRACTED         Timepoint extracted from DoseRegimen
PARAM_WAS_DOSE              ParameterName was a bare dose number → moved
ARM_WAS_HEADER              TreatmentArm was a column header echo → nulled
ARM_WAS_GENERIC             TreatmentArm was a generic label → nulled
ARM_WAS_STUDY               TreatmentArm was a study name → moved
ROW_TYPE=CAPTION            Row is a caption echo, not data
ROW_TYPE=HEADER             Row is a structural header, not data
ARM_N_MISMATCH              ArmN inconsistent with count/pct
BOUND_TYPE_INFERRED         BoundType guessed from TableCategory
PVALUE_OUT_OF_RANGE         PValue > 1.0 (likely misparse)
VALUETYPE_UNRESOLVED        PrimaryValueType couldn't resolve from Numeric
HTML_ENTITY_DECODED         HTML entities decoded from RawValue/ParameterName
FOOTNOTE_STRIPPED           Footnote marker removed during parse
DICT:SOC_RESOLVED           NULL ParameterCategory resolved from AE dictionary lookup
COL_STD:PK_NAME_CANONICALIZED        PK name collapsed to canonical form
COL_STD:PK_NAME_SUBTYPE_SWAPPED      Bare PK term promoted from Subtype to Name
COL_STD:PK_NAME_FROM_PHRASE          PK canonical extracted from descriptive phrase
COL_STD:PK_NAME_ROUTED_ARM           Displaced Name routed to TreatmentArm (drug / drug+dose)
COL_STD:PK_NAME_ROUTED_DOSE          Displaced Name routed to DoseRegimen
COL_STD:PK_NAME_ECHO_DROPPED         Displaced Name matched known column-header echo
COL_STD:PK_NAME_PARKED_CTX           Displaced Name parked into StudyContext
COL_STD:PK_NAME_DROPPED_UNCLASSIFIED Last-resort drop (0 expected on clean data)
COL_STD:PK_SUBTYPE_SCRUBBED          Subtype demoted / reduced to qualifier
COL_STD:PK_POPULATION_ROUTED         Displaced Name matched population dictionary
COL_STD:PK_POPULATION_ROUTED_REGEX   Displaced Name matched population regex second-pass
COL_STD:PK_NON_PK_MARKER_DETECTED    IPA / VASP-PRI etc. flagged for review
```
