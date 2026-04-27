# MedRecProImportClass

A standalone .NET 8 class library extracted from the MedRecPro web application to support SPL (Structured Product Labeling) and FDA Orange Book import operations. This library is designed to be used by console applications and other non-web clients that need to import FDA drug data.

## Purpose

This library was created to enable single-file publishing for the `MedRecProConsole` application. The main MedRecPro project is a Web SDK project which conflicts with self-contained deployment requirements. By extracting the import-related functionality into a dedicated class library, console applications can reference only the components they need without pulling in web-specific dependencies.

## Features

- **SPL XML Parsing**: Complete parsing infrastructure for FDA SPL documents
- **FDA Orange Book Import**: Parses `products.txt`, `patent.txt`, and `exclusivity.txt` (tilde-delimited) from Orange Book ZIP files with idempotent upserts and multi-tier entity matching to existing SPL data, plus embedded patent use code definitions
- **Entity Framework Core Integration**: Database context and repository pattern for data persistence
- **SPL Table Normalization**: Multi-stage pipeline transforms heterogeneous FDA drug label tables into a uniform 38-column analytical schema (`tmp_FlattenedStandardizedTable`) for cross-product meta-analysis -- includes Stage 0 bioequivalent ANDA dedup, table reconstruction, 8 section-aware parsers, 4-phase column standardization with per-category contract enforcement, deterministic parse-quality gate, Claude AI correction (gated by quality score), post-processing extraction, and automated validation
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
|   +-- ValidationFlagExtensions.cs   # Canonical "; "-delimited flag append
|   +-- ...
+-- Models/                 # Entity classes and DTOs
|   +-- Labels.cs           # Main Label container with 50+ nested classes
|   +-- OrangeBook.cs       # Orange Book entity classes (Applicant, Product, Patent, etc.)
|   +-- Import.cs           # Import result types
|   +-- ImportData.cs       # SplData entity
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
    +-- TransformationServices/   # SPL Table Standardization pipeline
        +-- BioequivalentLabelDedupService.cs   # Stage 0: ANDA + repackager dedup by Orange Book group
        +-- TableCellContextService.cs          # Stage 1: source view assembly
        +-- TableReconstructionService.cs       # Stage 2: table reconstruction
        +-- ValueParser.cs                      # Stage 3: regex-based value decomposition
        +-- PopulationDetector.cs               # Stage 3: population auto-detection
        +-- DoseExtractor.cs                    # Stage 3: dose number + unit extraction
        +-- BaseTableParser.cs                  # Stage 3: shared parser helpers
        +-- PkTableParser.cs                    # Stage 3: pharmacokinetic tables
        +-- SimpleArmTableParser.cs             # Stage 3: single-header AE/efficacy
        +-- MultilevelAeTableParser.cs          # Stage 3: two-row header AE tables
        +-- AeWithSocTableParser.cs             # Stage 3: AE with SOC dividers
        +-- EfficacyMultilevelTableParser.cs    # Stage 3: two-row header efficacy
        +-- BmdTableParser.cs                   # Stage 3: bone mineral density
        +-- TissueRatioTableParser.cs           # Stage 3: tissue-to-plasma ratio
        +-- DosingTableParser.cs                # Stage 3: dosing parameter grids
        +-- TableParserRouter.cs                # Stage 3: section code -> parser routing
        +-- ColumnStandardizationService.cs     # Stage 3.25: 4-phase column contracts
        +-- ColumnContractRegistry.cs           # Stage 3.25/4: per-category R/E/O/N column sets
        +-- AeParameterCategoryDictionaryService.cs # Stage 3.25: AE parameter -> SOC resolution (1189 entries)
        +-- ParseQualityService.cs              # Stage 3.4: deterministic parse-quality gate (drives Claude forwarding)
        +-- ClaudeApiCorrectionService.cs       # Stage 3.5: AI-powered correction (gated by Stage 3.4 score)
        +-- QCNetCorrectionService.cs           # Shadow-mode ML.NET classifiers — emits diagnostic flags only, does not correct
        +-- QCTrainingStore.cs                  # Persistent training data for the shadow-mode classifiers
        +-- TableParsingOrchestrator.cs         # Batch loop + stage sequencing + DB writes
        +-- RowValidationService.cs             # Stage 4: per-observation checks
        +-- TableValidationService.cs           # Stage 4: cross-row checks
        +-- BatchValidationService.cs           # Stage 4: aggregate reporting
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
| Microsoft.AspNetCore.Identity.EntityFrameworkCore | 8.0.15 | User entity support |
| Microsoft.Extensions.Hosting | 9.0.4 | Background service support |
| Microsoft.Extensions.Logging | 9.0.4 | Logging infrastructure |
| Microsoft.ML | 4.0.2 | ML.NET — backs the shadow-mode `QCNetCorrectionService` classifiers (diagnostic flags only; not part of the active correction flow) |
| Dapper | 2.1.66 | Micro-ORM for complex queries |
| Newtonsoft.Json | 13.0.3 | JSON serialization |
| Microsoft.Extensions.Http | 9.0.4 | HttpClient factory for Claude API |
| HtmlAgilityPack | 1.12.1 | HTML parsing |
| HtmlSanitizer | 9.0.884 | HTML sanitization |

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

A multi-stage pipeline that transforms heterogeneous FDA drug label table data into a uniform 38-column analytical schema for cross-product meta-analysis with classical ML. The full corpus is 250K+ labels; the pipeline supports batch processing by TextTableID range.

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
  TableParserRouter -> ITableParser (8 parsers) -> List<ParsedObservation>
        |
Stage 3.25: Column Standardization (deterministic)
  ColumnStandardizationService -> 4-phase pipeline (all categories)
        |
Stage 3.4: Parse-Quality Gate (deterministic, rule-based)
  ParseQualityService -> score in [0,1] per observation; rows below threshold (0.75) forward to Claude
        |
Stage 3.5: Claude AI Correction (gated by Stage 3.4 quality score)
  ClaudeApiCorrectionService -> semantic review + field correction
        |
Stage 3.6: Post-Processing Extraction
  ColumnStandardizationService.PostProcessExtraction -> catch values AI corrected into extractable form
        |
  TableParsingOrchestrator -> bulk write to tmp_FlattenedStandardizedTable
        |
Stage 4: Validation
  RowValidationService + TableValidationService + BatchValidationService -> BatchValidationReport
```

### Stage 0: Bioequivalent Label Dedup

`BioequivalentLabelDedupService` collapses multiple ANDA labels (and their repackager relabelings) referencing the same innovator down to one canonical DocumentGUID per bioequivalent group, preventing aggregate-signal inflation when the same published value appears 40+ times across generic/repackager labels.

- **Group key**: `Ingredient + DosageForm + Route` from Orange Book (all strengths collapse).
- **Selection priority**: NDA preferred over ANDA; within the chosen tier, the DocumentGUID with the most recent `LabelEffectiveDate` wins, tie-breaking on higher `VersionNumber` then lower DocumentGUID (ordinal).
- **Unclassifiable handling**: drops DocumentGUIDs that cannot be resolved to an Orange Book `(ApplType, ApplNo)` pair, with three distinct reason codes: `no_application_number`, `unrecognized_prefix`, `no_orange_book_match`.
- **UNII walk-order preserved**: kept rows retain their original UNII ordering — important because the Stage 3.4 ML training accumulator relies on UNII locality.
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

`TableParserRouter` maps `ParentSectionCode` to a `TableCategory` and selects the most specific parser via priority ordering. `TableParsingOrchestrator` runs the batch loop: reconstruct -> route -> parse -> standardize -> ML correct -> Claude correct -> post-process -> write to `tmp_FlattenedStandardizedTable`.

### Table Categories

The `TableCategory` column is the single most important classification value -- all downstream normalization rules, PrimaryValueType assignment, BoundType defaults, and ParameterSubtype interpretation depend on it.

| Category | Description | Typical Source Section |
|----------|-------------|------------------------|
| `ADVERSE_EVENT` | Incidence/frequency of adverse events by treatment arm | 34084-4 Adverse Reactions |
| `PK` | Pharmacokinetic parameters (Cmax, AUC, t1/2, etc.) | 43685-7 Clinical Pharmacology |
| `DRUG_INTERACTION` | Co-admin drug effects on PK parameters (geometric mean ratios) | 34073-7 Drug Interactions |
| `EFFICACY` | Comparative efficacy outcomes with risk measures and CIs | 34076-0 Clinical Studies |
| `DOSING` | Recommended doses, titration schedules, adjustments | 34068-7 Dosage and Administration |
| `BMD` | Bone mineral density at anatomical sites over time | 34076-0 Clinical Studies |
| `TISSUE_DISTRIBUTION` | Drug concentration across body tissues and fluids | 43685-7 Clinical Pharmacology |
| `DEMOGRAPHIC` | Baseline patient characteristics | 34076-0 Clinical Studies |
| `LABORATORY` | Lab parameter changes/shifts | 34084-4 Adverse Reactions |
| `TEXT_DESCRIPTIVE` | 100% text cells -- instructions, descriptions | Various |
| `UNCLASSIFIED` | Could not be classified deterministically | Various |
| `SKIP` | Tables to exclude (patient info, NDC, formulas) | Various |

#### Classification Decision Tree (Tier 1)

Tables are classified by applying tests in priority order against all rows sharing a TextTableID, plus Caption and ParentSectionCode. First match wins:

1. 100% of PrimaryValueType = "Text" -> **TextDescriptive**
2. MedDRA PT dictionary match (>=3 ParameterNames or >=2 SOC categories) -> **AdverseEvent**
3. PK parameter dictionary match (Cmax, AUC, t1/2, CL/F, Vss, etc.) -> **PK** or **DrugInteraction** (DDI if caption contains "drug interaction", "co-administered", "in the presence of")
4. PrimaryValueType contains RelativeRiskReduction or RiskDifference -> **Efficacy**
5. Dosing keywords in ParameterName -> **Dosing**
6. BMD anatomical site dictionary match -> **BMD**
7. Tissue/organ dictionary match with concentration units -> **TissueDistribution**
8. ParentSectionCode fallback (34084-4 -> AE, 43685-7 -> PK, 34068-7 -> Dosing, 34073-7 -> DDI)
9. No match -> **Unclassified**

Tables landing in Unclassified (~28%) are candidates for Tier 2 ML.NET classification using `LightGbmMulticlassTrainer` with 21 aggregated features per TextTableID (target: Macro F1 >= 0.85).

### Parsers

| Parser | Category | Structural Trigger |
|--------|----------|--------------------|
| `PkTableParser` | PK | Columns are PK parameters, rows are dose regimens |
| `SimpleArmTableParser` | AE / EFFICACY | Single-header with treatment arm columns |
| `MultilevelAeTableParser` | AE | Two-row header (colspan study contexts + arm sub-headers) |
| `AeWithSocTableParser` | AE | Single-header with SOC divider rows in body |
| `EfficacyMultilevelTableParser` | EFFICACY | Two-row header with stat columns (ARR, RR, P-value) |
| `BmdTableParser` | BMD | Columns are timepoints (Week/Month/Year) |
| `TissueRatioTableParser` | TISSUE_DISTRIBUTION | Two-column tissue/ratio tables |
| `DosingTableParser` | DOSING | Dosing parameter grid tables |

### Value Decomposition

`ValueParser` applies 13 regex patterns in priority order to decompose cell text into structured components: PrimaryValue, SecondaryValue, CI bounds, P-value, unit, and confidence score. Includes PCT_CHECK validation when arm sample size is available.

### Population Detection

`PopulationDetector` auto-detects study population from Caption/SectionTitle using regex extraction and a keyword dictionary, with Levenshtein-based fuzzy cross-validation between sources.

### Stage 3.25: Column Standardization

`ColumnStandardizationService` is a deterministic, rule-based service that processes ALL table categories (except SKIP) through a 4-phase pipeline. It corrects systematic misclassification caused by the diversity of FDA table layouts -- doses appearing as column headers, N-values in arm positions, study names in the wrong header row, etc.

#### Phase 1: Arm/Context Corrections (AE + EFFICACY only)

11 ordered rules applied most-specific to least-specific, relocating misclassified content from TreatmentArm and StudyContext to their correct columns:

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

Seven sub-passes clean up column content across all table categories:

| Sub-pass | Target Column | Key Operations |
|----------|---------------|----------------|
| `normalizeInlineNValues` | All columns | Strips N= patterns from every non-RawValue column, populates ArmN. Supports comma-formatted numbers. |
| `normalizeDoseRegimen` | DoseRegimen | Routes PK sub-params (Cmax, AUC, etc.) -> ParameterSubtype; co-admin drug names -> ParameterSubtype; residual population/timepoint -> their correct columns |
| `normalizeParameterName` | ParameterName | Removes caption echoes ("Table 3..."), header echoes ("n"), bare dose integers; decodes HTML entities; collapses OCR artifacts |
| `normalizeTreatmentArm` | TreatmentArm | Removes header echoes ("Number of Patients"), generic labels ("Treatment", "PD"); extracts embedded N= and doses; routes study names -> StudyContext |
| `extractUnitFromParameterSubtype` | ParameterSubtype | Extracts units from trailing parenthesized content in PK/DDI subtypes (e.g., `Cmax(pg/mL)` -> Subtype=`Cmax`, Unit=`pg/mL`; `Cmax(serum, mcg/mL)` -> Subtype=`Cmax, serum`, Unit=`mcg/mL`). Normalizes variant spellings. |
| `normalizeUnit` | Unit | Detects leaked column headers (>30 chars, drug names, keywords); normalizes variant spellings (`mcg h/mL` -> `mcg*h/mL`); extracts real units from verbose descriptions |
| `normalizeParameterCategory` | ParameterCategory | Canonical MedDRA SOC mapping (~55 variants -> 26 canonical names) with OCR artifact repair. AE tables only. |

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
| `Numeric` | context-resolved | AE+% -> Percentage, AE+int -> Count, PK -> ArithmeticMean, DDI -> GeometricMeanRatio, BMD -> PercentChange, Efficacy+bounds -> HazardRatio |

#### Phase 4: Column Contract Enforcement (ALL categories)

Enforces per-TableCategory contracts defining which columns are Required (R), Expected (E), Optional (O), or Not Applicable (N) for each table type:

- **NULL enforcement**: Columns marked N/A are set to null (e.g., Timepoint for AdverseEvent, ParameterCategory for PK)
- **Missing required flagging**: Columns marked Required that are empty produce `COL_STD:MISSING_R_{Column}` flags
- **Default BoundType**: When LowerBound/UpperBound are populated but BoundType is null, applies category defaults (90CI for PK/DDI, 95CI for Efficacy/BMD)

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

**Audit trail:** Corrected fields are flagged with `AI_CORRECTED:{FieldName}` in `ValidationFlags`. The service fails gracefully -- API errors return original observations unchanged.

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

### Column Contracts by Table Category

Each observation context column has a strict, context-dependent definition locked to the row's TableCategory. The same column name carries different semantic meaning depending on the table type.

**Legend:** **R** = Required (flag if missing), **E** = Expected (usually populated), **O** = Optional, **N** = NULL (not applicable -- enforced by Phase 4)

| Column | AdverseEvent | PK | DrugInteraction | Efficacy | Dosing | BMD | TissueDistribution |
|--------|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| ParameterName | R: MedDRA PT | R: PK param | R: PK param | R: Endpoint | R: Dose descriptor | R: Anatomical site | R: Tissue/fluid |
| ParameterCategory | E: Canonical SOC | N | N | N | N | N | N |
| ParameterSubtype | O: Severity | O: PK qualifier | R: **Co-admin drug** | O: Analysis pop | O: Adjustment ctx | N | N |
| TreatmentArm | R: Drug/Placebo | O | E: Index drug | R: Drug vs comparator | O | R: Drug/Placebo | O |
| ArmN | E | O | O | E | N | E | O |
| StudyContext | O | O | O | O | N | O | N |
| DoseRegimen | O | E | E | O | E | O | E |
| Population | O | O | O | O | E | O | O |
| Timepoint | N | O | N | O | O | E | E |
| PrimaryValueType | R: Percentage/Count | R: AM/GM/Median | R: GMR | R: HR/OR/RR/Percentage | O: Numeric | R: PercentChange/AM | R: AM/GM |
| Unit | E: % | R: conc/time/vol | O: ratio | O: % | O: mg/kg | E: %/g/cm2 | R: conc |
| Default BoundType | 95CI | 90CI | 90CI | 95CI | -- | 95CI | -- |

**Cross-table comparison keys** (columns that must match for meaningful comparison):

| Category | Comparison Key |
|----------|----------------|
| AdverseEvent | ParameterName + TreatmentArm + DoseRegimen |
| PK | ParameterName + DoseRegimen + Population + Timepoint + PrimaryValueType + Unit |
| DrugInteraction | ParameterName + ParameterSubtype + TreatmentArm |
| Efficacy | ParameterName + TreatmentArm + PrimaryValueType |
| Dosing | ParameterName + Population + DoseRegimen |
| BMD | ParameterName + TreatmentArm + Timepoint |
| TissueDistribution | ParameterName + DoseRegimen + Timepoint + Unit |

### Output Schema

`tmp_FlattenedStandardizedTable` -- 38 columns organized into 5 groups:

- **Provenance (8)**: DocumentGUID, LabelerName, ProductTitle, VersionNumber, TextTableID, Caption, SourceRowSeq, SourceCellSeq
- **Classification (4)**: TableCategory, ParentSectionCode, ParentSectionTitle, SectionTitle
- **Observation Context (11)**: ParameterName, ParameterCategory, ParameterSubtype, TreatmentArm, ArmN, StudyContext, DoseRegimen, Population, Timepoint, Time, TimeUnit
- **Decomposed Values (10)**: RawValue, PrimaryValue, PrimaryValueType, SecondaryValue, SecondaryValueType, LowerBound, UpperBound, BoundType, PValue, Unit
- **Validation (5)**: ParseConfidence, ParseRule, FootnoteMarkers, FootnoteText, ValidationFlags

### Enum Definitions

#### PrimaryValueType (canonical, tightened)

```
ArithmeticMean - GeometricMean - GeometricMeanRatio - Median -
Percentage - Count - PercentChange - HazardRatio - OddsRatio -
RelativeRisk - RiskDifference - LSMean - Numeric - Text - PValue
```

#### SecondaryValueType

```
SD - CV - Count
```

#### BoundType

```
90CI - 95CI - 99CI - CI - Range - SD - IQR
```

#### ParseRule

```
empty_or_na - pvalue - frac_pct - n_pct - caption_mean_sd -
value_cv - value_plusminus - value_plusminus_sample - value_ci - rr_ci - diff_ci -
range_to - percentage - plain_number - text_descriptive -
plain_number+caption - value_ci+caption
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
| `COL_STD:BOUND_TYPE_INFERRED` | BoundType was inferred from the TableCategory default because LowerBound/UpperBound were populated but BoundType was null (90CI for PK/DDI, 95CI for Efficacy/BMD). |

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
| `CategoryProfileRegistry` | static class | Per-`TableCategory` profile bundling column contract (R/E/O/N), row-required fields, completeness fields, allowed `PrimaryValueType` set, default `BoundType`, and arm/time validation switches. Consumed by `RowValidationService` (and prepared for `TableValidationService` / `ColumnStandardizationService` migration) |
| `ColumnContractRegistry` | `IColumnContractRegistry` | Per-`TableCategory` Required / Expected / Optional / NullExpected column sets transcribed from `TableStandards/column-contracts.md`. Consumed by `ParseQualityService` and (via `CategoryProfile`) by `RowValidationService` |
| `CategoryNameNormalizer` | static class | Resolves `ADVERSE_EVENT` <-> `AdverseEvent` and equivalent forms across the 8 known categories |
| `PkParameterDictionary` | static class | ~35 canonical PK parameter names + aliases (Cmax, AUC, t½, Tmax, Cl, Vd) with Unicode folding |
| `UnitDictionary` | static class | ~80 known PK unit strings + ~16 variant-spelling normalizations (`mcg h/mL` -> `mcg*h/mL`) |
| `PdMarkerDictionary` | static class | 9 pharmacodynamic markers (IPA, VASP-PRI, Platelet Aggregation, etc.) for parser routing |
| `AeParameterCategoryDictionaryService` | scoped service | 1,189 unambiguous AE `ParameterName` -> canonical SOC mappings derived from production data, with name-variant collapsing |
| `ParsedObservationFieldAccess` | static class | Reflection-free `Get` / `GetAsString` / `Set` / `IsPopulated` for the 15 observation-context columns (replaces three near-duplicate switch helpers) |
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
