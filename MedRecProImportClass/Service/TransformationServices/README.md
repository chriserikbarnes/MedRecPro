# TransformationServices

The SPL **Table Normalization pipeline**. This subsystem reads the raw HTML/structured
tables that live inside FDA *Structured Product Labeling* (drug label) documents and
flattens them into two analysis-ready SQL tables:

- **`tmp_FlattenedStandardizedTable`** — one row per decomposed table cell, 41 fixed
  columns, classified by `TableCategory` (`ADVERSE_EVENT`, `PK`, `DRUG_INTERACTION`,
  `EFFICACY`). This is the primary output.
- **`tmp_FlattenedAdverseEventTable`** — a downstream AE-only projection where each row
  already carries pre-computed Relative Risk (RR), Dose-Normalized RR (DNRR), and 95%
  confidence intervals, so visualizations bind without runtime statistics.

The design principle is *fixed columns, strict context-dependent meaning*: the schema
never widens. A column that means "MedDRA System Organ Class" for an AdverseEvent row
means nothing for a PK row. This keeps the output skinny and JOIN-friendly — cross-table
comparison is a `WHERE` clause, not a reinterpretation exercise.

---

## What's in this directory

| Item | Role |
|---|---|
| [`TableStandardizationServiceCollectionExtensions.cs`](TableStandardizationServiceCollectionExtensions.cs) | DI composition root — `AddTableStandardization()` wires the whole service graph. |
| [`BaseTableFlattening/`](BaseTableFlattening/README.md) | The Stage 1–4 engine: cell context, reconstruction, routing, parsing, standardization, ML/AI correction, validation, dedup. ~50 files. |
| [`AdverseEventTableFlattening/`](AdverseEventTableFlattening/README.md) | Stage 5: AE statistical denormalization (RR / DNRR / CI). |
| [`Dictionaries/`](Dictionaries/README.md) | Shared reference lookups: PK parameters, units, PD markers, per-category profiles, category-name normalization. |
| [`SampleSize/`](SampleSize/README.md) | Per-arm N (sample size) recognition and precedence resolution. |

`BaseTableFlattening` is the heart of the system; the other three folders are leaf
collaborators it depends on (`Dictionaries`, `SampleSize`) or a downstream consumer of
its output (`AdverseEventTableFlattening`).

---

## End-to-end information flow

The pipeline is staged. Each stage is numbered; fractional numbers were inserted as the
pipeline matured, so the numbering is the canonical vocabulary used throughout the code,
logs, and the subfolder READMEs.

```
                          ApplicationDbContext (EF Core → SQL Server)
                                        │
 Stage 0   Document discovery + dedup   │  GetDocumentGuidsOrderedByUniiAsync
           BioequivalentLabelDedupService│  → one canonical label per (Ingredient,DosageForm,Route)
                                        ▼
 Stage 1   Cell context                 │  TableCellContextService
           flat TableCellContext rows   │  (cell+row+table+section+document+UNII)
                                        ▼
 Stage 2   Reconstruction               │  TableReconstructionService
           ReconstructedTable           │  span resolution, row classification,
                                        │  multi-level headers, footnotes
                                        ▼
 Stage 3   Route + Parse                │  TableParserRouter → ITableParser.Parse
           List<ParsedObservation>      │  (arm/population/column context resolved here)
                                        ▼
 Stage 3.25 Column standardization      │  ColumnStandardizationService.Standardize
            (deterministic, 4 phases)   │  + opt-in dropIncompleteRows
                                        ▼
 Stage 3.35 Pre-ML PK analyzability filter (drop unusable PK rows before ML training)
                                        ▼
 Stage 3.4  ML.NET QC correction        │  QCNetCorrectionService.ScoreAndCorrect
            + ParseQualityService score │  (category / dose-routing / value-type)
                                        ▼
 Stage 3.45 Post-ML PK filter re-run    (catch rows ML reclassified into PK)
                                        ▼
 Stage 3.5  Claude AI correction        │  ClaudeApiCorrectionService.CorrectBatchAsync
            (low-quality rows only)     │  guarded by CorrectionGuardrailChain;
                                        │  accepted fixes fed back to ML as ground truth
                                        ▼
 Stage 3.6  Post-process extraction     │  ColumnStandardizationService.PostProcessExtraction
                                        ▼
 DB write   map → LabelView.FlattenedStandardizedTable, bulk insert  ──► tmp_FlattenedStandardizedTable
                                        ▼
 Stage 4    Validation (optional)       │  Row / Table / Batch validation + cross-version
            BatchValidationReport       │  concordance
                                        ▼
 Stage 5    AE denormalization (optional)│  AdverseEventDenormalizationService.PopulateAsync
            RR / DNRR / CI              │  ──► tmp_FlattenedAdverseEventTable
```

`TableParsingOrchestrator` (in `BaseTableFlattening/`) owns this sequence. The full-corpus
entry points walk documents in UNII order, in configurable batches, applying every stage
per batch and then running Stage 4/5 once at the end.

---

## The orchestrator: entry points

`ITableParsingOrchestrator` is the public face of the whole pipeline:

| Method | Purpose |
|---|---|
| `ProcessAllAsync(...)` | Full corpus: truncate → Stage 0 dedup → batch loop (Stages 2–3.6 + write) → optional Stage 5. |
| `ProcessAllWithValidationAsync(...)` | Same as above plus Stage 4 validation, returning a `BatchValidationReport`. Requires the batch validator. |
| `ProcessBatchAsync(filter, ...)` | One batch through every stage. |
| `ProcessBatchWithStagesAsync(filter, ...)` | One batch returning all intermediate artifacts (`BatchStageResult`) for diagnostics. |
| `ParseSingleTableAsync` / `ReconstructSingleTableAsync` / `RouteAndParseSingleTable` / `CorrectObservationsAsync` | Stage-isolated debug paths — exercise one stage without a DB write. |
| `TruncateAsync` | Wipe `tmp_FlattenedStandardizedTable` for a clean rerun. |

Batches are keyed by **DocumentGUID** and sorted by **UNII** so all observations for one
drug product flow into ML scoring and the output table together (product-clustered).

---

## Composition root

`TableStandardizationServiceCollectionExtensions.AddTableStandardization()` registers the
service graph. Two switches shape it:

- `includeValidation` — registers Stage 4 (`IRowValidationService`, `ITableValidationService`,
  `IBatchValidationService`).
- `dropRowsMissingArmNOrPrimaryValue` — enables the Stage 3.25 quality gate that drops rows
  unusable for cross-product meta-analysis.

Registrations are idempotent (`TryAdd*`). Crucially, the two **optional, host-owned**
services are *not* created here:

- `IClaudeApiCorrectionService` (Stage 3.5) — needs an API key + model settings.
- `IQCNetCorrectionService` (Stage 3.4) — needs ML model/training-store settings.

If the host registers them, the orchestrator factory consumes them via `GetService`; if not,
those stages simply no-op. The same is true for `IBioequivalentLabelDedupService` (Stage 0)
and `IAdverseEventDenormalizationService` (Stage 5) — present-and-used, or absent-and-skipped.
This is why every stage in the flow above is guarded by a null check on its service.

---

## Core data contracts

These three types are the currency that flows between stages. They are defined in
`MedRecProImportClass.Models`, but understanding them is essential to reading this folder.

- **`ReconstructedTable`** (Stage 2 output) — a fully resolved grid: classified rows
  (`ExplicitHeader`, `InferredHeader`, `ContinuationHeader`, `SocDivider`, `DataBody`,
  `Footer`), a `ResolvedHeader` of `HeaderColumn`s (each with a `HeaderPath` breadcrumb and
  `LeafHeaderText`), and `ProcessedCell`s carrying `ResolvedColumnStart/End` (colspan/rowspan
  already expanded), footnote markers, and cleaned text.
- **`ParsedObservation`** (Stage 3 output, the working DTO) — one decomposed cell with the
  full 41-column context: provenance (DocumentGUID, TextTableID, SourceRowSeq/CellSeq),
  classification (TableCategory, section codes), observation context (ParameterName/Category/
  Subtype, TreatmentArm, ArmN, Dose/DoseUnit, Population/Subpopulation, Timepoint), decomposed
  values (PrimaryValue/Type, SecondaryValue/Type, LowerBound/UpperBound/BoundType, PValue,
  Unit), and validation fields (ParseConfidence, ParseRule, ValidationFlags). At the DB-write
  boundary it is mapped to the `LabelView.FlattenedStandardizedTable` entity.
- **`TableCategory`** — the classification enum (`ADVERSE_EVENT`, `PK`, `DRUG_INTERACTION`,
  `EFFICACY`, `SKIP`). It governs which parser runs, which column contract applies, and how
  every context-dependent column is interpreted.

---

## Conventions used throughout

- **`ValidationFlags` are an append-only audit trail.** Every stage records its decisions as
  semicolon-delimited tokens on `ParsedObservation.ValidationFlags`, never clearing prior
  entries. Prefixes identify the stage: `COL_STD:*` (standardization), `QC:*` /
  `QC_PARSE_QUALITY:*` (ML), `AI_CORRECTED:*` / `AI_REJECTED:*` (Claude), `DICT:*`
  (dictionary resolution), `AE_ARMN_*` (sample-size resolution), `SUPPRESSED_AE_*`
  (structural suppression). This trail is how downstream review and ML retraining understand
  what happened to each row.
- **Confidence is a first-class signal.** `ParseConfidence` (set by parsers) and the
  Stage 3.4 `QC_PARSE_QUALITY` score gate whether a row is forwarded to the (expensive) Claude
  stage. Only low-quality rows are sent.
- **Failures are scoped, not fatal.** A parser exception skips the *whole table* (rolled back
  atomically) and is logged as a skip reason; a DB-write failure skips the *batch*. The
  corpus run continues. Stage 5 is the exception — it fails fast, because a partial
  denormalized table is considered more dangerous than a failed run.

---

## Where to read next

- Start with [`BaseTableFlattening/`](BaseTableFlattening/README.md) for the engine
  (Stages 1–4) — this is where most of the logic lives.
- [`AdverseEventTableFlattening/`](AdverseEventTableFlattening/README.md) for the Stage 5
  statistics.
- [`Dictionaries/`](Dictionaries/README.md) and [`SampleSize/`](SampleSize/README.md) for the
  leaf utilities the parsers and standardizer lean on.
