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
SKIP                 Tables to exclude or that did not classify
```

### AdverseEvent — Multi-Population Variants

Some AE tables encode multiple study populations and/or within-table partitions:

- **Multi-StudyContext (colspan headers).** Top-level header groups arms by study
  population (e.g., "Adults" vs "Children and Adolescents"), each with its own
  N. Stored on observations as `StudyContext`. Comparator pairing groups by
  StudyContext so Adults arms compare against Adults placebo, not Children
  placebo. Reference: TextTableID 44661 (Clomipramine).

- **In-table subpopulations (mid-body N= rows).** A row label like "Female
  Patients Only" with arm cells of the form `(N=182)` introduces a new
  population partition for the rows that follow. Stored on observations as
  `Subpopulation`. AE parsers detect these rows generically (≥1 parsed N + label
  + dash-or-N cells), suppress them as observations, and override the per-arm
  `ArmN` for subsequent rows in that section. Reset on next SOC divider,
  structural-context row, or combined/all-patients row ("Male and Female
  Patients Combined", bare "Overall"/"Total").

`StudyContext`, `Population`, and `Subpopulation` are orthogonal: any combination
can coexist on a single row, and all three participate in the comparator group
key (see `column-contracts.md` AE Comparison Key and `normalization-rules.md`
§7.2).

---

## Decision Tree

Inputs: all rows sharing a TextTableID, plus Caption and ParentSectionCode.
Apply tests in order. First match wins.

```
TEST                                                           → ASSIGN
─────────────────────────────────────────────────────────────  ─────────────────
1. ≥3 ParameterNames match MedDRA PT dictionary                AdverseEvent
   OR ≥2 ParameterCategory values match canonical SOC

2. Any ParameterName in PK param dictionary:                   PK or DrugInteraction
     {Cmax, Tmax, AUC*, t½, CL/F, V/F, Vss, Vd,
      Clearance, half-life, ke, bioavailability}
   └─ Caption matches DDI keywords:                            → DrugInteraction
        drug interaction | co-administered | coadministered |
        in the presence of | with/without
   └─ ParameterName row labels are drug names (not PK params)  → DrugInteraction
   └─ Otherwise                                                → PK

3. PrimaryValueType set contains RelativeRiskReduction          Efficacy
   OR RiskDifference

4. ParentSectionCode fallback:
     34084-4 (Adverse Reactions)           → AdverseEvent
     43685-7 (Clinical Pharmacology)       → PK
     34073-7 (Drug Interactions)           → DrugInteraction
     34076-0 (Clinical Studies)            → Efficacy

5. No match                                                     SKIP
```

---

## ParentSectionCode Reference

Strong structural signal when content-based classification is ambiguous.

```
CODE        SECTION NAME                        LIKELY TABLECATEGORY
─────────   ──────────────────────────────────  ────────────────────
34084-4     ADVERSE REACTIONS                   AdverseEvent
43685-7     CLINICAL PHARMACOLOGY               PK or DrugInteraction
34073-7     DRUG INTERACTIONS                   DrugInteraction
42229-5     SPL UNCLASSIFIED SECTION            (no signal)
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
Drug Names           ~500    RxNorm active ingredient list or FDA Orange Book.
                             Cross-check against corpus (Efavirenz, Rifampin, etc.)
DDI Caption Keywords ~10     Static: drug interaction, co-administered,
                             coadministered, in the presence of, with/without
Canonical SOC Map    140→26  See normalization-rules.md §4
Known Unit Dict      ~80     See normalization-rules.md §3
```
