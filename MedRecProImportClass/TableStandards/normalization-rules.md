# Normalization Rules

Deterministic (Tier 1) rules for the three dirty columns, plus cleanup rules
for ParameterCategory, ParameterName, and TreatmentArm. ML.NET (Tier 2)
guidance included for ambiguous cases.

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
"Percentage"             →  Proportion                Direct (Unit should be "%")
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
TableCategory = AdverseEvent AND Unit = "%"            Proportion
TableCategory = AdverseEvent AND Unit NULL AND int     Count
TableCategory = PK                                     GeometricMean
TableCategory = DrugInteraction                        GeometricMeanRatio
TableCategory = BMD                                    PercentChange
TableCategory = Efficacy AND bounds present            HazardRatio
TableCategory = Dosing                                 Numeric (keep — prescriptive)
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

**Only applies when TableCategory = AdverseEvent (or Laboratory).**
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
     TableCategory ∈ {Dosing, PK}                           Flag PARAM_WAS_DOSE
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
PCT_CHECK:PASS              Proportion value in [0,100] — OK
PCT_CHECK:WARN:{value}      Possible proportion (0–1) not converted
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
```
