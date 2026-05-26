# MedRecProImportClass

A standalone .NET 8 class library extracted from the MedRecPro web application to support SPL (Structured Product Labeling) and FDA Orange Book import operations. This library is designed to be used by console applications and other non-web clients that need to import FDA drug data.

## Purpose

This library was created to enable single-file publishing for the `MedRecProConsole` application. The main MedRecPro project is a Web SDK project which conflicts with self-contained deployment requirements. By extracting the import-related functionality into a dedicated class library, console applications can reference only the components they need without pulling in web-specific dependencies.

## Features

- **SPL XML Parsing**: Complete parsing infrastructure for FDA SPL documents
- **FDA Orange Book Import**: Parses `products.txt`, `patent.txt`, and `exclusivity.txt` (tilde-delimited) from Orange Book ZIP files with idempotent upserts and multi-tier entity matching to existing SPL data, plus embedded patent use code definitions
- **Entity Framework Core Integration**: Database context and repository pattern for data persistence
- **SPL Table Normalization**: Multi-stage pipeline transforms heterogeneous FDA drug label tables into a uniform 41-column analytical schema (`tmp_FlattenedStandardizedTable`) for cross-product analysis -- includes Stage 0 bioequivalent ANDA dedup, table reconstruction, five concrete section-aware parsers, structural-row suppression audit, category downgrade gates, 4-phase column standardization with explicit Phase 1 / Phase 2 ordering pipelines, deterministic parse-quality gate, shadow-mode QCNet diagnostics, guarded Claude AI correction, post-processing extraction, automated validation, and shared DI registration through `AddTableStandardization(...)`
- **AE Denormalization (Stage 5)**: Pre-computes Relative Risk (RR), Dose-Normalized RR (DNRR), 95% CI bounds, and PERSISTED log-scale companions per AE row into `tmp_FlattenedAdverseEventTable`, then materializes `dbo.vw_AeRisk` into `tmp_FlattenedAdverseEventRiskTable` so real-time visualizations bind without runtime statistics. Phase 1 (the SQL DDL) and Phase 2 (the `AdverseEventDenormalizationService` population service + helper decomposition + `RelativeRiskCalculator` utility) are both shipped
- **39+ Specialized Parsers**: Covers all SPL document sections and Orange Book data including:
  - Document structure and sections
  - Products, ingredients, and packaging
  - Organizations and licensing
  - Marketing status and regulatory information
  - REMS, warning letters, and compliance actions
  - Orange Book applicants, products, patents, exclusivity, and patent use codes
- **Background Task Processing**: Queue-based import worker service
- **Encryption Support**: AES-256 encryption for sensitive data

## Project Structure

```
MedRecProImportClass/
+-- Abstractions/           # Interfaces for dependency injection
|   +-- IFileSource.cs      # File input abstraction (replaces IFormFile)
|   +-- LocalFileSource.cs  # Local file system implementation
|   +-- ServiceInterfaces.cs # IEncryptionService, IDictionaryUtilityService
+-- Attributes/             # Validation attributes for SPL entities
+-- Context/                # Entity Framework DbContext (auto-registers OrangeBook entities via reflection)
+-- DataAccess/             # Repository pattern implementation
+-- Helpers/                # Utility classes
|   +-- EncryptionHelper.cs           # AES-256 encryption (StringCipher)
|   +-- TextUtil.cs                   # Text processing utilities
|   +-- XmlHelpers.cs                 # XML parsing utilities
|   +-- ApplicationNumberParser.cs    # NDA/ANDA prefix strip + classification
|   +-- DoseRegimenRoutingPolicy.cs   # Shared dose-regimen routing rules
|   +-- ParsedObservationFieldAccess.cs # Reflection-free Get/Set/IsPopulated by column name
|   +-- ObservationFlagSnapshotBuilder.cs # Occurrence-aware pre/post Claude flag snapshots for reports
|   +-- ValidationFlagExtensions.cs   # Canonical "; "-delimited flag append
|   +-- ...
+-- Models/                 # Entity classes and DTOs
|   +-- Labels.cs           # Main Label container with 50+ nested classes
|   +-- OrangeBook.cs       # Orange Book entity classes (Applicant, Product, Patent, etc.)
|   +-- Import.cs           # Import result types
|   +-- ImportData.cs       # SplData entity
|   +-- TableSuppressionAuditRecord.cs # Structural row/cell suppression audit record
|   +-- ...
+-- Resources/              # Embedded assembly resources
|   +-- OrangeBookPatentUseCodes.json  # Patent use code definitions (4,409 entries)
+-- Skills/                 # Prompt templates for AI-powered stages
|   +-- correction-system-prompt.md    # Stage 3.5 Claude correction rules
|   +-- pivot-comparison-prompt.md     # Pivot comparison analysis
+-- Service/                # Business logic services
    +-- SplImportService.cs       # Main SPL import orchestration
    +-- SplDataService.cs         # SPL data storage/retrieval
    +-- SplParsingService.cs      # XML parsing orchestration
    +-- ZipImportWorkerService.cs # Background ZIP processing
    +-- ParsingServices/          # 39+ specialized parser files
    |   +-- OrangeBookProductParsingService.cs     # Phase A: products.txt parser
    |   +-- OrangeBookPatentParsingService.cs      # Phase B: patent.txt parser
    |   +-- OrangeBookExclusivityParsingService.cs # Phase C: exclusivity.txt parser
    |   +-- OrangeBookPatentUseCodeParsingService.cs # Phase D: patent use code upsert
    |   +-- ...                   # SPL section parsers
    +-- ParsingValidators/        # Validation services
    +-- TransformationServices/   # SPL Table Standardization pipeline and DI registration
        +-- TableStandardizationServiceCollectionExtensions.cs # AddTableStandardization(...) service graph
        +-- BioequivalentLabelDedupService.cs   # Stage 0: ANDA + repackager dedup by Orange Book group
        +-- TableCellContextService.cs          # Stage 1: source view assembly
        +-- TableReconstructionService.cs       # Stage 2: table reconstruction
        +-- ValueParser.cs                      # Stage 3: regex-based value decomposition
        +-- PopulationDetector.cs               # Stage 3: population auto-detection
        +-- DoseExtractor.cs                    # Stage 3: dose number + unit extraction
        +-- BaseTableParser.cs                  # Stage 3: shared parser helpers
        +-- ArmDefinitionExtractor.cs           # Stage 3: shared arm definition extraction
        +-- ArmMetadataEnrichmentService.cs     # Stage 3: shared arm/sample-size metadata enrichment
        +-- StructuralRowSuppressionService.cs  # Stage 3: shared suppression diagnostic helpers
        +-- AeColumnContextResolver.cs          # Stage 3: AE arm/context recovery and suppression classification
        +-- ITableParserDiagnostics.cs          # Stage 3: optional suppressed-row diagnostic surface
        +-- PkTableParser.cs                    # Stage 3: pharmacokinetic tables
        +-- SimpleArmTableParser.cs             # Stage 3: single-header AE/efficacy
        +-- MultilevelAeTableParser.cs          # Stage 3: two-row header AE tables
        +-- AeWithSocTableParser.cs             # Stage 3: AE with SOC dividers
        +-- EfficacyMultilevelTableParser.cs    # Stage 3: two-row header efficacy
        +-- TableParserRouter.cs                # Stage 3: section code -> parser routing
        +-- ColumnStandardizationService.cs     # Stage 3.25: 4-phase column contracts
        +-- ColumnStandardizationPhase1Pipeline.cs # Stage 3.25: ordered AE/Efficacy arm/context rule chain
        +-- ColumnStandardizationPhase2Pipeline.cs # Stage 3.25: ordered content-normalization pass chain
        +-- ColumnContractRegistry.cs           # Stage 3.25/4: per-category R/E/O/N column sets
        +-- AeParameterCategoryDictionaryService.cs # Stage 3.25: AE parameter -> SOC resolution (1189 entries)
        +-- ParseQualityService.cs              # Stage 3.4: deterministic parse-quality gate (drives Claude forwarding)
        +-- ClaudeApiCorrectionService.cs       # Stage 3.5: AI-powered correction (gated by Stage 3.4 score)
        +-- ClaudeCorrectionGuardrails.cs       # Stage 3.5: deterministic first-rejection-wins correction guardrails
        +-- QCNetCorrectionService.cs           # Shadow-mode ML.NET classifiers — emits diagnostic flags only, does not correct
        +-- QCTrainingStore.cs                  # Persistent training data for the shadow-mode classifiers
        +-- TableParsingOrchestrator.cs         # Batch loop + stage sequencing + DB writes
        +-- RowValidationService.cs             # Stage 4: per-observation checks
        +-- TableValidationService.cs           # Stage 4: cross-row checks
        +-- BatchValidationService.cs           # Stage 4: aggregate reporting
        +-- AdverseEventTableFlattening/        # Stage 5 (Phase 2): AE denormalization
        |   +-- IAdverseEventDenormalizationService.cs # Service contract
        |   +-- AdverseEventDenormalizationService.cs  # Population service (truncate + stream + classify + write + risk materialize)
        |   +-- SourceRowEligibility.cs                # Source row inclusion / exclusion rules
        |   +-- ComparatorGrouper.cs                   # Study-group keying for comparator selection
        |   +-- ComparatorSelector.cs                  # Placebo / low-dose / no-comparator cascade
        |   +-- AeStatEntityBuilder.cs                 # Entity projection plus RR/DNRR calculation wiring
        |   +-- AeDenormalizationConstants.cs          # Shared calculation method / flag constants
        |   +-- IPlaceboArmClassifier.cs               # Shared placebo-arm classifier contract and default wrapper
        |   +-- RelativeRiskCalculator.cs              # Pure-function Katz log-method RR/CI + log-linear DNRR + trial-design classifier
        +-- Dictionaries/                       # Shared single-source-of-truth lookups
            +-- CategoryProfileRegistry.cs      # Per-TableCategory profiles (consolidates 6 prior dicts)
            +-- CategoryProfile.cs              # Profile record (wraps CategoryContract + extras)
            +-- CategoryNameNormalizer.cs       # Underscore-uppercase <-> documentation form
            +-- PkParameterDictionary.cs        # Canonical PK parameter names + aliases
            +-- UnitDictionary.cs               # Known PK units + variant normalization
            +-- PdMarkerDictionary.cs           # Pharmacodynamic marker recognition
```

## Dependencies

| Package | Version | Purpose |
|---------|---------|---------|
| Microsoft.EntityFrameworkCore.SqlServer | 8.0.15 | Database access |
| Microsoft.EntityFrameworkCore.Design | 8.0.15 | EF Core design-time tooling |
| Microsoft.AspNetCore.Identity.EntityFrameworkCore | 8.0.15 | User entity support |
| Microsoft.Extensions.Hosting | 9.0.4 | Background service support |
| Microsoft.Extensions.Hosting.Abstractions | 9.0.4 | Host abstractions for non-web clients |
| Microsoft.Extensions.Logging | 9.0.4 | Logging infrastructure |
| Microsoft.Extensions.Logging.Abstractions | 9.0.4 | Logging abstractions used by services |
| Microsoft.Extensions.Configuration.Abstractions | 9.0.4 | Configuration contracts |
| Microsoft.Extensions.DependencyInjection.Abstractions | 9.0.4 | DI extension contracts |
| Microsoft.Extensions.Caching.Memory | 9.0.4 | In-memory cache support |
| Microsoft.ML | 5.0.0 | ML.NET shadow-mode `QCNetCorrectionService` classifiers (diagnostic flags only; not part of the active correction flow) |
| Dapper | 2.1.66 | Micro-ORM for complex queries |
| Dapper.Contrib | 2.0.78 | Dapper helper extensions |
| Microsoft.Data.SqlClient | 6.0.2 | SQL Server client APIs |
| Newtonsoft.Json | 13.0.3 | JSON serialization |
| Microsoft.Extensions.Http | 9.0.4 | HttpClient factory for Claude API |
| HtmlAgilityPack | 1.12.1 | HTML parsing |
| HtmlSanitizer | 9.0.892 | HTML sanitization |
| Humanizer | 2.14.1 | Text formatting helpers |
| System.Runtime.Caching | 9.0.4 | Runtime cache support |
| AngleSharp | 0.17.1 | HTML/CSS parsing support |
| Microsoft.AspNetCore.Http.Abstractions | 2.3.0 | `IFormFile` / HTTP context abstractions retained for import compatibility |
| Microsoft.AspNetCore.Http.Features | 5.0.17 | HTTP feature abstractions retained for import compatibility |

## Usage

### Basic Setup

```csharp
using MedRecProImportClass.Data;
using MedRecProImportClass.Service;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

// Configure services
var services = new ServiceCollection();

services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

services.AddScoped<SplImportService>();
services.AddScoped<SplDataService>();
services.AddScoped<SplParsingService>();

var provider = services.BuildServiceProvider();
```

### Table Standardization Setup

Hosts that need the table-standardization pipeline can register the shared service graph with `AddTableStandardization(...)`. The extension owns Stage 1 through Stage 5 parser, validation, standardization, and AE denormalization registrations while leaving host-specific configuration, logging providers, database provider setup, Claude settings, and QC model settings in the application host.

```csharp
using MedRecProImportClass.Data;
using MedRecProImportClass.Service.TransformationServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();

services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

services.AddTableStandardization(
    includeValidation: true,
    dropRowsMissingArmNOrPrimaryValue: false);

var provider = services.BuildServiceProvider();
var orchestrator = provider.GetRequiredService<ITableParsingOrchestrator>();
```

### Importing SPL Files

```csharp
using MedRecProImportClass.Abstractions;
using MedRecProImportClass.Service;

// Create file sources from local files
var files = Directory.GetFiles(zipDirectory, "*.zip")
    .Select(path => new LocalFileSource(path))
    .Cast<IFileSource>()
    .ToList();

// Get the import service
var importService = provider.GetRequiredService<SplImportService>();

// Process the files
var results = await importService.ProcessZipFilesAsync(files, cancellationToken);
```

## Orange Book Import

The Orange Book import pipeline processes the FDA Orange Book ZIP file in four sequential phases, parsing tilde-delimited text files and upserting into normalized database tables.

### Import Phases

| Phase | Service | Source | Description |
|-------|---------|--------|-------------|
| A | `OrangeBookProductParsingService` | `products.txt` | Products, applicants, and SPL entity matching |
| B | `OrangeBookPatentParsingService` | `patent.txt` | Patents linked to products by (ApplType, ApplNo, ProductNo) |
| C | `OrangeBookExclusivityParsingService` | `exclusivity.txt` | Exclusivity records linked to products |
| D | `OrangeBookPatentUseCodeParsingService` | Embedded JSON | Patent use code definitions (lookup table) |

### Entity Model

The `OrangeBook` class contains nested entity classes mapped to database tables via `[Table]` attributes:

| Entity | Table | Description |
|--------|-------|-------------|
| `Applicant` | `OrangeBookApplicant` | Pharmaceutical companies holding FDA approvals |
| `Product` | `OrangeBookProduct` | Drug products from products.txt |
| `Patent` | `OrangeBookPatent` | Patent records from patent.txt |
| `Exclusivity` | `OrangeBookExclusivity` | Exclusivity records from exclusivity.txt |
| `PatentUseCodeDefinition` | `OrangeBookPatentUseCode` | Patent use code lookup (code -> definition) |
| `ApplicantOrganization` | `OrangeBookApplicantOrganization` | Junction linking applicants to SPL organizations |
| `ProductIngredientSubstance` | `OrangeBookProductIngredientSubstance` | Junction linking products to SPL ingredients |
| `ProductMarketingCategory` | `OrangeBookProductMarketingCategory` | Junction linking products to SPL marketing categories |

### Entity Matching Strategy

The import links Orange Book data to existing SPL entities using multi-tier matching:

- **Applicant -> Organization**: Normalized exact match, then token-based similarity (Jaccard/containment) with a two-pass strategy -- first pass uses full tokens for higher discrimination, fallback uses noise-stripped tokens. Corporate suffixes and pharma noise words are stripped before comparison.
- **Product -> IngredientSubstance**: Exact name match, then regex-based matching
- **Product -> MarketingCategory**: Exact name match, then regex-based matching

### Design

- **Idempotent**: Upsert-based -- safe to re-run without duplication
- **Batch processing**: Products and patents are processed in batches of 5,000
- **Progress callbacks**: Supports real-time progress reporting via callbacks to the console UI
- **In-memory matching**: Pre-computes organization cache for fast similarity scoring
- **Row-level retry**: Patent import falls back to row-by-row inserts on batch failure, logging all field values for diagnostics

### Patent Use Code Definitions

The `patent.txt` file in the Orange Book ZIP contains patent use code values (e.g., `U-141`) in the `Patent_Use_Code` column, but the definitions of what those codes mean are **not included** in the ZIP. The definitions are published separately by the FDA and are maintained as an embedded JSON resource in this assembly.

#### Updating Patent Use Code Definitions

If the FDA publishes new patent use codes, update the embedded resource as follows:

1. **Download the definitions** from the FDA Orange Book patent use code page:
   https://www.accessdata.fda.gov/scripts/cder/ob/results_patent.cfm

   Click the **Excel** button on that page to download the `.xlsx` file containing all patent use code definitions.

2. **Convert the Excel file to JSON** using an online converter such as:
   https://products.aspose.app/cells/conversion/xlsx-to-json

   Upload the `.xlsx` file and download the resulting `.json` file.

3. **Verify the JSON format** matches the expected structure -- an array of objects with `Code` and `Definition` properties:
   ```json
   [
     { "Code": "U-1", "Definition": "PREVENTION OF PREGNANCY" },
     { "Code": "U-2", "Definition": "TREATMENT OF ACNE" },
     ...
   ]
   ```

4. **Replace the embedded resource** at `MedRecProImportClass/Resources/OrangeBookPatentUseCodes.json` with the new JSON file. Ensure the file is saved as UTF-8.

5. **Rebuild** the `MedRecProImportClass` project. The updated definitions will be embedded in the assembly and upserted on the next Orange Book import run.

Phase D is idempotent -- existing records with changed definitions are updated, new records are inserted, and unchanged records are skipped.

## SPL Table Standardization Pipeline

A multi-stage pipeline that transforms heterogeneous FDA drug label table data into a uniform 41-column analytical schema for cross-product analysis, deterministic QC, and optional Claude correction. The full corpus is 250K+ labels; the pipeline supports batch processing by TextTableID range.

### Architecture

```
Stage 0: Bioequivalent Label Dedup (default on)
  BioequivalentLabelDedupService -> collapses ANDA + repackager labels to one canonical DocumentGUID per (Ingredient, DosageForm, Route)
        |
Stage 1: Source View Assembly
  TableCellContextService -> 26-column TableCellContext DTO
        |
Stage 2: Table Reconstruction
  TableReconstructionService -> ReconstructedTable (classified rows, resolved spans, multi-level headers)
        |
Stage 3: Section-Aware Parsing
  TableParserRouter -> ITableParser (5 concrete parsers) -> List<ParsedObservation> + suppression audit
        |
Stage 3.25: Column Standardization (deterministic)
  ColumnStandardizationService -> Phase1ArmContextPipeline + Phase2ContentNormalizationPipeline + Phase 3/4 contracts
        |
Stage 3.35: PK Analyzability Filter
  TableParsingOrchestrator -> drops non-analyzable PK rows before correction/write
        |
Stage 3.4: QCNet Shadow Diagnostics + Parse-Quality Gate
  QCNetCorrectionService -> diagnostic classifier flags only by default
  ParseQualityService -> score in [0,1] per observation; rows below threshold (0.75) forward to Claude
        |
Stage 3.45: Post-QC PK Analyzability Filter
  TableParsingOrchestrator -> defense-in-depth recheck before Claude/write
        |
Stage 3.5: Claude AI Correction (gated by Stage 3.4 quality score)
  ClaudeApiCorrectionService -> semantic review + CorrectionGuardrailChain -> field correction / AI_REJECTED flags
        |
Stage 3.6: Post-Processing Extraction
  ColumnStandardizationService.PostProcessExtraction -> catch values AI corrected into extractable form
        |
  TableParsingOrchestrator -> bulk write to tmp_FlattenedStandardizedTable
        |
Stage 4: Validation
  RowValidationService + TableValidationService + BatchValidationService -> BatchValidationReport
        |
Stage 5: AE Denormalization (Phase 1 SQL DDL + Phase 2 service both shipped)
  AdverseEventDenormalizationService + helper services -> tmp_FlattenedAdverseEventTable
  dbo.vw_AeRisk -> tmp_FlattenedAdverseEventRiskTable
        (one row per eligible AE source row except the comparator chosen per study group;
         RR/DNRR/CI pre-computed; PERSISTED log columns auto-maintained by SQL Server;
         risk view materialized as the final Stage 5 step)
```

### Stage 0: Bioequivalent Label Dedup

`BioequivalentLabelDedupService` collapses multiple ANDA labels (and their repackager relabelings) referencing the same innovator down to one canonical DocumentGUID per bioequivalent group, preventing aggregate-signal inflation when the same published value appears 40+ times across generic/repackager labels.

- **Group key**: `Ingredient + DosageForm + Route` from Orange Book (all strengths collapse).
- **Selection priority**: NDA preferred over ANDA; within the chosen tier, the DocumentGUID with the most recent `LabelEffectiveDate` wins, tie-breaking on higher `VersionNumber` then lower DocumentGUID (ordinal).
- **Unclassifiable handling**: drops DocumentGUIDs that cannot be resolved to an Orange Book `(ApplType, ApplNo)` pair, with three distinct reason codes: `no_application_number`, `unrecognized_prefix`, `no_orange_book_match`.
- **UNII walk-order preserved**: kept rows retain their original UNII ordering -- important because the QCNet training accumulator and diagnostic output rely on UNII locality.
- **Default on**; bypass via `--no-dedup-bioequivalent` on the CLI (applies to `parse` and `validate` modes).

### Stage 1: Source View Assembly

`TableCellContextService` joins cell-level data (TextTableCell -> TextTableRow -> TextTable -> SectionTextContent) with section context (vw_SectionNavigation) and document context (Document) into a flat 26-column `TableCellContext` DTO using EF Core LINQ. Supports filtering by DocumentGUID, TextTableID, ID range, and MaxRows.

### Stage 2: Table Reconstruction

`TableReconstructionService` takes flat `TableCellContext` rows and reconstructs logical table structures:

- **Cell processing**: Extracts footnote markers from `<sup>` tags, extracts `styleCode` attributes, strips HTML
- **Row classification**: ExplicitHeader, InferredHeader, ContinuationHeader, SocDivider, DataBody, Footer
- **Span resolution**: 2D occupancy grid resolves ColSpan/RowSpan into absolute column positions
- **Header resolution**: Builds multi-level header paths (e.g., "Treatment > Drug A") from classified header rows
- **Footnote extraction**: Parses footer rows into marker -> text dictionary

### Stage 3: Section-Aware Parsing

`TableParserRouter` maps `ParentSectionCode`, `SectionTitle`, caption text, and table shape to a `TableCategory`, then selects the most specific parser for the reconstructed layout. `TableParsingOrchestrator` runs the batch loop: reconstruct -> route -> parse -> standardize -> QCNet shadow diagnostics / parse-quality score -> Claude correction -> post-process -> write to `tmp_FlattenedStandardizedTable`.

Parsers can expose `ITableParserDiagnostics` so structural rows or cells suppressed before observation emission are preserved as audit records. The console markdown and JSONL reports surface those suppressions alongside emitted observations, which makes parser-gate changes auditable without forcing non-observational labels into `tmp_FlattenedStandardizedTable`.

### Table Categories

The `TableCategory` column is the single most important classification value -- all downstream normalization rules, PrimaryValueType assignment, BoundType defaults, and ParameterSubtype interpretation depend on it.

| Category | Description | Typical Source Section |
|----------|-------------|------------------------|
| `ADVERSE_EVENT` | Incidence/frequency of adverse events by treatment arm | 34084-4 Adverse Reactions |
| `PK` | Pharmacokinetic parameters (Cmax, AUC, t1/2, etc.) | 34090-1 Clinical Pharmacology / 43682-4 Pharmacokinetics |
| `DRUG_INTERACTION` | Co-admin drug effects on PK parameters (geometric mean ratios) | 34073-7 Drug Interactions |
| `EFFICACY` | Comparative efficacy outcomes with risk measures and CIs | 34092-7 Clinical Studies |
| `SKIP` | Tables to exclude (patient info, NDC, formulas, and any table that does not classify into the four parsed categories) | Various |

#### Classification Decision Tree

Tables are classified conservatively. Section and caption signals propose a category, then category-specific viability gates decide whether the table is safe to parse or should be downgraded to `SKIP`:

1. Hard skip checks run first: patient information sections, single-column tables, and captions such as NDC, How Supplied, inactive ingredients, storage, packaging, and formulas.
2. Clinical pharmacology / pharmacokinetics sections route to PK only when PK-like content is present; strong drug-interaction wording routes to `DRUG_INTERACTION`; otherwise the table downgrades to `SKIP`.
3. Adverse reaction sections route to `ADVERSE_EVENT` only when at least one recoverable arm and at least one parseable outcome cell are present.
4. Clinical studies sections route to `EFFICACY` only when the arm-based shape is viable and the body is not dominated by structural text-only rows.
5. Missing or unclassified parent section codes fall back to `SectionTitle` and caption keywords before returning `SKIP`.

### Parsers

| Parser | Category | Structural Trigger |
|--------|----------|--------------------|
| `PkTableParser` | PK | Columns are PK parameters, rows are dose regimens |
| `SimpleArmTableParser` | AE / EFFICACY | Single-header with treatment arm columns |
| `MultilevelAeTableParser` | AE | Two-row header (colspan study contexts + arm sub-headers) |
| `AeWithSocTableParser` | AE | Single-header with SOC divider rows in body |
| `EfficacyMultilevelTableParser` | EFFICACY | Two-row header with stat columns (ARR, RR, P-value) |

### Value Decomposition

`ValueParser` applies ordered regex patterns to decompose cell text into structured components: PrimaryValue, SecondaryValue, CI bounds, P-value, unit, and confidence score. Includes `PCT_CHECK` validation when arm sample size is available.

Recent parser work added category-aware value interpretation before values are copied into observations:

- AE/Efficacy count-plus-inequality cells such as `1 (<1)`, `1 (< 1%)`, `3.0 (<0.1)`, and `1.2 (<0.1)` parse as `PrimaryValueType=Percentage`, keep the presented count as `SecondaryValue` / `SecondaryValueType=Count`, and emit `PCT_DERIVED_FROM_COUNT_LT`.
- Standalone `<1` in AE percentage contexts is treated as incidence percentage rather than p-value; explicit p-value row/header text is still required for `PrimaryValueType=PValue`.
- Efficacy `n/N` rows parse as `PrimaryValueType=Count` with `SecondaryValueType=Denominator`, and the denominator can populate or cross-check `ArmN`.
- Explicit Efficacy p-value/stat rows can emit `TreatmentArm=Comparison`, but broad post-parse duplicate comparison suppression is intentionally disabled because the same source cell can contain valid ordinary-arm and comparison evidence. Future duplicate prevention should happen at the parser decision point, not via list-wide removal.
- Percentages greater than 100 are rejected in context. AE/Efficacy rows demote to count or numeric evidence; PK rows apply isolated parenthetical-stat demotion with `PCT_GT100_REJECTED` / `PK_PCT_GT100_DEMOTED`.
- Interspersed body labels are recovered as `ParameterName` while category-like parent labels remain in `ParameterCategory`; the label text cell itself is not emitted as an observation.

### Population Detection

`PopulationDetector` auto-detects study population from Caption/SectionTitle using regex extraction and a keyword dictionary, with Levenshtein-based fuzzy cross-validation between sources.

### Stage 3.25: Column Standardization

`ColumnStandardizationService` is a deterministic, rule-based service that processes ALL table categories (except SKIP) through a 4-phase pipeline. It corrects systematic misclassification caused by the diversity of FDA table layouts -- doses appearing as column headers, N-values in arm positions, study names in the wrong header row, etc.

The service is now split into a partial class with explicit ordering helpers. `ColumnStandardizationPhase1Pipeline.cs` owns the AE/Efficacy arm/context rule order, and `ColumnStandardizationPhase2Pipeline.cs` owns the all-category content-normalization pass order. The individual rule bodies remain on `ColumnStandardizationService`, which keeps behavior stable while making the execution sequence testable.

#### Phase 1: Arm/Context Corrections (AE + EFFICACY only)

11 ordered rules relocate misclassified content from TreatmentArm and StudyContext to their correct columns. `Phase1ArmContextPipeline` preserves the legacy order: Rule 11 runs first, Rules 1-6 are first-match, and Rules 7-10 always run afterward.

| Rule | Pattern | Action |
|------|---------|--------|
| 11 | Bracketed `[N=xxx]` in TreatmentArm | Extract N -> ArmN, classify remaining text |
| 1 | TreatmentArm is `(N=267)` or `N=677` | Move N -> ArmN, recover arm from StudyContext |
| 2 | TreatmentArm is format hint (`%`, `#`, `n(%)`) | Discard, recover arm from StudyContext |
| 3 | TreatmentArm is severity grade (`Severe`, `Grades 3/4`) | Move -> ParameterSubtype |
| 4 | TreatmentArm is pure dose (`10 mg daily`) | Move -> DoseRegimen |
| 5 | TreatmentArm is bare number + StudyContext has dose descriptor | Reconstruct DoseRegimen, extract drug name |
| 6 | TreatmentArm is drug+dose combined | Split drug -> TreatmentArm, dose -> DoseRegimen |
| 7 | StudyContext contains arm with embedded N= | Split drug -> TreatmentArm, N -> ArmN |
| 8 | StudyContext contains drug name, TreatmentArm does not | Swap |
| 9 | StudyContext is descriptor hint (`Incidence`, `Reaction`) | Clear StudyContext |
| 10 | TreatmentArm has trailing `%` | Strip format hint, promote Numeric -> Percentage |

N-value patterns support comma-formatted numbers (e.g., `(n = 8,506)` -> ArmN=8506).

Drug name identification uses an exact-match dictionary loaded from `vw_ProductsByIngredient` at initialization, supplemented by 13 known abbreviations (AZA, MMF, CsA, etc.) and first-word partial matching.

#### Phase 2: Content Normalization (ALL categories)

Ten ordered sub-passes clean up column content across all table categories. `Phase2ContentNormalizationPipeline` makes the dependency order explicit: inline N stripping before DoseRegimen triage, subtype unit extraction before PK canonicalization, SOC normalization before AE dictionary fill, and final dose scanning after column movement.

| Sub-pass | Target Column | Key Operations |
|----------|---------------|----------------|
| `normalizeInlineNValues` | All columns | Strips N= patterns from every non-RawValue column, populates ArmN. Supports comma-formatted numbers. |
| `normalizeDoseRegimen` | DoseRegimen | Routes PK sub-params (Cmax, AUC, etc.) -> ParameterSubtype; co-admin drug names -> ParameterSubtype; residual population/timepoint -> their correct columns |
| `normalizeParameterName` | ParameterName | Removes caption echoes ("Table 3..."), header echoes ("n"), bare dose integers; decodes HTML entities; collapses OCR artifacts |
| `normalizeTreatmentArm` | TreatmentArm | Removes header echoes ("Number of Patients"), generic labels ("Treatment", "PD"); extracts embedded N= and doses; routes study names -> StudyContext |
| `extractUnitFromParameterSubtype` | ParameterSubtype | Extracts units from trailing parenthesized content in PK/DDI subtypes (e.g., `Cmax(pg/mL)` -> Subtype=`Cmax`, Unit=`pg/mL`; `Cmax(serum, mcg/mL)` -> Subtype=`Cmax, serum`, Unit=`mcg/mL`). Normalizes variant spellings. |
| `applyPkCanonicalization` | PK columns | Applies PK parameter and unit dictionary normalization after subtype/unit extraction. |
| `normalizeUnit` | Unit | Detects leaked column headers (>30 chars, drug names, keywords); normalizes variant spellings (`mcg h/mL` -> `mcg*h/mL`); extracts real units from verbose descriptions |
| `normalizeParameterCategory` | ParameterCategory | Canonical MedDRA SOC mapping (~55 variants -> 26 canonical names) with OCR artifact repair. AE tables only. |
| `AeParameterCategoryDictionaryService.TryResolveObservation` | ParameterCategory | Fills missing AE SOC values from the production-derived AE dictionary after existing categories normalize. |
| `DoseExtractor.ScanAllColumnsForDose` | Dose/DoseUnit | Final defensive scan for dose evidence after prior column movements complete. |

#### Phase 3: PrimaryValueType Migration (ALL categories)

Maps old PrimaryValueType strings to a tightened 15-value enum using TableCategory, Caption, and bounds context. ArithmeticMean is the default for "Mean" in all categories -- GeometricMean is only used when there is an explicit hint in the caption, header, or footer.

| Old Value | New Value | Resolution Logic |
|-----------|-----------|------------------|
| `Mean` | `ArithmeticMean` | Default for ALL categories (unless caption explicitly says "geometric" or "LS mean") |
| `Mean` | `GeometricMean` | Only when caption/header/footer explicitly contains "geometric" |
| `Mean` | `LSMean` | Caption says "LS mean" or "least square" |
| `Percentage` | `Percentage` | Direct rename (all categories) |
| `MeanPercentChange` | `PercentChange` | Direct rename |
| `RelativeRiskReduction` | `HazardRatio` | Caption contains "hazard" |
| `RelativeRiskReduction` | `OddsRatio` | Caption contains "odds" |
| `RelativeRiskReduction` | `RelativeRisk` | Default |
| `Ratio` | `GeometricMeanRatio` | DDI category |
| `Numeric` | context-resolved | AE+% -> Percentage, AE+int -> Count, PK -> ArithmeticMean, DDI -> GeometricMeanRatio, Efficacy+bounds -> HazardRatio |

#### Phase 4: Column Contract Enforcement (ALL categories)

Enforces per-TableCategory contracts defining which columns are Required (R), Expected (E), Optional (O), or Not Applicable (N) for each table type:

- **NULL enforcement**: Columns marked N/A are set to null (e.g., Timepoint for AdverseEvent, ParameterCategory for PK)
- **Missing required flagging**: Columns marked Required that are empty produce `COL_STD:MISSING_R_{Column}` flags
- **Default BoundType**: When LowerBound/UpperBound are populated but BoundType is null, applies category defaults (90CI for PK/DDI, 95CI for Efficacy)

All corrections across all phases are flagged in `ValidationFlags` with `COL_STD:` prefixed audit flags.

### Stage 3.4: Parse-Quality Gate

Stage 3.4 does **not** correct observations. It computes a deterministic, rule-based parse-quality score per observation, and that score gates whether Stage 3.5 forwards the row to the Claude API for correction. The actual correction work happens at Stage 3.5; Stage 3.4's job is to pick which rows are worth spending an API call on.

`ParseQualityService` produces a score in `[0,1]` by applying multiplicative penalties for parse-alignment failures — the class of error Claude is good at correcting — rather than value-extremity. The score is emitted as `QC_PARSE_QUALITY:{score}` on every observation, with a companion `QC_PARSE_QUALITY:REVIEW_REASONS:{list}` flag when penalties fire.

| Penalty | Multiplier | Trigger |
|---------|:---:|---------|
| Hard failure | × 0.2 | Null `PrimaryValue` or null `PrimaryValueType` |
| Hard failure | × 0.3 | `PrimaryValueType="Text"` or null `ParameterName` (only when Required for the category) |
| Hard failure | × 0.4 | Null `TableCategory` |
| Required miss | × 0.6 each | Per missing Required column from the per-category contract (`ColumnContractRegistry`) |
| Structural garbage | × 0.5 | `Unit` matches digit / length-26+ / caption-leak regex |
| Structural garbage | × 0.5 | `ParameterSubtype` matches stat-format / food-status / frequency / dose-phrase regex |
| Structural garbage | × 0.7 | Negative `LowerBound` on `ArithmeticMean` / `GeometricMean` / `Percentage` / `Count` |
| Soft repair | × 0.9 each | One of: `PVT_MIGRATED`, `BOUND_TYPE_INFERRED`, `CAPTION_REINTERPRET`, `PLUSMINUS_TYPE_INFERRED`, `MISSING_R_Unit`, `PK_NAME_PARKED_CTX` |
| Rescue boost | × 0.85 | `PK_UNIT_SIBLING_VOTED:RESCUE_BOOST` (subsumes the plain 0.95 multiplier so they don't stack) |
| Confidence floor | `score = min(score, ParseConfidence)` | Prevents low-confidence rows from skipping review even without specific flag hits |

Every rule that fires pushes a stable token (`PrimaryValueNull`, `BadUnit`, `SoftRepair:PVT_MIGRATED`, `MissingRequired:Unit`, …) into a `Reasons` list emitted as `QC_PARSE_QUALITY:REVIEW_REASONS:{pipe-joined list}`. `ClaudeApiCorrectionService` forwards observations whose score is **strictly less than** `ClaudeReviewQualityThreshold` (default `0.75`); absent / unparseable / NaN scores forward conservatively.

### Stage 3.5: Claude AI Correction

`ClaudeApiCorrectionService` performs AI-powered post-parse correction of `ParsedObservation` objects before database write. After Stages 3.25 and 3.4 produce observations, the correction service sends table-level batches to Claude Haiku for semantic review and correction of misclassified fields.

**Common corrections:**
- PrimaryValueType misclassification (e.g., "Numeric" -> "Percentage" when table caption indicates "n(%)")
- SecondaryValueType confusion (e.g., "SD" vs "SE" vs "CV_Percent")
- Swapped TreatmentArm/ParameterName (row vs column confusion)
- Caption-derived hints not applied by regex parsers

**Deterministic guardrails:** `ClaudeCorrectionGuardrails.cs` validates each proposed correction before mutation using an ordered Chain of Responsibility. First rejection wins, and rejected proposals append `AI_REJECTED:{Field}:{Reason}` while preserving the original observation value.

Guardrail reasons currently include:

- `ProtectedField`
- `PlaceboClassFlip`
- `TreatmentArmNull`
- `TreatmentArmBodySystem`
- `TreatmentArmHeaderToken`
- `ParameterNameSuperset`
- `PercentColumnTypeDemotion`
- `TextRowUnitPercent`

The guardrail chain uses `ClaudeCorrectionContext` for source header and percent-column facts and `IPlaceboArmClassifier` for shared placebo-arm semantics. This keeps prompt guidance from being the only protection against harmful rewrites.

**Configuration:** Add `ClaudeApiCorrectionSettings` section to `appsettings.json`. API key must be stored in User Secrets:

```bash
dotnet user-secrets set "ClaudeApiCorrectionSettings:ApiKey" "sk-ant-..."
```

| Setting | Default | Description |
|---------|---------|-------------|
| `Enabled` | `true` | Master enable/disable switch |
| `Model` | `claude-haiku-4-5-20251001` | Claude model (Haiku for speed/cost) |
| `MaxObservationsPerRequest` | `50` | Max observations per API call |
| `DelayBetweenRequestsMs` | `200` | Rate limiting delay between calls |

The payload sent to Claude excludes token-heavy provenance fields (DocumentGUID, LabelerName, ProductTitle, VersionNumber, TextTableID) to minimize cost.

**Audit trail:** Corrected fields are flagged with `AI_CORRECTED:{FieldName}` in `ValidationFlags`; rejected fields are flagged with `AI_REJECTED:{FieldName}:{Reason}`. The service fails gracefully -- API errors return original observations unchanged.

### Stage 3.6: Post-Processing Extraction

`ColumnStandardizationService.PostProcessExtraction` re-runs targeted extraction rules after Claude correction to catch values that Claude corrected into extractable form. For example, if Claude restores a ParameterSubtype like `Cmax(pg/mL)` or an N= value, this stage extracts the unit or sample size that the earlier Phase 2 pass would have caught had the data been correct initially.

Post-processing flags use the `COL_STD:POST_` prefix to distinguish from Phase 2 corrections.

### Stage 4: Validation

Three validation services run post-parse:

| Service | Scope | Key Checks |
|---------|-------|------------|
| `RowValidationService` | Per-observation | Required fields by category, value type appropriateness, bound consistency, low confidence |
| `TableValidationService` | Per-table | Duplicate observations, arm coverage gaps, count reasonableness |
| `BatchValidationService` | Cross-table | Aggregate counts, confidence distribution, cross-version concordance (>50% row count divergence) |

Validation results are returned as in-memory DTOs (`BatchValidationReport`) and logged via ILogger. The orchestrator's `ProcessAllWithValidationAsync` integrates validation into the batch pipeline.

### Stage 5: Adverse Event Denormalization

Stage 5 produces two outputs. `tmp_FlattenedAdverseEventTable` is a denormalized, AE-only projection of `tmp_FlattenedStandardizedTable` where each row already carries pre-computed risk statistics. `tmp_FlattenedAdverseEventRiskTable` is a persistent materialization of `dbo.vw_AeRisk` refreshed after the AE stats table is populated, so real-time visualizations (RR scatter plots, RR heatmaps with hierarchical clustering, product/class risk views) bind directly without runtime joins or stats. The DDL scripts are at `MedRecPro/SQL/MedRecPro-Table-tmp_FlattenedAdverseEventTable.sql` and `MedRecPro/SQL/MedRecPro-Table-tmp_FlattenedAdverseEventRiskTable.sql`.

**Status:** Phase 1 (SQL DDL) and Phase 2 (the `AdverseEventDenormalizationService` population service, EF entities, DTO, `RelativeRiskCalculator` utility, helper decomposition, orchestrator hook, DI registration, and final risk-table materialization) are both shipped.

**Entry points:**

- Pipeline-integrated: `TableParsingOrchestrator.ProcessAllWithValidationAsync` invokes `IAdverseEventDenormalizationService.PopulateAsync` after Stage 4 validation when the dependency was provided.
- Standalone: resolve `IAdverseEventDenormalizationService` from DI and call `PopulateAsync` directly to re-run AE denormalization and risk-table materialization without re-doing Stage 3/4.

**Helper decomposition:**

| Helper | Responsibility |
|--------|----------------|
| `SourceRowEligibility` | Filters invalid AE source rows before comparator grouping while preserving eligible missing-ArmN rows for percentage-only fallback. |
| `ComparatorGrouper` | Builds deterministic study groups scoped by document/table/parameter context. |
| `ComparatorSelector` | Applies the placebo, lowest non-zero dose, and single-arm comparator cascade. |
| `AeStatEntityBuilder` | Projects source/comparator rows into `FlattenedAdverseEventTable` entities and wires RR/DNRR calculations. |
| `AeDenormalizationConstants` | Centralizes calculation method and flag token constants. |
| `IPlaceboArmClassifier` / `PlaceboArmClassifier` | Provides shared placebo-arm classification by delegating to `RelativeRiskCalculator.IsPlaceboArm(...)`. |

**Study group key:** source rows are first scoped by `(DocumentGUID, TextTableID)`, then `ComparatorGrouper` splits comparator cohorts by normalized `(ParameterName, ParameterSubtype, StudyContext, Population, Subpopulation)`. `TextTableID` is included so the same AE term appearing in multiple study tables of one document does not get a single comparator cross-paired across unrelated studies. Rows with NULL `DocumentGUID` are skipped with a warning log.

**Comparator cascade** (deterministic tie-breakers: `Dose` nulls-first, then `SourceRowSeq`, `SourceCellSeq`, source `Id`):

1. Placebo arm — `placebo`/`sham`/`vehicle` (case-insensitive) OR `Dose == 0` → `PLACEBO_COMPARATOR`
2. Lowest non-zero `Dose` → `LOW_DOSE_COMPARATOR`
3. Single-arm fallback → `NO_COMPARATOR` (stats NULL)

`ACTIVE_COMPARATOR` is reserved for a future phase that has arm-level UNII; the current source row carries only document-level UNII so active-comparator detection is not implementable.

**`IsPlaceboControlled`** is a row-level placebo-comparator flag. It is `1` iff the row's chosen comparator was a placebo arm (matches `placebo`/`sham`/`vehicle` regex or has Dose=0) — equivalent to `CalculationFlags LIKE 'PLACEBO_COMPARATOR%'` but indexable as a bit. May vary across rows of the same DocumentGUID and even within one TextTableID when a parameter group lacks a placebo row. Independent of the bit, the per-`(DocumentGUID, TextTableID)` trial-design classifier emits `AMBIGUOUS_TRIAL_DESIGN` into `CalculationFlags` when arm names cannot be reduced to a usable root.

**Math:**

- RR + 95% CI: Katz log-method. When `EventsTreatment == 0` or `EventsComparator == 0`, the Haldane-Anscombe continuity correction (`a' = a + 0.5`, `c' = c + 0.5`, `n1' = n1 + 1`, `n2' = n2 + 1`) is applied to BOTH the point estimate and the CI. Raw event counts are still persisted in `EventsTreatment` / `EventsComparator` for audit; the adjusted locals are scope-local to the calculation.
- DNRR: log-linear with intra-study reference dose `D_ref = MIN(Dose) WHERE Dose > 0` over the study group. `logDNRR = ln(RR) / ln(rowDose / D_ref)`.
- Denominator guard: RR and CI require positive `ArmN` and positive `ComparatorN`. Percentage rows missing either denominator cannot derive auditable event counts, so `EventsTreatment`, `EventsComparator`, `RR`, CI, DNRR, and `CalculationMethod` remain NULL while `NO_ARMN` and/or `NO_COMPARATOR_N` is emitted.

#### Output Schema

`tmp_FlattenedAdverseEventTable` -- 31 columns:

- **Surrogate PK (1)**: `tmp_FlattenedAdverseEventTableID` (IDENTITY)
- **Source projection (10)**: `tmp_FlattenedStandardizedTableID` (FK), `DocumentGUID`, `UNII`, `ParameterName`, `ParameterCategory`, `ArmN`, `Dose`, `DoseUnit`, `PrimaryValue`, `PrimaryValueType` -- copied verbatim from source; `PrimaryValueType` is never derived
- **Comparator metadata (4)**: `TreatmentArm`, `ComparatorArm`, `ComparatorN`, `IsPlaceboControlled` (BIT, NOT NULL DEFAULT 0; row-level placebo-comparator flag)
- **Derived event counts (2)**: `EventsTreatment` (a in 2x2), `EventsComparator` (c in 2x2)
- **Risk statistics (6)**: `RR`, `DNRR`, `RRLowerBound`, `RRUpperBound`, `DNRRLowerBound`, `DNRRUpperBound`
- **Log-scale companions (6, PERSISTED computed)**: `LogRR`, `LogRRLowerBound`, `LogRRUpperBound`, `LogDNRR`, `LogDNRRLowerBound`, `LogDNRRUpperBound` -- materialized on disk, auto-maintained by SQL Server, indexable; `CASE WHEN > 0 THEN LOG(...)` guards prevent `LOG(0)` / `LOG(NULL)` errors
- **Calculation provenance (2)**: `CalculationMethod` (e.g. `KATZ_LOG`), `CalculationFlags` (semicolon-delimited audit, e.g. `ZERO_CELL_CORRECTED;PLACEBO_COMPARATOR`)

Five nonclustered indexes: `DocumentGUID`, `UNII`, `ParameterName`, `ParameterCategory`, and the source FK column.

`tmp_FlattenedAdverseEventRiskTable` materializes the full `dbo.vw_AeRisk` projection. It adds `tmp_FlattenedAdverseEventRiskTableID` as an identity primary key, stores the source AE/standardized IDs, product/class identifiers and names, AE fields, significance fields, denominators/events, number-needed fields, RR/log-RR fields, provenance/context, `Dose decimal(18, 6)`, and `IsCombo bit`. Its nonclustered indexes cover source AE ID, source standardized ID, `DocumentGUID`, `(PharmacologicClassID, Significance)`, `(IsPlaceboControlled, Significance)`, `ParameterCategory`, plus computed prefix keys for long `UNII` and `ParameterName` values.

#### Statistical Contract (what Phase 2 must produce)

**Row inclusion.** Eligible source rows with `TableCategory = 'ADVERSE_EVENT'` produce one row, except the row chosen as the comparator within its study group (no self-comparison). `SourceRowEligibility` skips invalid source rows with unusable arm/value shapes before grouping; RR/CI/DNRR may still be NULL when statistical prerequisites are not met, and retained rows carry calculation provenance.

**Comparator pairing** (per study group `TextTableID + ParameterName + ParameterSubtype + StudyContext + Population + Subpopulation`, with normalized casing/whitespace; all keys are scoped within one DocumentGUID):

1. **Placebo arm** -- `TreatmentArm` matches `%placebo%`, `%sham%`, or `%vehicle%` (case-insensitive), OR `Dose = 0`
2. **Lowest non-zero `Dose`** in the group -- covers active-controlled and stepped-dose trials
3. **Single-arm** -- no comparator possible; stats remain NULL, flag `NO_COMPARATOR`

**`IsPlaceboControlled` semantics -- row-level placebo-comparator flag:**

The bit is `1` iff the row's chosen comparator (per the cascade above) was a placebo arm. It is equivalent to `CalculationFlags LIKE 'PLACEBO_COMPARATOR%'` but persisted as an indexable BIT. It can vary across rows of the same DocumentGUID, and even within one TextTableID when a parameter group lacks a placebo row.

**Diagnostic-only:** the per-`(DocumentGUID, TextTableID)` trial-design classifier categorizes the table's arm composition into one of the kinds below. The kind itself is *not persisted* on the row; only the `AMBIGUOUS_TRIAL_DESIGN` case adds a flag to `CalculationFlags`. None of these kinds drive the `IsPlaceboControlled` bit.

| Per-table arm composition                                              | `TrialDesignKind` |
|------------------------------------------------------------------------|:------------------|
| Drug arm(s) of one root + placebo arm(s)                               | `PLACEBO_ONLY` (or `STEPPED_DOSE_PLUS_PLACEBO` for >1 dose) |
| Drug arms with multiple distinct roots + placebo                       | `PLACEBO_PLUS_ACTIVE` |
| Drug arms only, single root                                             | `STEPPED_DOSE_MONOTHERAPY` |
| Drug arms only, multiple roots                                          | `ACTIVE_ONLY` |
| Single arm                                                              | `SINGLE_ARM` |
| Arm names won't reduce to a usable root                                | `AMBIGUOUS` (emits `AMBIGUOUS_TRIAL_DESIGN` flag) |

**"Drug-name root"** is extracted by stripping numeric dose tokens (e.g. `50 mg`, `5 mg/kg`, `25 IU`) and common regimen tokens (`qd`, `bid`, `daily`, etc.) from the `TreatmentArm` string and lowercasing. The classifier uses arm-name roots, NOT the document's UNII column.

`CalculationFlags` records the row's actual comparator type (`PLACEBO_COMPARATOR`, `LOW_DOSE_COMPARATOR`, `NO_COMPARATOR`); `ACTIVE_COMPARATOR` is reserved for a future phase with arm-level UNII.

**Like-typed comparison constraint.** RR/CI/DNRR are computed only when treatment and comparator share the same `PrimaryValueType`:

| Treatment | Comparator | Action |
|-----------|------------|--------|
| `Percentage` | `Percentage` | Compute (events = `ArmN x PrimaryValue / 100`) |
| `Numeric` (count) | `Numeric` (count) | Compute (events = `PrimaryValue`) |
| any other (`Mean`, `Median`, etc.) | same | NULL stats, flag `UNCOMPARABLE_VALUE_TYPE` |
| mismatch | mismatch | NULL stats, flag `MIXED_VALUE_TYPES` |

**Relative Risk (Katz log-method).** Let `a` = `EventsTreatment`, `n1` = `ArmN`, `c` = `EventsComparator`, `n2` = `ComparatorN`:

```
RR        = (a / n1) / (c / n2)
SE(logRR) = sqrt(1/a - 1/n1 + 1/c - 1/n2)
RRLower   = exp(ln(RR) - 1.96 x SE)
RRUpper   = exp(ln(RR) + 1.96 x SE)
```

**Zero-cell correction (Haldane-Anscombe):** if `a == 0` or `c == 0`, add 0.5 to both `a` and `c`, and add 1 to both `n1` and `n2` (continuity correction, applied only for the SE step). Flag `ZERO_CELL_CORRECTED`.

**Dose-Normalized RR (DNRR) -- log-linear with intra-study reference dose:**

```
D_ref = MIN(Dose) over rows in same group WHERE Dose > 0

logDNRR        = ln(RR)        / ln(Dose / D_ref)
logDNRR_lower  = ln(RRLower)   / ln(Dose / D_ref)
logDNRR_upper  = ln(RRUpper)   / ln(Dose / D_ref)

DNRR        = exp(logDNRR)
DNRRLower   = exp(logDNRR_lower)
DNRRUpper   = exp(logDNRR_upper)
```

Skip DNRR (NULL) when: `Dose IS NULL OR Dose = 0` (placebo, already excluded as comparator), `Dose = D_ref` (denominator `ln(1) = 0`; flag `IS_REFERENCE_DOSE`), `D_ref` undefined (only one non-zero dose in group; flag `NO_DOSE_RANGE`), or `RR IS NULL`.

#### Why PERSISTED Computed Columns

The six `Log*` columns are SQL Server `PERSISTED` computed columns (materialized on disk, auto-maintained, indexable) rather than calculated in C#. This gives single-source-of-truth math and zero per-query overhead for the heatmap clustering case (which today calls `Math.log(rr)` at runtime). The `CASE WHEN > 0` guards make them safe even when source columns are 0 or NULL.

#### Stage 5 ValidationFlags (CalculationFlags column)

**Comparator-kind** (always emitted first, exactly one):

| Flag | Meaning |
|------|---------|
| `PLACEBO_COMPARATOR` | The chosen comparator row was a placebo/sham/vehicle arm or had `Dose = 0` |
| `LOW_DOSE_COMPARATOR` | The chosen comparator row was the lowest non-zero `Dose` in the group |
| `NO_COMPARATOR` | Single-arm trial; no comparator could be paired. RR/CI/DNRR are NULL |

`ACTIVE_COMPARATOR` is reserved for a future phase that has arm-level UNII (the current source row's `UNII` is document-level, plus-delimited).

**Math diagnostics:**

| Flag | Meaning |
|------|---------|
| `ZERO_CELL_CORRECTED` | Treatment or comparator events count was 0; Haldane-Anscombe continuity correction applied to BOTH point estimate and CI |
| `IS_REFERENCE_DOSE` | This row's `Dose` equals the group's `D_ref`, so DNRR denominator `ln(1) = 0`; DNRR is NULL |
| `NO_DOSE_RANGE` | The study group has no non-zero `Dose`; DNRR is undefined |
| `DOSE_UNIT_MISMATCH` | Treatment row's `DoseUnit` differs from the reference-dose row's `DoseUnit`; log-linear extrapolation across units is meaningless, DNRR is NULL |

**Type-mismatch:**

| Flag | Meaning |
|------|---------|
| `UNCOMPARABLE_VALUE_TYPE` | Both rows share a `PrimaryValueType` that doesn't yield event counts (e.g. both `Mean`); RR is NULL |
| `MIXED_VALUE_TYPES` | Treatment and comparator have different `PrimaryValueType`; calculation requires like-typed pairs |

**Hard guards:**

| Flag | Meaning |
|------|---------|
| `NO_ARMN` | Treatment `ArmN` is NULL or ≤ 0 — percentage-derived events and RR/CI/DNRR cannot be computed. |
| `NO_COMPARATOR_N` | Comparator `ArmN` is NULL or ≤ 0 |
| `INVALID_EVENT_COUNT` | `PrimaryValue` is NULL or negative on at least one side |
| `EVENTS_EXCEED_ARMN` | Derived event count exceeds `ArmN` (sanity-check failure) |
| `PERCENT_OUT_OF_RANGE` | `PrimaryValueType = 'Percentage'` and `PrimaryValue > 100` (defense-in-depth; parser rejects upstream) |

**Trial-design:**

| Flag | Meaning |
|------|---------|
| `AMBIGUOUS_TRIAL_DESIGN` | The per-table trial-design classifier could not extract usable arm-name roots (e.g., arms named only by dose). Pure diagnostic — no longer affects `IsPlaceboControlled` (which is comparator-driven). |

`CalculationMethod` is `KATZ_LOG` whenever a numeric RR was produced; NULL when stats remain NULL.

### Column Contracts by Table Category

Each observation context column has a strict, context-dependent definition locked to the row's TableCategory. The same column name carries different semantic meaning depending on the table type.

**Legend:** **R** = Required (flag if missing), **E** = Expected (usually populated), **O** = Optional, **N** = NULL (not applicable -- enforced by Phase 4)

| Column | AdverseEvent | PK | DrugInteraction | Efficacy |
|--------|:---:|:---:|:---:|:---:|
| ParameterName | R: MedDRA PT | R: PK param | R: PK param | R: Endpoint |
| ParameterCategory | E: Canonical SOC | N | N | N |
| ParameterSubtype | O: Severity | O: PK qualifier | R: **Co-admin drug** | O: Analysis pop |
| TreatmentArm | R: Drug/Placebo | O | E: Index drug | R: Drug vs comparator |
| ArmN | E | O | O | E |
| StudyContext | O | O | O | O |
| DoseRegimen | O | E | E | O |
| Population | O | O | O | O |
| Timepoint | N | O | N | O |
| PrimaryValueType | R: Percentage/Count | R: AM/GM/Median | R: GMR | R: HR/OR/RR/Percentage |
| Unit | E: % | R: conc/time/vol | O: ratio | O: % |
| Default BoundType | 95CI | 90CI | 90CI | 95CI |

**Cross-table comparison keys** (columns that must match for meaningful comparison):

| Category | Comparison Key |
|----------|----------------|
| AdverseEvent | ParameterName + TreatmentArm + DoseRegimen |
| PK | ParameterName + DoseRegimen + Population + Timepoint + PrimaryValueType + Unit |
| DrugInteraction | ParameterName + ParameterSubtype + TreatmentArm |
| Efficacy | ParameterName + TreatmentArm + PrimaryValueType |

### Output Schema

`tmp_FlattenedStandardizedTable` -- 41 non-key columns organized into 5 groups:

- **Provenance (9)**: DocumentGUID, LabelerName, ProductTitle, VersionNumber, UNII, TextTableID, Caption, SourceRowSeq, SourceCellSeq
- **Classification (4)**: TableCategory, ParentSectionCode, ParentSectionTitle, SectionTitle
- **Observation Context (13)**: ParameterName, ParameterCategory, ParameterSubtype, TreatmentArm, ArmN, StudyContext, DoseRegimen, Dose, DoseUnit, Population, Timepoint, Time, TimeUnit
- **Decomposed Values (10)**: RawValue, PrimaryValue, PrimaryValueType, SecondaryValue, SecondaryValueType, LowerBound, UpperBound, BoundType, PValue, Unit
- **Validation (5)**: ParseConfidence, ParseRule, FootnoteMarkers, FootnoteText, ValidationFlags

### Enum Definitions

#### PrimaryValueType (canonical, tightened)

```
ArithmeticMean - GeometricMean - GeometricMeanRatio - Median -
Percentage - Count - PercentChange - HazardRatio - OddsRatio -
RelativeRisk - RiskDifference - LSMean - Numeric - Text - PValue -
CodedExclusion
```

#### SecondaryValueType

```
SD - SE - CV_Percent - Count - Denominator
```

#### BoundType

```
90CI - 95CI - 99CI - CI - Range - SD - IQR
```

#### ParseRule

```
empty_or_na - pvalue - frac_pct - fraction_count - n_equals - n_pct -
inequality_percent - count_inequality_percent - value_cv - value_plusminus -
value_plusminus_sample - value_paren_dispersion - value_ci - rr_ci - diff_ci -
range_to - percentage - plain_number - value_trailing_unit - letter_code -
text_descriptive - plus contextual suffixes such as +caption, +row_pvalue,
+lt_one_percent_context, +pct_gt100_rejected, and +pk_pct_gt100_demoted
```

## ValidationFlags Dictionary

Every observation accumulates audit flags in `ValidationFlags` as a semicolon-delimited string. Each flag records what happened to the observation as it passed through the pipeline. Flags are grouped by the stage that produces them.

### Stage 3: Parser Flags

These flags are set by `BaseTableParser` and `ValueParser` during initial parsing.

| Flag | Meaning |
|------|---------|
| `CAPTION_HINT:{source}` | Caption/header provided a hint that promoted a bare numeric to a specific PrimaryValueType or set SecondaryValueType/BoundType. `{source}` is the hint origin (e.g., "Mean (SD)", "Geometric Mean"). |
| `CAPTION_REINTERPRET:n_pct->{pvt}({svt})` | An n_pct value (count + percentage) was reinterpreted per caption context to a different PrimaryValueType/SecondaryValueType pairing. |
| `PLUSMINUS_TYPE_INFERRED:SD` | The ± dispersion type could not be resolved from caption, header path, or footnotes. Defaulted to SD (most common in PK tables). Observations with this flag should be reviewed if the actual type matters. |
| `PCT_CHECK:PASS` | Percentage cross-validation passed -- the derived percentage from count/ArmN matches the reported percentage within 1.5 points. |
| `PCT_CHECK:WARN:{derived}` | Percentage cross-validation failed -- the derived percentage differs from the reported value by more than 1.5 points. `{derived}` is the calculated percentage. |
| `INEQUALITY_UPPER:{symbol}` | Parsed value is an upper-bound inequality, usually from `<` or `<=` percentage text. |
| `PCT_DERIVED_FROM_COUNT_LT:ArmN={n}` | Count-plus-less-than percentage was converted to an incidence percentage using the arm denominator. |
| `PCT_DERIVED_FROM_COUNT_LT:fallback=0.1` | Count-plus-less-than percentage was converted with the conservative fallback when no arm denominator was available. |
| `PCT_LT_DISPLAY:{bound}` | Original less-than percentage bound displayed in a count-plus-inequality cell. |
| `PCT_CONTEXT_PROMOTION` | Bare numeric value was promoted to percentage because AE/Efficacy row/header/caption context indicates percent reporting. |
| `P_VALUE_ROW_CONTEXT` | Explicit p-value row/header context forced the parsed value to `PrimaryValueType=PValue`. |
| `PCT_GT100_REJECTED:{value}` | A would-be percentage greater than 100 was rejected and demoted to a safer type. |
| `PK_PCT_GT100_DEMOTED` | PK-specific parenthetical-stat demotion handled a percentage-over-100 shape without changing AE/Efficacy `n (%)` behavior. |

### Stage 3.25: Column Standardization Flags (COL_STD)

These flags are set by `ColumnStandardizationService` during the 4-phase deterministic pipeline. Every flag starts with `COL_STD:` for audit trail purposes.

#### Phase 1: Arm/Context Corrections

| Flag | Meaning |
|------|---------|
| `COL_STD:ARM_WAS_N` | TreatmentArm was a sample size value (e.g., "(N=267)"). Moved to ArmN; arm name recovered from StudyContext if available. |
| `COL_STD:ARM_WAS_FMT` | TreatmentArm was a format hint (e.g., "%", "n(%)"). Discarded; arm name recovered from StudyContext. |
| `COL_STD:ARM_WAS_SEVERITY` | TreatmentArm was a severity grade (e.g., "Severe", "Grades 3/4"). Moved to ParameterSubtype. |
| `COL_STD:ARM_WAS_DOSE` | TreatmentArm was a dose regimen (e.g., "10 mg daily"). Moved to DoseRegimen. |
| `COL_STD:ARM_WAS_BARE_DOSE` | TreatmentArm was a bare number that combined with a dose descriptor in StudyContext to form a DoseRegimen. |
| `COL_STD:SPLIT_DRUG_DOSE` | TreatmentArm contained both a drug name and dose (e.g., "Losartan 50 mg"). Split: drug stays in TreatmentArm, dose moved to DoseRegimen. |
| `COL_STD:CTX_WAS_ARM_N` | StudyContext contained a drug name with embedded N= (e.g., "Placebo (N=300) n(%)"). Split: drug -> TreatmentArm, N -> ArmN, format hint discarded. |
| `COL_STD:SWAP_ARM_CTX` | TreatmentArm and StudyContext were swapped because StudyContext contained a drug name and TreatmentArm did not. |
| `COL_STD:CTX_WAS_DESC` | StudyContext was a descriptor hint (e.g., "Incidence", "Reaction", "% of Patients"). Cleared. |
| `COL_STD:ARM_STRIP_PCT` | Trailing `%` or `n(%)` format hint stripped from TreatmentArm. PrimaryValueType promoted to Percentage if it was "Numeric". |
| `COL_STD:ARM_BRACKET_N` | TreatmentArm contained a bracketed or embedded N= value (e.g., "Placebo [N=459]", "Drug N=339"). Extracted N -> ArmN, cleaned TreatmentArm. |

#### Phase 2: Content Normalization

| Flag | Meaning |
|------|---------|
| `COL_STD:N_STRIPPED:{Column}` | An inline N= pattern was found and stripped from `{Column}` (e.g., TreatmentArm, StudyContext, DoseRegimen, RawValue). The N value was populated into ArmN if not already set. |
| `COL_STD:PK_SUBPARAM_ROUTED` | A PK sub-parameter name (e.g., Cmax, AUC) was found in DoseRegimen and routed to ParameterSubtype. |
| `COL_STD:COADMIN_ROUTED` | A co-administered drug name was found in DoseRegimen or ParameterName and routed to ParameterSubtype (DRUG_INTERACTION tables). |
| `COL_STD:POPULATION_EXTRACTED` | A population descriptor was found in DoseRegimen and moved to the Population column. |
| `COL_STD:TIMEPOINT_EXTRACTED` | A timepoint descriptor was found in DoseRegimen and moved to the Timepoint column. |
| `COL_STD:ROW_TYPE=CAPTION` | ParameterName was a caption echo (e.g., "Table 3: Adverse Events"). Cleared. |
| `COL_STD:ROW_TYPE=HEADER` | ParameterName was a header echo (e.g., bare "n" or "N"). Cleared. |
| `COL_STD:PARAM_WAS_DOSE` | ParameterName was a dose value. Moved to DoseRegimen. |
| `COL_STD:HTML_ENTITY_DECODED` | HTML entities (e.g., `&amp;`, `&#8805;`) were decoded in ParameterName. |
| `COL_STD:ARM_WAS_HEADER` | TreatmentArm was a leaked column header (e.g., "Number of Patients", "Percent of Subjects"). Cleared. |
| `COL_STD:ARM_WAS_GENERIC` | TreatmentArm was a generic label (e.g., "Treatment", "PD", "Drug"). Cleared or replaced with drug name from ProductTitle. |
| `COL_STD:ARM_WAS_STUDY` | TreatmentArm was a study name (e.g., "Trial 1", "Phase III"). Moved to StudyContext. |
| `COL_STD:DOSE_EXTRACTED` | An embedded dose was found in TreatmentArm (e.g., "Drug 150 mg/d"). Dose moved to DoseRegimen. |
| `COL_STD:PK_SUBPARAM_UNIT_EXTRACTED` | A unit was extracted from trailing parenthesized content in ParameterSubtype (e.g., `Cmax(pg/mL)` -> Unit=`pg/mL`). PK and DRUG_INTERACTION categories only. |
| `COL_STD:UNIT_HEADER_LEAK` | Unit column contained a leaked header value (drug name, long string, or keyword like "Regimen"). Cleared. |
| `COL_STD:UNIT_NORMALIZED` | Unit value was normalized to canonical spelling (e.g., `mcg h/mL` -> `mcg*h/mL`, `hr` -> `h`, `ug/mL` -> `mcg/mL`). |
| `COL_STD:SOC_NORMALIZED` | ParameterCategory (System Organ Class) was normalized to its canonical MedDRA name (e.g., "Gastrointestinal Disorders" -> "Gastrointestinal disorders"). |
| `COL_STD:SOC_UNMATCHED` | ParameterCategory did not match any known SOC variant in the canonical map. Left as-is for manual review. |

#### Phase 3: PrimaryValueType Migration

| Flag | Meaning |
|------|---------|
| `COL_STD:PVT_MIGRATED:{old}->{new}` | PrimaryValueType was migrated from an old enum value to a new canonical value (e.g., `Mean->ArithmeticMean`, `Percentage->Percentage`). |
| `COL_STD:PVT_UNRESOLVED` | PrimaryValueType was "Numeric" but could not be resolved to a more specific type based on category, unit, and value context. Left as "Numeric". |

#### Phase 4: Column Contract Enforcement

| Flag | Meaning |
|------|---------|
| `COL_STD:NULL_{Column}` | Column was set to null because it is Not Applicable (N) for this TableCategory (e.g., Timepoint nulled for ADVERSE_EVENT). |
| `COL_STD:MISSING_R_{Column}` | A Required (R) column for this TableCategory is empty. Flagged for review. |
| `COL_STD:BOUND_TYPE_INFERRED` | BoundType was inferred from the TableCategory default because LowerBound/UpperBound were populated but BoundType was null (90CI for PK/DDI, 95CI for Efficacy). |

#### Confidence Provenance

| Flag | Meaning |
|------|---------|
| `CONFIDENCE:PATTERN:{score}:{reason}({count})` | Summary of deterministic standardization. `{score}` is ParseConfidence at time of standardization. `{reason}` is `clean` (0 corrections), `minor` (1-2 corrections), or `major` (3+ corrections). `{count}` is the total number of corrections applied. |

### Stage 3.4: Parse-Quality Flags (QC_PARSE_QUALITY)

These flags are emitted by `ParseQualityService` on every observation. They drive the Stage 3.5 Claude forwarding decision.

| Flag | Meaning |
|------|---------|
| `QC_PARSE_QUALITY:{score}` | Deterministic parse-quality score in [0,1] (4 decimal places). 1.0 = clean parse, no penalties; lower values = more parse-alignment failures. Emitted on every observation. |
| `QC_PARSE_QUALITY:REVIEW_REASONS:{list}` | Pipe-delimited list of rule names that fired when the score is below the Claude review threshold — e.g. `PrimaryValueNull\|BadUnit\|SoftRepair:PVT_MIGRATED`. Audit trail for every Claude forward. |

The `ClaudeApiCorrectionService` forwards observations whose `QC_PARSE_QUALITY` score is **strictly less than** `ClaudeApiCorrectionSettings.ClaudeReviewQualityThreshold` (default `0.75`). Observations without a quality flag pass through conservatively (forwarded to Claude).

### Stage 3.4: Shadow-Mode Classifier Flags (QC) — diagnostic only

The `QCNetCorrectionService` is preserved as shadow-mode infrastructure. By default it does **not** mutate observations — it only emits `*_SHADOW` flags so the classifier's predictions can be compared against the deterministic pipeline's output. The `*_CORRECTED` / `*_ROUTED_TO_*` / `PVTYPE_DISAMBIGUATED` variants only appear in historical data or if a deployment explicitly disables shadow mode.

| Flag | Meaning |
|------|---------|
| `QC:CATEGORY_SHADOW:{label}:{score}` | Stage 1 classifier prediction — what `TableCategory` it WOULD have set, without mutating. Default emission when `EnableStage1ShadowMode=true`. |
| `QC:DOSEREGIMEN_SHADOW:{target}:{score}` | Stage 2 classifier prediction — what column DoseRegimen content WOULD have been routed to, without mutating. Default emission when `EnableStage2ShadowMode=true`. |
| `QC:CATEGORY_CORRECTED:{label}:{score}` | **Historical / non-shadow only.** TableCategory was overridden by the Stage 1 classifier. Only fires when `EnableStage1TableCategoryCorrection=true` (not default). |
| `QC:DOSEREGIMEN_ROUTED_TO_{target}:{score}` | **Historical / non-shadow only.** DoseRegimen content was rerouted. Only fires when `EnableStage2DoseRegimenRoutingCorrection=true` (not default). |
| `QC:PVTYPE_DISAMBIGUATED:{label}:{score}` | **Historical / non-shadow only.** PrimaryValueType was disambiguated from "Numeric" by the Stage 3 classifier. |

#### Confidence Provenance

| Flag | Meaning |
|------|---------|
| `CONFIDENCE:ML:{score}:{label}` | Historical summary flag from the shadow-mode pipeline. `{label}` is `CATEGORY_CORRECTED` / `DOSEREGIMEN_ROUTED` / `PVTYPE_DISAMBIGUATED` / `no_correction`. Present on observations from before shadow mode became default; in current data, expect `no_correction`. |

### Stage 3.5: Claude AI Correction Flags

These flags are set by `ClaudeApiCorrectionService` after the Claude API returns corrections.

| Flag | Meaning |
|------|---------|
| `AI_CORRECTED:{Field}` | The named field was corrected by Claude (e.g., `AI_CORRECTED:ParameterName`, `AI_CORRECTED:PrimaryValueType`, `AI_CORRECTED:TreatmentArm`). One flag per corrected field -- an observation can have multiple if Claude corrected several fields. |
| `CONFIDENCE:AI:{score}:{count}_corrections` | Summary of Claude correction. `{score}` is ParseConfidence after Claude. `{count}` is the number of fields Claude corrected on this observation (0 = no corrections). |

**Correctable fields:** ParameterName, PrimaryValueType, SecondaryValueType, TreatmentArm, DoseRegimen, Population, Unit, ParameterCategory, ParameterSubtype, Timepoint, TimeUnit, StudyContext, BoundType.

### Stage 3.6: Post-Processing Flags

These flags are set by `ColumnStandardizationService.PostProcessExtraction` when it re-extracts values after Claude correction. They mirror Phase 2 flags but use the `POST_` prefix.

| Flag | Meaning |
|------|---------|
| `COL_STD:POST_PK_SUBPARAM_UNIT_EXTRACTED` | Same as `PK_SUBPARAM_UNIT_EXTRACTED` but triggered during post-processing (Claude corrected a ParameterSubtype into extractable form). |
| `COL_STD:POST_N_STRIPPED:{Column}` | Same as `N_STRIPPED` but triggered during post-processing (Claude restored an N= value that wasn't present during Phase 2). |

### Stage 4: Row Validation Flags

These flags are set by `RowValidationService` during per-observation validation checks.

| Flag | Meaning |
|------|---------|
| `LOW_CONFIDENCE` | ParseConfidence is below 0.5. The observation's values may be unreliable. |
| `BOUND_INVERSION` | LowerBound is greater than UpperBound. Likely a parsing error or swapped values. |
| `TIME_UNIT_MISMATCH` | Time column is populated but TimeUnit is missing, or vice versa. |
| `UNREASONABLE_TIME` | Time value is <= 0, which is not valid for a timepoint. |
| `INVALID_TIME_UNIT` | TimeUnit contains a value not in the allowed vocabulary (h, min, d, wk, mo, yr, etc.). |

### Reading ValidationFlags

Flags are semicolon-delimited with spaces: `COL_STD:ARM_WAS_N; COL_STD:PVT_MIGRATED:Mean->ArithmeticMean; CONFIDENCE:PATTERN:0.90:minor(2)`. To query:

```sql
-- Find all observations where Claude corrected something
SELECT * FROM tmp_FlattenedStandardizedTable
WHERE ValidationFlags LIKE '%AI_CORRECTED%'

-- Find low-parse-quality observations (candidates for Claude review)
SELECT * FROM tmp_FlattenedStandardizedTable
WHERE ValidationFlags LIKE '%QC_PARSE_QUALITY:0.2%'
   OR ValidationFlags LIKE '%QC_PARSE_QUALITY:0.3%'
   OR ValidationFlags LIKE '%QC_PARSE_QUALITY:0.4%'
   OR ValidationFlags LIKE '%QC_PARSE_QUALITY:0.5%'
   OR ValidationFlags LIKE '%QC_PARSE_QUALITY:0.6%'
   OR ValidationFlags LIKE '%QC_PARSE_QUALITY:0.7%'

-- Find observations where unit was extracted from ParameterSubtype
SELECT * FROM tmp_FlattenedStandardizedTable
WHERE ValidationFlags LIKE '%PK_SUBPARAM_UNIT_EXTRACTED%'

-- Count corrections by type
-- (QC:CATEGORY_CORRECTED is historical/non-shadow-only — current data uses Claude-gated correction)
SELECT
    CASE
        WHEN ValidationFlags LIKE '%AI_CORRECTED%' THEN 'Claude'
        WHEN ValidationFlags LIKE '%COL_STD:%' THEN 'Deterministic'
        ELSE 'None'
    END AS CorrectionSource,
    COUNT(*) AS ObservationCount
FROM tmp_FlattenedStandardizedTable
GROUP BY CASE
    WHEN ValidationFlags LIKE '%AI_CORRECTED%' THEN 'Claude'
    WHEN ValidationFlags LIKE '%COL_STD:%' THEN 'Deterministic'
    ELSE 'None'
END
```

### Shared Dictionaries

The pipeline relies on several single-source-of-truth lookups housed under `Service/TransformationServices/Dictionaries/` (or as standalone services where the data is too large to embed inline). These replace the historical pattern of duplicating the same per-category data across multiple consumers.

| Dictionary | Source | Purpose |
|-----------|--------|---------|
| `CategoryProfileRegistry` | static class | Per-`TableCategory` profile bundling column contract (R/E/O/N), row-required fields, completeness fields, allowed `PrimaryValueType` set, default `BoundType`, and arm/time validation switches for the four parsed categories plus `SKIP`. Consumed by validation services and backed by `ColumnContractRegistry` for contract data. |
| `ColumnContractRegistry` | `IColumnContractRegistry` | Required / Expected / Optional / NullExpected column sets for the four parsed categories (`ADVERSE_EVENT`, `PK`, `DRUG_INTERACTION`, `EFFICACY`) transcribed from `TableStandards/column-contracts.md`. Consumed by `ColumnStandardizationService`, `ParseQualityService`, and validation profiles |
| `CategoryNameNormalizer` | static class | Resolves `ADVERSE_EVENT` <-> `AdverseEvent` and equivalent forms across the current `TableCategory` enum values |
| `PkParameterDictionary` | static class | ~35 canonical PK parameter names + aliases (Cmax, AUC, t½, Tmax, Cl, Vd) with Unicode folding |
| `UnitDictionary` | static class | ~80 known PK unit strings + ~16 variant-spelling normalizations (`mcg h/mL` -> `mcg*h/mL`) |
| `PdMarkerDictionary` | static class | 9 pharmacodynamic markers (IPA, VASP-PRI, Platelet Aggregation, etc.) for parser routing |
| `AeParameterCategoryDictionaryService` | scoped service | 1,189 unambiguous AE `ParameterName` -> canonical SOC mappings derived from production data, with name-variant collapsing |
| `ParsedObservationFieldAccess` | static class | Reflection-free `Get` / `GetAsString` / `Set` / `SetFromString` / `IsPopulated` for the observation context, value, bound, and type columns used by standardization contracts and Claude corrections |
| `ObservationFlagSnapshotBuilder` | static class | Occurrence-aware `ValidationFlags` snapshots for pre/post Claude report diffs when multiple observations originate from the same source cell |
| `ValidationFlagExtensions` | static class | Canonical `"; "`-delimited flag append on `ParsedObservation.ValidationFlags` |
| Drug Names | runtime (from DB) | ~500+ exact-match drug names from `vw_ProductsByIngredient` (loaded by `ColumnStandardizationService` at init) |
| Drug Abbreviations | inline | 13 common abbreviations not in formal product DB (AZA, MMF, CsA, etc.) |
| Unit Header Keywords | inline | 13 leak-detection keywords (Regimen, Dosage, Patients, etc.) |
| Canonical SOC Map | inline (`ColumnStandardizationService`) | ~55 MedDRA SOC variants -> 26 canonical names |
| PVT Direct Map | inline | 9 direct 1:1 `PrimaryValueType` migration mappings |

## Relationship to MedRecPro

This library contains **copies** of files from the main MedRecPro project. The source MedRecPro project remains unchanged and can continue to function independently. Key differences:

| Aspect | MedRecPro | MedRecProImportClass |
|--------|-----------|---------------------|
| SDK | Web SDK | Class Library SDK |
| Namespace | `MedRecPro.*` | `MedRecProImportClass.*` |
| Web Dependencies | Full ASP.NET Core | Minimal (IFormFile only) |
| SPL Import | Full support | Full support |
| Orange Book Import | Not included | Full support |
| DTO Queries | Full support | Not included |
| Rendering Services | Included | Not included |

## Limitations

- **No DTO Query Support**: The `GetCompleteLabelsAsync` methods throw `NotSupportedException`. Use the full MedRecPro project for DTO-based queries.
- **No Rendering**: SPL rendering services are not included. This library is for import only.
- **Web Context**: Some utility methods that depend on `HttpContext` return empty values when called outside a web context.
- **Table Normalization Corpus**: The SPL Table Normalization pipeline is designed for the full 250K+ label corpus and requires batch processing by TextTableID range for memory management.

## Building

```bash
cd MedRecProImportClass
dotnet build
```

## Publishing with MedRecProConsole

The console application can be published as a self-contained single file:

```bash
cd MedRecProConsole
dotnet publish -c Release
```

Output: `bin/Release/net8.0/win-x64/publish/MedRecProConsole.exe` (~42MB)

## License

This project is part of the MedRecPro application suite. See the main MedRecPro repository for licensing information.
