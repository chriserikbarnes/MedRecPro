# SampleSize

Resolves the **per-arm N** (number of subjects) for a treatment arm — the denominator that
turns an adverse-event count or percentage into a comparable rate. SPL tables encode N in
many places (arm headers, standalone metadata rows, multi-row header tiers, `x/N` fraction
cells, inline `(n=…)` suffixes, count/percent math, captions, footnotes), so this folder
collects evidence from all of them and then applies a strict precedence to pick one value.

The design enforces a clean split:
- **`SampleSizeParser`** recognizes *syntax* — what text looks like an N value.
- **`SampleSizeEvidence`** carries the *structured result*.
- **`ArmNResolver`** makes the *policy* decision — which evidence wins, and which flag to stamp.

The diagnostic flag prefix throughout is `AE_ARMN_*`; the primary consumers are the AE
parsing path and Stage 5 denormalization.

> See the [parent README](../README.md) for pipeline context. This folder is a leaf utility
> called by the parsers (via `BaseTableParser`/`ArmMetadataEnrichmentService` in
> [`BaseTableFlattening`](../BaseTableFlattening/README.md)).

---

## File index

| File | Responsibility |
|---|---|
| `SampleSizeSourceKind.cs` | Enum of the 10 places N evidence can come from: `ArmHeader`, `ColumnHeader`, `HeaderTier`, `BodyMetadataRow`, `FractionDenominator`, `InlineValueSuffix`, `CountPercentInference`, `CaptionOrFootnote`, `RangeOnly`, `Stage5GroupBackfill`. |
| `SampleSizeEvidence.cs` | The structured result record (Value, IsExact, SourceKind, RawText, CleanedText, row/column index, ArmCandidate, FormatHint, DiagnosticCode/Reason). Three factories: `Exact`, `Inexact`, `Rejected`. |
| `SampleSizeParser.cs` | All regex-based syntax recognition. ~7 `TryParse*` entry points (arm header, standalone cell, header tier, fraction denominator, inline suffix, range-only) plus `TryInferCountPercentSampleSize`. Decides nothing about assignment. |
| `ArmNResolver.cs` | The policy layer. `ResolveForAeObservation` chooses the single best N from all available evidence and returns an `ArmNResolution(ArmN, ValidationFlag)`. Small and deterministic. |

---

## Information flow

```
table reconstruction
   ├─ arm column headers ──► SampleSizeParser.TryParseArmHeaderSampleSize  → Evidence{ArmHeader}    → ArmDefinition.SampleSize
   ├─ header tier cells  ──► SampleSizeParser.TryParseHeaderTierSampleSize → Evidence{HeaderTier}
   ├─ body metadata rows ──► SampleSizeParser.TryParseStandaloneSampleSizeCell → Evidence{BodyMetadataRow} → scopedMetadataN
   ├─ fraction cells x/N ──► SampleSizeParser.TryParseFractionDenominator  → Evidence{FractionDenominator} → ParsedValue
   ├─ inline (n=45)      ──► SampleSizeParser.TryStripInlineSampleSize     → Evidence{InlineValueSuffix}   → ParsedValue
   ├─ count+percent pair ──► SampleSizeParser.TryInferCountPercentSampleSize → Evidence{CountPercentInference}
   └─ range "45-98"      ──► SampleSizeParser.TryParseRangeOnlySampleSize  → Evidence{RangeOnly, Rejected}

   all channels converge at:
        ArmNResolver.ResolveForAeObservation(arm, parsedValue, scopedMetadataN, existingArmN)
              → ArmNResolution{ ArmN, ValidationFlag = "AE_ARMN_FROM_*" }
              → ParsedObservation.ArmN
```

`SampleSizeParser` is the sole producer of `SampleSizeEvidence`; `ArmNResolver` is the sole
component that writes `ParsedObservation.ArmN`. `BuildValueContextArm` lets a parser carry a
row-scoped N override into per-cell resolution without mutating the original `ArmDefinition`.

---

## Precedence (ArmNResolver)

When evidence disagrees, `ResolveForAeObservation` picks in this fixed order (highest first):

1. **`scopedMetadataN`** — a body metadata row / header-tier N scoped to the current row.
   Beats everything, including the arm header. Flag `AE_ARMN_FROM_METADATA_ROW`. (A mid-body
   `n=89` is treated as a more specific override than a header-level arm N.)
2. **`arm.SampleSize`** — N parsed from the arm column header. Flag `AE_ARMN_FROM_HEADER_N`.
3. **`ParsedValue.SampleSize`** — cell-local evidence (fraction denominator, inline suffix,
   count/percent inference) — used only when no contextual N exists. Flag depends on the
   parse rule: `AE_ARMN_FROM_FRACTION_DENOMINATOR`, `AE_ARMN_FROM_COUNT_PERCENT_INFERENCE`,
   or `AE_ARMN_FROM_INLINE_SUFFIX`.
4. **`existingArmN`** — a prior assignment, kept as a last resort (no flag).
5. **`null`** — nothing found.

**Conflict rule:** if the chosen contextual N (1 or 2) disagrees with the cell-local N (3),
the contextual N is *kept* but the row is flagged `AE_ARMN_REJECTED_CONFLICTING_N`; the
cell-local N is discarded.

---

## Relationships

- **`SampleSizeSourceKind` is a discriminant on `SampleSizeEvidence`** — there is no type
  hierarchy; all three factory methods return the same record, and callers branch on
  `SourceKind` + `IsExact`.
- **`SampleSizeParser` → `SampleSizeEvidence` → `ArmNResolver`** is a strict producer →
  carrier → consumer chain. The parser never assigns; the resolver never re-parses.
- **`Stage5GroupBackfill` is defined here but implemented elsewhere** — the enum member tags
  rows whose ArmN was filled by the cross-row backfill in
  [`AdverseEventTableFlattening`](../AdverseEventTableFlattening/README.md), not by this folder.

---

## Gotchas

- **`TryInferCountPercentSampleSize` tolerance is ±1.5 points.** It accepts a sibling N when
  the back-calculated percentage rounds within 1.5 of the reported value. If two or more
  siblings qualify, it returns `Rejected` (`AE_ARMN_REJECTED_CONFLICTING_N`) rather than
  guessing — but still returns `true` so the diagnostic surfaces.
- **`RangeOnly` evidence is rejected at parse time**, not by the resolver. A `45-98` range
  produces `Inexact`/`Rejected` evidence with `AE_ARMN_REJECTED_RANGE_ONLY` and can never
  flow to `ArmN`; it exists only for audit visibility.
- **Two inline parsers, different scopes.** `TryParseInlineSampleSizeSuffix` requires the
  annotation at the very end of the string; `TryStripInlineSampleSize` is broader (standalone,
  bracket-anywhere, and bare-trailing forms, the last emitting `BARE_TRAILING_N`).
- **Patterns are tolerant** of `N`/`n` case, an optional footnote `*` after `N`, unit context
  words (`patients`/`subjects`/`eyes`), and comma-formatted integers — but `RangeOnly`
  deliberately is not, so ranges do not masquerade as exact Ns.
