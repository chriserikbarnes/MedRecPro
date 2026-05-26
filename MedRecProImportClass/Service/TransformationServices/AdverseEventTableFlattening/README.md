# AdverseEventTableFlattening

**Stage 5** of the pipeline: AdverseEvent statistical denormalization. This folder reads the
already-parsed, already-standardized AE rows from `tmp_FlattenedStandardizedTable`
(`TableCategory = 'ADVERSE_EVENT'`) and produces **`tmp_FlattenedAdverseEventTable`**, where
every row carries a pre-computed risk-statistics packet. After that table is populated,
the service materializes **`tmp_FlattenedAdverseEventRiskTable`** from `dbo.vw_AeRisk`
so visualizations bind to numbers directly — no runtime statistics.

Stage 5 now persists only rows with a non-null `RR`. It still calculates intermediate
diagnostics for skipped rows in memory, logs the null-RR reason families per batch, and
keeps only visualization-ready treatment/comparator pairs in the target table.

Each output row pairs a treatment arm against a chosen comparator arm and stores:
- **RR** — Relative Risk, with 95% CI (`RRLowerBound` / `RRUpperBound`)
- **DNRR** — Dose-Normalized RR, with 95% CI
- **IsPlaceboControlled** — 1 iff the chosen comparator was a placebo/sham/vehicle/zero-dose arm
- **ComparatorArm / ComparatorN**, **EventsTreatment / EventsComparator**
- **CalculationMethod** (`KATZ_LOG` when RR was produced) and **CalculationFlags** (a
  semicolon-delimited audit of every guard that fired)

The `Log*` companion columns (`LogRR`, `LogDNRR`, and their bounds) are **SQL Server
PERSISTED computed columns** — they are materialized by the database, never written by C#.
The risk table is a persistent snapshot of `dbo.vw_AeRisk`; SQL Server builds it with an
explicit-column `INSERT INTO ... SELECT ... FROM dbo.vw_AeRisk` after all AE batches save.

> See the [parent README](../README.md) for where Stage 5 sits in the overall flow. It runs
> once at the end of `ProcessAllAsync` / `ProcessAllWithValidationAsync`, after Stage 3 (and
> Stage 4) have populated the standardized table — and only when an
> `IAdverseEventDenormalizationService` is registered.

---

## Information flow

```
AdverseEventDenormalizationService.PopulateAsync
  ├─ TruncateAsync                              wipe both Stage 5 output tables (idempotent rerun)
  ├─ SELECT DISTINCT DocumentGUID  WHERE TableCategory='ADVERSE_EVENT'
  └─ for each batch of documents → processBatchAsync:
        load standardized rows (AsNoTracking)
        │
        ├─ SourceRowEligibility.IsDenormalizableAeSourceRow   keep only valid AE rows w/ a value
        ├─ AeMeddraTermStandardizer.Standardize              canonicalize AE name + official SOC
        │
        └─ group by DocumentGUID → group by TextTableID:
              ├─ RelativeRiskCalculator.ClassifyTrialDesign   (diagnostic flag only)
              └─ ComparatorGrouper.Group                       cohorts keyed by 5-tuple
                   for each cohort:
                     applySameArmNBackfill                     in-memory ArmN repair
                     ComparatorSelector.Select                 → (comparator row, flag)
                     ComparatorSelector.SelectReferenceDose     → (dRef, dRefUnit) = MIN(Dose>0)
                     for each NON-comparator row:
                       AeStatEntityBuilder.Build
                         ├─ RelativeRiskCalculator.DeriveEventCount   PrimaryValue → event count
                         ├─ RelativeRiskCalculator.Compute            RR + 95% CI (Katz log)
                         └─ RelativeRiskCalculator.ComputeDnrr        DNRR + 95% CI (log-linear)
                       → FlattenedAdverseEventTable entity
        AddRange → SaveChangesAsync → ChangeTracker.Clear()
  └─ materializeRiskTableAsync                 insert explicit-column SELECT from dbo.vw_AeRisk
```

The comparator row itself is **excluded** from the output; its identity/N appear on the
treatment rows as `ComparatorArm` / `ComparatorN`.

---

## File index

| File | Responsibility |
|---|---|
| `IAdverseEventDenormalizationService.cs` / `AdverseEventDenormalizationService.cs` | Orchestrates the stage. Public surface: `PopulateAsync` (truncate → batch by document → group → compute → bulk insert → materialize risk table) and `TruncateAsync`. Disables EF change-tracking for bulk insert; fails fast on save errors. |
| `SourceRowEligibility.cs` | The eligibility gate. A source row is denormalizable iff it has a DocumentGUID, a `PrimaryValue`, and a `ParameterName`/`TreatmentArm` that is not a caption, body-system label, threshold fragment, or value-axis token (delegates to `AeColumnContextResolver`). |
| `AeMeddraTermStandardizer.cs` | Stage 5-only MedDRA standardizer. Canonicalizes AE names before grouping, maps category aliases to the official 27 SOC labels, fills null categories from known AE terms, and emits auditable `AE_STD:*` flags. |
| `ComparatorGrouper.cs` | Groups one table's rows into comparison cohorts keyed by `{ParameterName, ParameterSubtype, StudyContext, Population, Subpopulation}` — each dimension normalized (trim, collapse whitespace, upper-invariant). Scope is per-`TextTableID`. |
| `ComparatorSelector.cs` | Picks the comparator for a cohort via a 3-tier cascade and computes the per-study reference dose. |
| `IPlaceboArmClassifier.cs` / `PlaceboArmClassifier.cs` | Thin injectable wrapper over `RelativeRiskCalculator.IsPlaceboArm`, so upstream services (e.g., Claude guardrails) can share placebo classification. |
| `RelativeRiskCalculator.cs` | The math core (static, side-effect free): event-count derivation, RR + CI (Katz log with Haldane-Anscombe zero-cell correction), DNRR + CI (log-linear), trial-design classification, placebo detection. |
| `AeStatEntityBuilder.cs` | Assembles one output entity from a source row + comparator context + computed stats, accumulating `CalculationFlags` and applying early-exit guards. |
| `AeDenormalizationConstants.cs` | Centralized flag/method string constants (`KATZ_LOG`, `PLACEBO_COMPARATOR`, `NO_ARMN`, `AE_ARMN_STAGE5_GROUP_BACKFILL`, …). |

---

## The statistics (RelativeRiskCalculator)

This is the part to get right. All methods are static and pure.

**1. Event count** — `DeriveEventCount(primaryValue, primaryValueType, armN)`:
- `Percentage` → `events = armN * value / 100` (guards: value > 100 → `PERCENT_OUT_OF_RANGE`; missing N → `NO_ARMN`)
- `Count` → `events = value` (direct)
- anything else (`Numeric`, `Mean`, `Median`, `Ratio`, …) → null + `UNCOMPARABLE_VALUE_TYPE`

`Numeric` is deliberately rejected: upstream assigns it to means/medians/ratios without
committing to count semantics. Only `Count` and `Percentage` are comparable.

**2. RR + 95% CI** — `Compute(eventsT, armN, eventsC, comparatorN)`, the Katz log method:
- Guards return all-null stats with a flag: `NO_ARMN`, `NO_COMPARATOR_N`,
  `INVALID_EVENT_COUNT`, `EVENTS_EXCEED_ARMN`.
- **Zero-cell (Haldane-Anscombe) correction**: if either event count is 0, add 0.5 to each
  event count and 1 to each N (`ZERO_CELL_CORRECTED`). Applied to both the point estimate
  and the CI; raw counts are still stored for audit.
- `RR = (a'/n1') / (c'/n2')`; `SE_ln = sqrt(1/a' − 1/n1' + 1/c' − 1/n2')`;
  `RR_bounds = exp(ln(RR) ± 1.95996 · SE_ln)`.

**3. DNRR + 95% CI** — `ComputeDnrr(rr, rowDose, rowDoseUnit, dRef, dRefUnit)`, log-linear:
- `DNRR = exp(ln(RR) / ln(rowDose / dRef))`, bounds analogously.
- Skips (null DNRR, with a diagnostic flag): `NO_DOSE_RANGE` (no positive reference dose),
  `DOSE_UNIT_MISMATCH`, and `IS_REFERENCE_DOSE` when `rowDose == dRef` (the reference row
  itself: `ln(1)=0` is undefined, so DNRR is intentionally suppressed).
- Defensive bound swap when the dose ratio is < 1 so `lower ≤ upper`.

---

## Comparator selection & IsPlaceboControlled

`ComparatorSelector.Select` is a 3-tier cascade over a cohort:
1. **Placebo** (`IsPlaceboArm` true) → `PLACEBO_COMPARATOR`
2. else **lowest non-zero dose** (only if the cohort has >1 row) → `LOW_DOSE_COMPARATOR`
3. else → `NO_COMPARATOR`

Ties break deterministically by Dose → SourceRowSeq → SourceCellSeq → Id.

`IsPlaceboArm(arm, dose)`: true if `dose == 0`, else regex `placebo|sham|vehicle` on the arm name.

**`IsPlaceboControlled` is strictly per-row**: set to 1 only when Tier 1 fired
(`comparatorFlag == "PLACEBO_COMPARATOR"`). It is *not* derived from the table-level trial
design — so within one table some rows can be 1 and others 0, depending on which comparator
each cohort resolved to. `ClassifyTrialDesign` exists for diagnostics only (it contributes
the `AMBIGUOUS_TRIAL_DESIGN` flag) and no longer drives the persisted bit.

`SelectReferenceDose` returns `MIN(Dose) WHERE Dose > 0` over the cohort — the intra-study
reference dose `dRef` used for DNRR.

---

## Entity building & the C#/DB boundary

`AeStatEntityBuilder.Build` projects the source row's identity columns (DocumentGUID, UNII,
ParameterName, ParameterCategory, ArmN, Dose/DoseUnit, PrimaryValue/Type, StudyContext,
Population, Subpopulation, TreatmentArm), stamps the comparator context, then computes
stats with early exits — each guard appends to `CalculationFlags` and returns partial/null
stats:

`MIXED_VALUE_TYPES` (treatment vs comparator type differ) → `UNCOMPARABLE_VALUE_TYPE` →
`INVALID_EVENT_COUNT` / `PERCENT_OUT_OF_RANGE` → `NO_ARMN` / `NO_COMPARATOR_N` (for
percentages) → event counts → `Compute` (RR) → `ComputeDnrr` (DNRR).

`CalculationMethod = "KATZ_LOG"` iff RR was produced; `CalculationFlags` is always set.
The **`Log*` columns are never set in C#** — they are PERSISTED computed columns in the
DDL, guarded with `CASE WHEN > 0 THEN LOG(...)` to avoid `LOG(0)`/`LOG(NULL)` errors.

Rows that finish with `RR = NULL` are not inserted into `tmp_FlattenedAdverseEventTable`.
This suppresses non-visualizable rows such as `NO_COMPARATOR`, `NO_ARMN`,
`NO_COMPARATOR_N`, `MIXED_VALUE_TYPES`, `UNCOMPARABLE_VALUE_TYPE`,
`INVALID_EVENT_COUNT`, `EVENTS_EXCEED_ARMN`, and `PERCENT_OUT_OF_RANGE` outputs.

---

## Relationships

| Interface | Implementation |
|---|---|
| `IAdverseEventDenormalizationService` | `AdverseEventDenormalizationService` (DI entry point) |
| `IPlaceboArmClassifier` | `PlaceboArmClassifier` (delegates to the static calculator) |

`AdverseEventDenormalizationService` composes all the rest: `SourceRowEligibility` (filter)
→ `ComparatorGrouper` (cohorts) → `ComparatorSelector` (comparator + dRef) →
`AeStatEntityBuilder` (entity) → `RelativeRiskCalculator` (math). `AeDenormalizationConstants`
supplies the flag strings used across all of them. The only cross-folder dependency is
`AeColumnContextResolver` (in [`BaseTableFlattening`](../BaseTableFlattening/README.md)),
used by `SourceRowEligibility` to reject structural arm/parameter text.

`AeMeddraTermStandardizer` intentionally uses the existing Phase 2
`IAeParameterCategoryDictionaryService` as a seed without changing Phase 2 fill-only
behavior. Stage 5 applies the stronger name-authoritative SOC alignment only to the
in-memory rows that feed `ComparatorGrouper`.

---

## Gotchas

- **Like-typed pairing only.** RR is computed only when treatment and comparator share the
  same `PrimaryValueType`; otherwise `MIXED_VALUE_TYPES` and null stats — even if the numbers
  would divide cleanly. `PrimaryValueType` is copied verbatim from source, never re-derived.
- **The reference-dose row keeps its RR but gets null DNRR** (`IS_REFERENCE_DOSE`).
- **Null-RR rows are filtered after build.** Reference-dose rows with valid RR and null
  DNRR still persist; rows with no RR do not.
- **AE names and SOCs are standardized before grouping.** Name-derived SOCs override
  conflicting raw categories for known AE terms, and all persisted categories should be
  in the official 27 MedDRA SOC set.
- **Zero-cell correction changes the stored RR/CI** (corrected values), while raw event
  counts are preserved in `EventsTreatment`/`EventsComparator` and the row is flagged.
- **ArmN backfill is in-memory only.** `applySameArmNBackfill` fills a missing ArmN from a
  unique same-arm N within the cohort (`AE_ARMN_STAGE5_GROUP_BACKFILL`); conflicting Ns are
  rejected (`AE_ARMN_REJECTED_CONFLICTING_N`). The fix never flows back to the source table.
- **Idempotent + fail-fast.** `PopulateAsync` truncates first, so reruns are clean; any save
  error clears the tracker and rethrows (a partial denormalized table is worse than none).
- **`TruncateAsync` falls back** to `RemoveRange` + `SaveChanges` on the EF InMemory test
  provider, which does not support raw `TRUNCATE`.
