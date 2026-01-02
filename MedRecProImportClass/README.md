# MedRecProImportClass

A standalone .NET 8 class library extracted from the MedRecPro web application to support SPL (Structured Product Labeling) import operations. This library is designed to be used by console applications and other non-web clients that need to import FDA SPL XML data.

## Purpose

This library was created to enable single-file publishing for the `MedRecProConsole` application. The main MedRecPro project is a Web SDK project which conflicts with self-contained deployment requirements. By extracting the import-related functionality into a dedicated class library, console applications can reference only the components they need without pulling in web-specific dependencies.

## Features

- **SPL XML Parsing**: Complete parsing infrastructure for FDA SPL documents
- **Entity Framework Core Integration**: Database context and repository pattern for data persistence
- **39 Specialized Parsers**: Covers all SPL document sections including:
  - Document structure and sections
  - Products, ingredients, and packaging
  - Organizations and licensing
  - Marketing status and regulatory information
  - REMS, warning letters, and compliance actions
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
├── Context/                # Entity Framework DbContext
├── DataAccess/             # Repository pattern implementation
├── Helpers/                # Utility classes
│   ├── EncryptionHelper.cs # AES-256 encryption (StringCipher)
│   ├── TextUtil.cs         # Text processing utilities
│   ├── XmlHelpers.cs       # XML parsing utilities
│   └── ...
├── Models/                 # Entity classes and DTOs
│   ├── Labels.cs           # Main Label container with 50+ nested classes
│   ├── Import.cs           # Import result types
│   ├── ImportData.cs       # SplData entity
│   └── ...
└── Service/                # Business logic services
    ├── SplImportService.cs       # Main import orchestration
    ├── SplDataService.cs         # SPL data storage/retrieval
    ├── SplParsingService.cs      # XML parsing orchestration
    ├── ZipImportWorkerService.cs # Background ZIP processing
    ├── ParsingServices/          # 39 specialized parser files
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

## Relationship to MedRecPro

This library contains **copies** of files from the main MedRecPro project. The source MedRecPro project remains unchanged and can continue to function independently. Key differences:

| Aspect | MedRecPro | MedRecProImportClass |
|--------|-----------|---------------------|
| SDK | Web SDK | Class Library SDK |
| Namespace | `MedRecPro.*` | `MedRecProImportClass.*` |
| Web Dependencies | Full ASP.NET Core | Minimal (IFormFile only) |
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
