# MedRecProImportClass

A standalone .NET 8 class library extracted from the MedRecPro web application to support SPL (Structured Product Labeling) and FDA Orange Book import operations. This library is designed to be used by console applications and other non-web clients that need to import FDA drug data.

## Purpose

This library was created to enable single-file publishing for the `MedRecProConsole` application. The main MedRecPro project is a Web SDK project which conflicts with self-contained deployment requirements. By extracting the import-related functionality into a dedicated class library, console applications can reference only the components they need without pulling in web-specific dependencies.

## Features

- **SPL XML Parsing**: Complete parsing infrastructure for FDA SPL documents
- **FDA Orange Book Import**: Parses `products.txt`, `patent.txt`, and `exclusivity.txt` (tilde-delimited) from Orange Book ZIP files with idempotent upserts and multi-tier entity matching to existing SPL data, plus embedded patent use code definitions
- **Entity Framework Core Integration**: Database context and repository pattern for data persistence
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
    └── ParsingValidators/        # Validation services
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
