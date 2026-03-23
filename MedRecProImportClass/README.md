# MedRecProImportClass

A standalone .NET 8 class library extracted from the MedRecPro web application to support SPL (Structured Product Labeling) and FDA Orange Book import operations. This library is designed to be used by console applications and other non-web clients that need to import FDA drug data.

## Purpose

This library was created to enable single-file publishing for the `MedRecProConsole` application. The main MedRecPro project is a Web SDK project which conflicts with self-contained deployment requirements. By extracting the import-related functionality into a dedicated class library, console applications can reference only the components they need without pulling in web-specific dependencies.

## Features

- **SPL XML Parsing**: Complete parsing infrastructure for FDA SPL documents
- **FDA Orange Book Import**: Parses `products.txt`, `patent.txt`, and `exclusivity.txt` (tilde-delimited) from Orange Book ZIP files with idempotent upserts and multi-tier entity matching to existing SPL data, plus embedded patent use code definitions
- **Entity Framework Core Integration**: Database context and repository pattern for data persistence
- **SPL Table Normalization**: 4-stage pipeline transforms heterogeneous FDA drug label tables into a uniform 36-column analytical schema (`tmp_FlattenedStandardizedTable`) for cross-product meta-analysis with classical ML — includes table reconstruction, 8 section-aware parsers, and automated validation
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
├── Abstractions/           # Interfaces for dependency injection
│   ├── IFileSource.cs      # File input abstraction (replaces IFormFile)
│   ├── LocalFileSource.cs  # Local file system implementation
│   └── ServiceInterfaces.cs # IEncryptionService, IDictionaryUtilityService
├── Attributes/             # Validation attributes for SPL entities
├── Context/                # Entity Framework DbContext (auto-registers OrangeBook entities via reflection)
├── DataAccess/             # Repository pattern implementation
├── Helpers/                # Utility classes
│   ├── EncryptionHelper.cs # AES-256 encryption (StringCipher)
│   ├── TextUtil.cs         # Text processing utilities
│   ├── XmlHelpers.cs       # XML parsing utilities
│   └── ...
├── Models/                 # Entity classes and DTOs
│   ├── Labels.cs           # Main Label container with 50+ nested classes
│   ├── OrangeBook.cs       # Orange Book entity classes (Applicant, Product, Patent, etc.)
│   ├── Import.cs           # Import result types
│   ├── ImportData.cs       # SplData entity
│   └── ...
├── Resources/              # Embedded assembly resources
│   └── OrangeBookPatentUseCodes.json  # Patent use code definitions (4,409 entries)
└── Service/                # Business logic services
    ├── SplImportService.cs       # Main SPL import orchestration
    ├── SplDataService.cs         # SPL data storage/retrieval
    ├── SplParsingService.cs      # XML parsing orchestration
    ├── ZipImportWorkerService.cs # Background ZIP processing
    ├── ParsingServices/          # 39+ specialized parser files
    │   ├── OrangeBookProductParsingService.cs     # Phase A: products.txt parser
    │   ├── OrangeBookPatentParsingService.cs      # Phase B: patent.txt parser
    │   ├── OrangeBookExclusivityParsingService.cs # Phase C: exclusivity.txt parser
    │   ├── OrangeBookPatentUseCodeParsingService.cs # Phase D: patent use code upsert
    │   └── ...                   # SPL section parsers
    ├── ParsingValidators/        # Validation services
    └── TransformationServices/   # SPL Table Standardization pipeline
        ├── TableCellContextService.cs          # Stage 1: source view assembly
        ├── TableReconstructionService.cs       # Stage 2: table reconstruction
        ├── ValueParser.cs                      # Stage 3: regex-based value decomposition
        ├── PopulationDetector.cs               # Stage 3: population auto-detection
        ├── BaseTableParser.cs                  # Stage 3: shared parser helpers
        ├── PkTableParser.cs                    # Stage 3: pharmacokinetic tables
        ├── SimpleArmTableParser.cs             # Stage 3: single-header AE/efficacy
        ├── MultilevelAeTableParser.cs          # Stage 3: two-row header AE tables
        ├── AeWithSocTableParser.cs             # Stage 3: AE with SOC dividers
        ├── EfficacyMultilevelTableParser.cs    # Stage 3: two-row header efficacy
        ├── BmdTableParser.cs                   # Stage 3: bone mineral density
        ├── TissueRatioTableParser.cs           # Stage 3: tissue-to-plasma ratio
        ├── DosingTableParser.cs                # Stage 3: dosing parameter grids
        ├── TableParserRouter.cs                # Stage 3: section code → parser routing
        ├── TableParsingOrchestrator.cs         # Stage 3: batch loop + DB writes
        ├── ClaudeApiCorrectionService.cs      # Stage 3.5: AI-powered post-parse correction
        ├── RowValidationService.cs             # Stage 4: per-observation checks
        ├── TableValidationService.cs           # Stage 4: cross-row checks
        └── BatchValidationService.cs           # Stage 4: aggregate reporting
```

## Dependencies

| Package | Version | Purpose |
|---------|---------|---------|
| Microsoft.EntityFrameworkCore.SqlServer | 8.0.15 | Database access |
| Microsoft.AspNetCore.Identity.EntityFrameworkCore | 8.0.15 | User entity support |
| Microsoft.Extensions.Hosting | 9.0.4 | Background service support |
| Microsoft.Extensions.Logging | 9.0.4 | Logging infrastructure |
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
| `PatentUseCodeDefinition` | `OrangeBookPatentUseCode` | Patent use code lookup (code → definition) |
| `ApplicantOrganization` | `OrangeBookApplicantOrganization` | Junction linking applicants to SPL organizations |
| `ProductIngredientSubstance` | `OrangeBookProductIngredientSubstance` | Junction linking products to SPL ingredients |
| `ProductMarketingCategory` | `OrangeBookProductMarketingCategory` | Junction linking products to SPL marketing categories |

### Entity Matching Strategy

The import links Orange Book data to existing SPL entities using multi-tier matching:

- **Applicant → Organization**: Normalized exact match, then token-based similarity (Jaccard/containment) with a two-pass strategy — first pass uses full tokens for higher discrimination, fallback uses noise-stripped tokens. Corporate suffixes and pharma noise words are stripped before comparison.
- **Product → IngredientSubstance**: Exact name match, then regex-based matching
- **Product → MarketingCategory**: Exact name match, then regex-based matching

### Design

- **Idempotent**: Upsert-based — safe to re-run without duplication
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

3. **Verify the JSON format** matches the expected structure — an array of objects with `Code` and `Definition` properties:
   ```json
   [
     { "Code": "U-1", "Definition": "PREVENTION OF PREGNANCY" },
     { "Code": "U-2", "Definition": "TREATMENT OF ACNE" },
     ...
   ]
   ```

4. **Replace the embedded resource** at `MedRecProImportClass/Resources/OrangeBookPatentUseCodes.json` with the new JSON file. Ensure the file is saved as UTF-8.

5. **Rebuild** the `MedRecProImportClass` project. The updated definitions will be embedded in the assembly and upserted on the next Orange Book import run.

Phase D is idempotent — existing records with changed definitions are updated, new records are inserted, and unchanged records are skipped.

## SPL Table Standardization Pipeline

A 4-stage pipeline that transforms heterogeneous FDA drug label table data into a uniform 36-column analytical schema for cross-product meta-analysis with classical ML. The full corpus is 250K+ labels; the pipeline supports batch processing by TextTableID range.

### Architecture

```
Stage 1: Source View Assembly
  TableCellContextService → 26-column TableCellContext DTO
        │
Stage 2: Table Reconstruction
  TableReconstructionService → ReconstructedTable (classified rows, resolved spans, multi-level headers)
        │
Stage 3: Section-Aware Parsing
  TableParserRouter → ITableParser (8 parsers) → List<ParsedObservation>
        │
Stage 3.5: Claude API Correction (optional)
  ClaudeApiCorrectionService → corrected List<ParsedObservation>
  TableParsingOrchestrator → bulk write to tmp_FlattenedStandardizedTable
        │
Stage 4: Validation
  RowValidationService + TableValidationService + BatchValidationService → BatchValidationReport
```

### Stage 1: Source View Assembly

`TableCellContextService` joins cell-level data (TextTableCell → TextTableRow → TextTable → SectionTextContent) with section context (vw_SectionNavigation) and document context (Document) into a flat 26-column `TableCellContext` DTO using EF Core LINQ. Supports filtering by DocumentGUID, TextTableID, ID range, and MaxRows.

### Stage 2: Table Reconstruction

`TableReconstructionService` takes flat `TableCellContext` rows and reconstructs logical table structures:

- **Cell processing**: Extracts footnote markers from `<sup>` tags, extracts `styleCode` attributes, strips HTML
- **Row classification**: ExplicitHeader, InferredHeader, ContinuationHeader, SocDivider, DataBody, Footer
- **Span resolution**: 2D occupancy grid resolves ColSpan/RowSpan into absolute column positions
- **Header resolution**: Builds multi-level header paths (e.g., "Treatment > Drug A") from classified header rows
- **Footnote extraction**: Parses footer rows into marker → text dictionary

### Stage 3: Section-Aware Parsing

`TableParserRouter` maps `ParentSectionCode` to a `TableCategory` and selects the most specific parser via priority ordering. `TableParsingOrchestrator` runs the batch loop: reconstruct → route → parse → write to `tmp_FlattenedStandardizedTable`.

#### Table Categories

| Category | Description |
|----------|-------------|
| `PK` | Pharmacokinetic parameter tables |
| `ADVERSE_EVENT` | Adverse reaction incidence tables |
| `EFFICACY` | Clinical study efficacy/outcomes tables |
| `DOSING` | Dosage and administration tables |
| `BMD` | Bone mineral density / timepoint tables |
| `TISSUE_DISTRIBUTION` | Tissue-to-plasma ratio tables |
| `DRUG_INTERACTION` | Drug interaction tables (stub) |
| `OTHER` | Unclassified but parseable tables |
| `SKIP` | Tables to exclude (patient info, NDC, formulas) |

#### Parsers

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

#### Value Decomposition

`ValueParser` applies 13 regex patterns in priority order to decompose cell text into structured components: PrimaryValue, SecondaryValue, CI bounds, P-value, unit, and confidence score. Includes PCT_CHECK validation when arm sample size is available.

#### Population Detection

`PopulationDetector` auto-detects study population from Caption/SectionTitle using regex extraction and a keyword dictionary, with Levenshtein-based fuzzy cross-validation between sources.

### Stage 3.5: Claude API Correction

`ClaudeApiCorrectionService` performs AI-powered post-parse correction of `ParsedObservation` objects before database write. After Stage 3 parsers produce observations, the correction service sends table-level batches to Claude Haiku for semantic review and correction of misclassified fields.

**Common corrections:**
- PrimaryValueType misclassification (e.g., "Numeric" → "Percentage" when table caption indicates "n(%)")
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

**Audit trail:** Corrected fields are flagged with `AI_CORRECTED:{FieldName}` in `ValidationFlags`. The service fails gracefully — API errors return original observations unchanged.

### Stage 4: Validation

Three validation services run post-parse:

| Service | Scope | Key Checks |
|---------|-------|------------|
| `RowValidationService` | Per-observation | Required fields by category, value type appropriateness, bound consistency, low confidence |
| `TableValidationService` | Per-table | Duplicate observations, arm coverage gaps, count reasonableness |
| `BatchValidationService` | Cross-table | Aggregate counts, confidence distribution, cross-version concordance (>50% row count divergence) |

Validation results are returned as in-memory DTOs (`BatchValidationReport`) and logged via ILogger. The orchestrator's `ProcessAllWithValidationAsync` integrates validation into the batch pipeline.

### Output Schema

`tmp_FlattenedStandardizedTable` — 36 columns organized into:

- **Provenance (8)**: DocumentGUID, LabelerName, ProductTitle, VersionNumber, TextTableID, Caption, SourceRowSeq, SourceCellSeq
- **Classification (4)**: TableCategory, ParentSectionCode, ParentSectionTitle, SectionTitle
- **Observation Context (9)**: ParameterName, ParameterCategory, ParameterSubtype, TreatmentArm, ArmN, StudyContext, DoseRegimen, Population, Timepoint
- **Decomposed Values (10)**: RawValue, PrimaryValue, PrimaryValueType, SecondaryValue, SecondaryValueType, LowerBound, UpperBound, BoundType, PValue, Unit
- **Validation (5)**: ParseConfidence, ParseRule, FootnoteMarkers, FootnoteText, ValidationFlags

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
