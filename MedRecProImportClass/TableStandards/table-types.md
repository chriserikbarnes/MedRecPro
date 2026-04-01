# TableCategory Classification

How to assign TableCategory — the single most important column for downstream
parsing. All normalization rules, PrimaryValueType assignment, BoundType
defaults, and ParameterSubtype interpretation depend on this value.

For per-type column expectations, see `references/column-contracts.md`.

---

## TableCategory Enum

```
VALUE                DESCRIPTION
───────────────────  ──────────────────────────────────────────────
AdverseEvent         Incidence/frequency of adverse events by arm
PK                   Pharmacokinetic parameters
DrugInteraction      Co-admin drug effects on PK (or vice versa)
Efficacy             Comparative efficacy outcomes with risk measures
Dosing               Recommended doses, titration, adjustments
BMD                  Bone mineral density at anatomical sites
TissueDistribution   Drug concentration across tissues/fluids
Demographic          Baseline patient characteristics
Laboratory           Lab parameter changes/shifts
TextDescriptive      100% text cells — instructions, descriptions
Unclassified         Could not be classified deterministically
```

---

## Decision Tree (Tier 1)

Inputs: all rows sharing a TextTableID, plus Caption and ParentSectionCode.
Apply tests in order. First match wins.

```
TEST                                                           → ASSIGN
─────────────────────────────────────────────────────────────  ─────────────────
1. 100% of PrimaryValueType = "Text"                           TextDescriptive

2. ≥3 ParameterNames match MedDRA PT dictionary                AdverseEvent
   OR ≥2 ParameterCategory values match canonical SOC

3. Any ParameterName in PK param dictionary:                   PK or DrugInteraction
     {Cmax, Tmax, AUC*, t½, CL/F, V/F, Vss, Vd,
      Clearance, half-life, ke, bioavailability}
   └─ Caption matches DDI keywords:                            → DrugInteraction
        drug interaction | co-administered | coadministered |
        in the presence of | with/without
   └─ ParameterName row labels are drug names (not PK params)  → DrugInteraction
   └─ Otherwise                                                → PK

4. PrimaryValueType set contains RelativeRiskReduction          Efficacy
   OR RiskDifference

5. Any ParameterName contains dosing keywords:                  Dosing
     dose | dosage | titration | starting | maintenance |
     adjustment | recommended

6. Any ParameterName matches BMD site dictionary:               BMD
     lumbar spine | femoral neck | total hip | trochanter |
     ward | total body | distal radius

7. ≥3 ParameterNames match tissue/organ dictionary AND          TissueDistribution
   Unit matches concentration pattern (mcg/mL, ng/g, etc.)

8. ParentSectionCode fallback:
     34084-4 (Adverse Reactions)           → AdverseEvent
     43685-7 (Clinical Pharmacology)       → PK
     34068-7 (Dosage and Administration)   → Dosing
     34073-7 (Drug Interactions)           → DrugInteraction

9. No match                                                     Unclassified
```

---

## ParentSectionCode Reference

Strong structural signal when content-based classification is ambiguous.

```
CODE        SECTION NAME                        LIKELY TABLECATEGORY
─────────   ──────────────────────────────────  ────────────────────
34084-4     ADVERSE REACTIONS                   AdverseEvent
43685-7     CLINICAL PHARMACOLOGY               PK or DrugInteraction
34068-7     DOSAGE AND ADMINISTRATION           Dosing
34073-7     DRUG INTERACTIONS                   DrugInteraction
34067-9     INDICATIONS AND USAGE               TextDescriptive
42229-5     SPL UNCLASSIFIED SECTION            (no signal)
43684-0     USE IN SPECIFIC POPULATIONS         Dosing or TextDescriptive
34076-0     CLINICAL STUDIES                    Efficacy
```

---

## Dictionaries Required

```
DICTIONARY           SIZE    SOURCE / NOTES
───────────────────  ──────  ─────────────────────────────────────
MedDRA PT            ~800    Top PTs observed in labels. Start with the
                             ~50 most common from the corpus and expand.
PK Parameters        ~25     Static list: Cmax, Cmin, Tmax, AUC (all forms),
                             t½, half-life, CL/F, CL, V/F, Vss, Vd, ke,
                             MRT, MAT, bioavailability, F(%)
Dosing Keywords      ~15     Static: dose, dosage, titration, starting,
                             maintenance, adjustment, recommended, initial,
                             maximum, loading, renal, hepatic
BMD Sites            ~10     Static: lumbar spine, femoral neck, total hip,
                             trochanter, ward, total body, distal radius
Tissue/Organ Names   ~30     Static: liver, kidney, lung, spleen, muscle,
                             fat, bone, skin, sputum, CSF, bile, synovial,
                             tonsil, prostate, aqueous humor, nasal mucosa
Drug Names           ~500    RxNorm active ingredient list or FDA Orange Book.
                             Cross-check against corpus (Efavirenz, Rifampin, etc.)
DDI Caption Keywords ~10     Static: drug interaction, co-administered,
                             coadministered, in the presence of, with/without
Canonical SOC Map    140→26  See normalization-rules.md §4
Known Unit Dict      ~80     See normalization-rules.md §3
```

---

## ML.NET Table Classifier (Tier 2)

For the ~28% of tables that land in Unclassified after Tier 1.

### Features (aggregated per TextTableID)

```
FEATURE                          TYPE    DESCRIPTION
───────────────────────────────  ──────  ───────────────────────────────
pct_meddra_pt_match              float   % ParameterNames in MedDRA dict
pct_pk_param_match               float   % ParameterNames in PK dict
pct_dosing_keyword               float   % ParameterNames with dose keywords
pct_drug_name_match              float   % ParameterNames in drug dict
pct_pvt_proportion               float   % PrimaryValueType = Proportion
pct_pvt_text                     float   % PrimaryValueType = Text
pct_pvt_mean_types               float   % PrimaryValueType ∈ {AM, GM, GMR}
pct_pvt_risk_types               float   % PrimaryValueType ∈ {HR, OR, RR, RD}
has_soc_categories               bool    Any ParameterCategory matches SOC
has_bounds                       bool    Any LowerBound populated
dominant_unit                    string  Most common Unit (categorical)
dominant_unit_is_pct             bool    dominant_unit = "%"
dominant_unit_is_conc            bool    dominant_unit matches conc pattern
row_count                        int     Rows in table
unique_param_count               int     Distinct ParameterNames
section_code                     string  ParentSectionCode (categorical)
caption_has_adverse              bool    Caption has "adverse"/"safety"
caption_has_pk                   bool    Caption has "pharmacokinetic"
caption_has_interaction          bool    Caption has "interaction"/"co-admin"
caption_has_efficacy             bool    Caption has "efficacy"/"survival"
caption_has_dose                 bool    Caption has "dose"/"dosage"/"titration"
```

### Algorithm

- **ML.NET trainer:** `LightGbmMulticlassTrainer`
- **Label:** TableCategory (from human-reviewed gold standard)
- **Split:** 70/15/15 stratified by type
- **Target metric:** Macro F1 ≥ 0.85
- **Goal:** Reduce Unclassified from ~28% to <10%
