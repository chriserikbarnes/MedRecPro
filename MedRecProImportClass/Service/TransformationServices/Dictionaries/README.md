# Dictionaries

The shared **reference-data and canonicalization layer** for the table-parsing pipeline.
Every file here is a static, dependency-free leaf utility — no I/O, no database — consumed
*upward* by the parsers, the column standardizer, the validators, and the router. They
answer three kinds of question:

1. *Is this text a recognized PK parameter / PD marker / unit, and what is its canonical
   form?* — `PkParameterDictionary`, `UnitDictionary`, `PdMarkerDictionary`
2. *What validation rules and column contract apply to this table category?* —
   `CategoryProfile`, `CategoryProfileRegistry`
3. *How do I normalize a `TableCategory` string between its two naming conventions?* —
   `CategoryNameNormalizer`

> See the [parent README](../README.md) for the pipeline context. These dictionaries are the
> bottom of the dependency graph — nothing here depends on the parsers; the parsers depend on
> these.

---

## File index

| File | Responsibility |
|---|---|
| `PkParameterDictionary.cs` | Canonical PK parameter names (Cmax, AUC0-inf, t½, CL/F, …) with all alias spellings. Provides hit-checking, canonicalization, anchored-prefix matching, substring scanning, descriptive-phrase extraction, and SPL Unicode folding. |
| `UnitDictionary.cs` | Canonical PK/clinical units (`mcg/mL`, `ng·h/mL`, `h`, `%CV`, …). Recognition + normalization (`hr`→`h`, `ug`→`mcg`), extraction from cell/header text, plus unit *sets* exposed to `DoseExtractor` and `PopulationDetector`. |
| `PdMarkerDictionary.cs` | Small set of pharmacodynamic platelet-function markers (IPA, VASP-PRI, …) that sometimes leak into PK tables; lets callers flag (and keep) them rather than mis-treat them as PK. |
| `CategoryProfile.cs` | The `CategoryProfile` record — a per-category bundle of column contract + row-required fields + completeness fields + allowed value types + default bound type + the `UsesArmCoverage`/`UsesTimeConsistency` switches. |
| `CategoryProfileRegistry.cs` | Static registry holding one `CategoryProfile` per `TableCategory`. Accepts either category-name form; returns `CategoryProfile.Empty` for unknowns. |
| `CategoryNameNormalizer.cs` | Converts between the underscore form (`ADVERSE_EVENT`, used by `ParsedObservation.TableCategory`) and the documentation form (`AdverseEvent`, used for lookups). |

---

## Information flow

```
raw cell / header / category text
        │
        ├─ PkParameterDictionary.NormalizeUnicode()   fold ⋅∙•× → · and µ → μ before any lookup
        │     ├─ TryCanonicalize()      row label → canonical ParameterName   (PkTableParser, router)
        │     ├─ StartsWithPk()         DoseRegimen triage                     (ColumnStandardizationService)
        │     └─ ContainsPkParameter()  category confirmation                  (TableParserRouter)
        │
        ├─ PdMarkerDictionary.IsPdMarker()             flag mixed-in PD rows   (ColumnStandardizationService)
        │
        ├─ UnitDictionary.TryExtractFromCellText()     parser-time unit fill   (PkTableParser)
        │     ├─ TryExtractFromHeaderLikeText()        sub-header unit rows
        │     └─ TryNormalize()                         unit scrub             (ColumnStandardizationService)
        │
        └─ CategoryProfileRegistry.Get(tableCategory)
              │  (normalizes the key via CategoryNameNormalizer.Normalize)
              └─ CategoryProfile →
                   Contract            → ColumnStandardizationService (Phase 4 R/E/O/N)
                   RowRequiredFields   → RowValidationService
                   CompletenessFields  → RowValidationService
                   AllowedValueTypes   → RowValidationService
                   DefaultBoundType    → ColumnStandardizationService (Phase 4 bound fill)
                   UsesArmCoverage     → TableValidationService
                   UsesTimeConsistency → TableValidationService
```

---

## How the files relate

- **`CategoryNameNormalizer` feeds `CategoryProfileRegistry`.** The registry normalizes every
  incoming key through it, so both `"ADVERSE_EVENT"` and `"AdverseEvent"` resolve. It also
  feeds `ColumnContractRegistry` (in `BaseTableFlattening`) the same way.
- **`CategoryProfileRegistry` composes `CategoryProfile` records**, sourcing each profile's
  `Contract` from a `ColumnContractRegistry`. The profile consolidates what were once six
  parallel per-category dictionaries scattered across the standardizer and validators.
- **`PkParameterDictionary` and `UnitDictionary` share Unicode normalization** —
  `UnitDictionary` delegates to `PkParameterDictionary.NormalizeUnicode` rather than
  duplicating the fold logic.
- **`PdMarkerDictionary` complements `PkParameterDictionary`** — PD markers are deliberately
  *excluded* from the PK dictionary, so PK-row processing checks both to tell true PK metrics
  from mixed-in PD content.

External consumers: `PkTableParser`, `ColumnStandardizationService`, `RowValidationService`,
`TableValidationService`, `DoseExtractor`, `PopulationDetector`, and `TableParserRouter` — all
in [`BaseTableFlattening`](../BaseTableFlattening/README.md).

---

## What the dictionaries actually map

- **`PkParameterDictionary`** — each `PkEntry` has one canonical, a list of aliases, and an
  anchored prefix regex. `TryCanonicalize` runs four passes: exact alias (parens intact) →
  strip trailing `(unit)` and retry → prefix-regex scan → strip biological-matrix prefix
  (`Serum`/`Plasma`/`Blood`) and retry. Example collapses: `C max`, `Cmax,ss`, `Peak Plasma
  Concentration` → `Cmax`; `AUC(0-∞)`, `AUCinf` → `AUC0-inf`; non-standard intervals
  (`AUC48`, `AUC72`) → generic `AUC`.
- **`UnitDictionary`** — `KnownUnits` (~65 canonical tokens), `NormalizationMap` (~50 variant
  → canonical, e.g. `hr`/`hrs`/`hours` → `h`, `ug/mL` → `mcg/mL`, `mcg*h/mL` → `mcg·h/mL`),
  plus a structural fallback regex for valid-but-unenumerated tokens, and dose/clinical unit
  sets for other callers.
- **`CategoryProfile` / `CategoryProfileRegistry`** — keyed by documentation-form category.
  AdverseEvent/Efficacy require `{ParameterName, TreatmentArm}`, default bound `95CI`, use
  arm-coverage; PK requires `{ParameterName, DoseRegimen}`, default bound `90CI`, uses
  time-consistency. Each profile also lists the `PrimaryValueType` values allowed for that
  category.

---

## Gotchas

- **`NormalizationMap` wins over `KnownUnits` in `TryNormalize`** — `hr` is in `KnownUnits`
  but normalizes to `h`. Callers probing `KnownUnits` directly will see different behavior.
- **Unicode is treacherous.** SPL tables use at least five different "multiplication dot"
  codepoints in AUC units and two visually identical micro/mu signs. All dictionary keys use
  the post-fold form, so any caller that skips `NormalizeUnicode` will miss entries.
- **First-writer-wins on PK aliases.** If two canonicals declare the same alias, the first
  declared entry keeps it. Entry declaration order in the dictionary is therefore
  semantically meaningful for overlapping aliases.
- **`CategoryProfile.RowRequiredFields` is intentionally narrower than `Contract.Required`** —
  PK row validation requires only `{ParameterName, DoseRegimen}` even though the full column
  contract also requires value/type/unit. Keeping them separate preserves historical
  validation behavior.
- **`PkParameterDictionary.TryCanonicalize` keeps parentheses on the first pass** so
  `AUC(0-inf)` resolves as its own alias before the second pass strips the suffix and would
  otherwise collapse it to generic `AUC`.
