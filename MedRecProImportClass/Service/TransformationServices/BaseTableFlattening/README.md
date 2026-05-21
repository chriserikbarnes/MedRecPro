# BaseTableFlattening

The engine of the SPL Table Normalization pipeline. Everything that turns raw label table
cells into validated rows of **`tmp_FlattenedStandardizedTable`** lives here — Stage 1
(cell context) through Stage 4 (validation), plus the Stage 0 dedup filter and the
orchestrator that drives the whole run.

> New to the subsystem? Read the [parent README](../README.md) first for the staged
> pipeline overview and the core data contracts (`ReconstructedTable`, `ParsedObservation`,
> `TableCategory`). This document drills into the ~50 files in this folder.

---

## Information flow inside this folder

```
TableParsingOrchestrator  ── drives every stage, batches by DocumentGUID (UNII order) ──┐
                                                                                        │
 Stage 0  BioequivalentLabelDedupService   prune to one canonical label per product     │
 Stage 1  TableCellContextService          EF query → flat TableCellContext rows         │
 Stage 2  TableReconstructionService       → ReconstructedTable (spans, headers, rows)   │
          (+ ReconstructedTableExtensions)                                               │
 Stage 3  TableParserRouter.Route          pick TableCategory + ITableParser             │
            │                                                                            │
            ├─ pre-parse context (used by parsers):                                      │
            │    AeColumnContextResolver · ArmDefinitionExtractor                        │
            │    ArmMetadataEnrichmentService · PopulationDetector                       │
            │    AeParameterCategoryDictionaryService                                    │
            │                                                                            │
            └─ ITableParser.Parse  (BaseTableParser + 5 concrete parsers)                │
                 uses ValueParser (decompose cell) + DoseExtractor (dose)                │
                 uses StructuralRowSuppressionService (drop scaffold rows)               │
                 → List<ParsedObservation>                                               │
 Stage 3.25 ColumnStandardizationService   Phase 1→4 deterministic cleanup               │
            (Phase1ArmContextPipeline, Phase2ContentNormalizationPipeline)               │
            uses ColumnContractRegistry                                                  │
 Stage 3.4  QCNetCorrectionService          ML.NET correction; ParseQualityService score │
            (+ QCTrainingStore persistence)                                              │
 Stage 3.5  ClaudeApiCorrectionService      AI correction of low-quality rows            │
            (+ ClaudeCorrectionGuardrails)                                               │
 Stage 3.6  ColumnStandardizationService.PostProcessExtraction                           │
            ↓ DB write ─────────────────────────────────────────────────────────────────┘
 Stage 4  RowValidationService → TableValidationService → BatchValidationService
```

---

## File index

### Orchestration & routing
| File | Responsibility |
|---|---|
| `ITableParsingOrchestrator.cs` / `TableParsingOrchestrator.cs` | The pipeline driver. Owns the Stage 0→5 sequence, the UNII-ordered batch loop, parser dispatch, all the inter-stage filters, and the DB write. Optional services (validation, ML, Claude, dedup, AE denorm) are null-checked at each stage. |
| `ITableParserRouter.cs` / `TableParserRouter.cs` | Maps a `ReconstructedTable` to a `TableCategory` (via LOINC `ParentSectionCode`, caption/section keywords, and DDI detection) and selects the best `ITableParser` by priority + `CanParse`. Emits skip/downgrade reasons. |
| `ITableParserDiagnostics.cs` / `ITableParserRouterDiagnostics.cs` | Optional diagnostic surfaces — suppressed-row audit records and the last route reason — kept off the primary parse/route contracts. |

### Cell context & reconstruction (Stages 1–2)
| File | Responsibility |
|---|---|
| `ITableCellContextService.cs` / `TableCellContextService.cs` | Stage 1. Multi-join EF Core query producing the flat `TableCellContext` projection (cell + row + table + section nav + document + UNII). Also serves ID ranges and UNII-ordered DocumentGUID lists for batching. |
| `ITableReconstructionService.cs` / `TableReconstructionService.cs` | Stage 2. Rebuilds the physical grid: HTML cleanup, footnote-marker extraction, row classification, colspan/rowspan occupancy resolution, multi-level header path building. Produces `ReconstructedTable`. |
| `ReconstructedTableExtensions.cs` | Public `DataRows()` / `CellAt()` helpers mirroring `BaseTableParser`'s protected versions, so non-parser callers (e.g., the router) can navigate a reconstructed table. |

### Pre-parse context resolution (consumed by parsers)
| File | Responsibility |
|---|---|
| `AeColumnContextResolver.cs` | The core column classifier. Decides whether a header column is a `TreatmentArm`, a `PairedSubcolumn` (inheriting an arm from a parent/sibling header), a structural value-axis, or unresolved — and supplies the `SUPPRESSED_AE_*` reasons. |
| `ArmDefinitionExtractor.cs` | Builds `ArmDefinition`s from the header (name, column index, inline N, study context, subtype, format hint), with sibling/parent recovery and a conservative single-product-from-title fallback. |
| `ArmMetadataEnrichmentService.cs` | Scans the first few body rows for header-continuation metadata (dose lines, N= rows, arm-name rows, format hints) and votes it onto the arm definitions. |
| `PopulationDetector.cs` | Two levels: whole-table `Population` (from caption/section title, cross-validated) and per-row `Subpopulation` (`TryMatchLabel` against an ~80-entry dictionary + anchored regex for age/weight/renal/CYP bands). |
| `IAeParameterCategoryDictionaryService.cs` / `AeParameterCategoryDictionaryService.cs` | Two static maps: ParameterName → canonical MedDRA SOC (~1,189 entries) and variant → canonical ParameterName (~50). Fills missing `ParameterCategory` and normalizes event names; fills NULL only (never overwrites). |

### Parsers (Stage 3)
| File | Responsibility |
|---|---|
| `ITableParser.cs` | The parser contract: `SupportedCategory`, `Priority` (lower tried first), `CanParse` (structural gate), `Parse`. |
| `BaseTableParser.cs` | Abstract base for all parsers. Provides the shared machinery: arm extraction, base-observation creation, the `parseAdverseEventArmRows` template loop, value parsing with AE/Efficacy promotions, caption-hint detection, subpopulation tracking, fault-tolerant per-row parsing, and suppression audit recording. |
| `MultilevelAeTableParser.cs` | AE, priority 10. Tables with ≥2 header rows (colspan study-context over arm sub-headers). |
| `AeWithSocTableParser.cs` | AE, priority 20. Tables with SOC (System Organ Class) divider rows; propagates SOC into `ParameterCategory`. |
| `SimpleArmTableParser.cs` | AE, priority 30 (always `CanParse` — the AE fallback). Single-header arm tables; also serves the EFFICACY shape via an internal category parameter. Splits off stat columns (p-value, RR/CI, ARR) as comparison rows. |
| `EfficacyMultilevelTableParser.cs` | EFFICACY, priority 10. Multi-level headers, stat columns, mid-body `n=` declaration rows, and column sub-header rows that override value type/unit. |
| `PkTableParser.cs` | PK, priority 10 (the only PK parser). The most complex parser: parameters in columns / doses in rows, with transposed-layout swap, compound multi-study headers, sticky dose qualifiers, sub-header unit rows, and a sibling-unit vote. |
| `ValueParser.cs` | Static, context-neutral cell decomposer. Tries ~20 patterns in priority order (fraction%, n(%), value±SD, value(CI), ranges, p-values, trailing-unit, plain number, …) → a typed `ParsedValue`. |
| `DoseExtractor.cs` | Static. Extracts `(Dose, DoseUnit)` from free text, normalizes units, handles ranges/titration and BID/TID promotion, scans all columns for embedded doses, and backfills placebo arms to Dose=0. |

### Column standardization (Stage 3.25)
| File | Responsibility |
|---|---|
| `IColumnStandardizationService.cs` / `ColumnStandardizationService.cs` | The deterministic cleanup service (a large `partial class`). Hosts Phases 1–4 plus the Stage 3.6 `PostProcessExtraction`. Loads a drug-name dictionary from `vw_ProductsByIngredient` on init. |
| `ColumnStandardizationPhase1Pipeline.cs` | Defines the nested `Phase1ArmContextPipeline`. Owns ordering only — its delegate arrays point back to `applyRule1..11` on the service. Corrects misclassified `TreatmentArm`/`StudyContext` (AE + EFFICACY): rules 1–6 first-match, 7–10 always-run, 11 pre-chain `[N=]` extraction. |
| `ColumnStandardizationPhase2Pipeline.cs` | Defines the nested `Phase2ContentNormalizationPipeline`. Ten ordered passes (all categories): inline-N strip, DoseRegimen routing, ParameterName/TreatmentArm cleanup, subtype-unit extraction, PK canonicalization, unit scrub, SOC mapping, AE dictionary fill, all-column dose scan. Ordering matters — later passes depend on earlier column moves. |
| `IColumnContractRegistry.cs` / `ColumnContractRegistry.cs` | Per-`TableCategory` column contract: which of the 41 columns are Required / Expected / Optional / NullExpected. Consumed by Phase 4 (null-out + missing-flag) and by parse-quality scoring. |
| `IParseQualityService.cs` / `ParseQualityService.cs` | Multiplicative penalty model → a 0–1 quality score + reason list per row. The score gates Claude forwarding; replaced a retired PCA anomaly pipeline. |

### ML QC correction (Stage 3.4)
| File | Responsibility |
|---|---|
| `IQCNetCorrectionService.cs` / `QCNetCorrectionService.cs` | Three ML.NET multiclass classifiers: TableCategory validation (default *shadow-only*), DoseRegimen routing, and PrimaryValueType disambiguation. Emits `QC:*` flags, scores parse quality, accumulates training rows, and retrains on thresholds. |
| `IQCTrainingStore.cs` / `QCTrainingStore.cs` | File-backed JSON persistence of training records (bootstrap + Claude ground-truth), with atomic writes, row/size eviction caps, and reload-on-init so the model survives restarts. |

### Claude AI correction (Stage 3.5)
| File | Responsibility |
|---|---|
| `ClaudeApiCorrectionService.cs` | Sends only low-quality rows (grouped by `TextTableID`, with the original table rendering) to the Anthropic Messages API; applies returned field corrections from a static allowlist; salvages truncated responses; feeds accepted corrections back to ML. |
| `ClaudeCorrectionGuardrails.cs` | A stateless Chain-of-Responsibility of 8 guardrails (first rejection wins) that veto unsafe corrections — placebo-class flips, arm-nulling, body-system/header-token arms, ParameterName token-supersets, percent-column type demotions. Accepted → `AI_CORRECTED:*`; vetoed → `AI_REJECTED:*`. |

### Validation (Stage 4) + suppression + dedup
| File | Responsibility |
|---|---|
| `IRowValidationService.cs` / `RowValidationService.cs` | Per-row checks (orphan, missing required, bound inversion, missing ArmN, time consistency, low confidence). Appends flags and writes `AdjustedConfidence`. Only this level can emit `Error`. |
| `ITableValidationService.cs` / `TableValidationService.cs` | Per-`TextTableID` checks (duplicate observations, arm-coverage gaps, count deviation, mixed time presence). Always `Warning`/`Valid`. |
| `IBatchValidationService.cs` / `BatchValidationService.cs` | Composes the two above over a batch or the whole DB table; produces the `BatchValidationReport` (confidence bands, per-category/parse-rule counts, skip reasons) and the cross-version concordance check. In-memory only — never persisted. |
| `StructuralRowSuppressionService.cs` | Decides, *during parsing*, whether an AE/EFFICACY row is scaffold (SOC labels, header echoes, placeholder cells, degenerate rows) and should never be emitted. Records a `TableSuppressionAuditRecord` for each suppression. |
| `IBioequivalentLabelDedupService.cs` / `BioequivalentLabelDedupService.cs` | Stage 0. Collapses the document set so each `(Ingredient, DosageForm, Route)` group keeps one canonical label (NDA-preferred, most-recent), preventing 40× count inflation from genericized drugs. Operates on DocumentGUIDs only. |

---

## How the pieces relate

### The parser contract is the spine of Stage 3
Every parser implements `ITableParser` and extends `BaseTableParser`. The router groups
registered parsers by `SupportedCategory`, sorts by `Priority` (ascending = more specific
first), and picks the first whose `CanParse` returns true:

```
ITableParser
  └── BaseTableParser (abstract; all shared logic)
        ├── MultilevelAeTableParser        [ADVERSE_EVENT, prio 10]
        ├── AeWithSocTableParser           [ADVERSE_EVENT, prio 20]
        ├── SimpleArmTableParser           [ADVERSE_EVENT, prio 30 — fallback; also EFFICACY]
        ├── EfficacyMultilevelTableParser  [EFFICACY,      prio 10]
        └── PkTableParser                  [PK,            prio 10]
```

`BaseTableParser` does the heavy lifting once and shares it. The three AE parsers all
delegate their body-row loop to its `parseAdverseEventArmRows` template method, passing
flags/delegates for the parts that differ (whether SOC dividers set the category, how
per-row p-values are resolved). `SimpleArmTableParser` handles the EFFICACY shape through
an internal category parameter rather than a separate class.

### Parsers stand on three static utilities and the context resolvers
Per cell, a parser calls `ValueParser.Parse` to decompose the raw text into typed
components, then `BaseTableParser` applies AE/Efficacy promotions (p-value coercion,
dash/`<1` → midpoint percentage, `>100%` rejection) and caption hints. `DoseExtractor`
pulls dose/unit from regimen text and arm labels. Before any of that, the **pre-parse
context resolvers** have already run off the header: `AeColumnContextResolver` classifies
each column, `ArmDefinitionExtractor` + `ArmMetadataEnrichmentService` build the arm list
(column index → arm), and `PopulationDetector` sets the whole-table population. Sample-size
resolution is delegated to the [`SampleSize`](../SampleSize/README.md) folder; reference
lookups to [`Dictionaries`](../Dictionaries/README.md).

### Standardization → QC → Claude is a confidence funnel
After parsing, `ColumnStandardizationService` runs deterministic Phase 1–4 cleanup (cheap,
always). `ParseQualityService` then scores each row; `QCNetCorrectionService` applies ML
corrections and stamps the score. Only rows *below* the quality threshold are forwarded to
`ClaudeApiCorrectionService` (expensive, network), and every Claude suggestion must clear
`ClaudeCorrectionGuardrails` before it is applied. Accepted Claude corrections become ML
ground truth via `QCTrainingStore`, so the cheap stage learns from the expensive one.

### Validation and suppression sit at different points
`StructuralRowSuppressionService` runs *inside* parsing — it prevents scaffold rows from
ever becoming observations (pre-storage). The validation trio runs *after* the batch is
written (or in-memory), annotating `ValidationFlags` and producing a report; it never
deletes rows. `BioequivalentLabelDedupService` runs *before* anything else (Stage 0),
pruning the document set.

### Interface ↔ implementation
Most services are interface-backed for DI and testing. `StructuralRowSuppressionService`,
`AeColumnContextResolver`, `ArmDefinitionExtractor`, `ArmMetadataEnrichmentService`,
`PopulationDetector`, `ValueParser`, `DoseExtractor`, and the guardrail chain are static or
internal helpers with no interface — they are pure logic invoked directly.

---

## Design notes & gotchas

- **`ValidationFlags` is the audit spine.** Each stage appends semicolon-delimited tokens
  (`COL_STD:*`, `QC:*`, `AI_CORRECTED:*`/`AI_REJECTED:*`, `DICT:*`, `AE_ARMN_*`,
  `SUPPRESSED_AE_*`) and never clears prior ones. Parse-quality and ML retraining read this
  trail. The delimiter (`"; "`) is a shared convention — changing it in one place breaks
  flag parsing everywhere.
- **Failures are scoped.** A parser exception rolls back that row's partial emissions and
  skips the *whole table* (`TableParseException`, logged as a skip reason). A DB-write
  exception clears the change tracker and skips the *batch*. The corpus run continues.
- **The PK analyzability filter runs twice** (Stage 3.35 pre-ML and 3.45 post-ML). The
  second pass is defense-in-depth against the known ML classifier bug that reclassifies
  non-PK rows into PK after the pre-ML filter has already passed them.
- **Stage 1 TableCategory ML correction is default-OFF (shadow mode).** It audits into
  `QC:CATEGORY_SHADOW:*` flags without mutating data unless explicitly enabled.
- **Dashes and `<1` are not "missing" in AE tables** — they mean "below threshold" and are
  coerced to a midpoint percentage (using ArmN when available). This matters for downstream
  arm-pairing.
- **`n (%)` vs `Mean (SD)` ambiguity**: `ValueParser` reads `3057 (980)` as count+percent;
  a caption hint can reinterpret it as Mean(SD) and drop the bogus `%`. Without the hint,
  large round parentheticals would be miscoded.
- **`ColumnStandardizationService` is `partial`**: the Phase 1/2 pipeline *ordering* lives
  in the two `*Phase*Pipeline.cs` files (nested `private sealed` classes), but the rule and
  pass *bodies* live on the main service file. Read both to follow Phase 1/2.
- **The router validates content, not just section codes.** A PK-coded section with no
  canonical PK term, or an AE section with no recoverable arm/outcome, is downgraded to
  `SKIP` rather than parsed into zero observations. DDI keywords beat PK/Efficacy.
