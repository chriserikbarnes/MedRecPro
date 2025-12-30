# MedRecPro Console

A console application for bulk importing SPL (Structured Product Labeling) ZIP files into the MedRecPro database. Designed for high-volume import operations that are not suitable for a web interface.

## Overview

This application walks an entire directory tree, identifies ZIP files containing SPL XML data, and imports them into a specified database using the existing MedRecPro import infrastructure.

## Features

- **Bulk Import**: Process thousands of SPL ZIP files in a single operation
- **Recursive Scanning**: Automatically discovers ZIP files in all subdirectories
- **Progress Tracking**: Real-time progress bars for overall and per-file processing
- **Error Handling**: Failed imports are logged and skipped; processing continues
- **Timeout Support**: Optional maximum runtime limit to control long-running imports
- **Styled Console UI**: Uses Spectre.Console for rich, interactive terminal output

## Requirements

- .NET 8.0 SDK
- SQL Server (LocalDB or full instance)
- MedRecPro project reference

## Project Structure

```
MedRecProConsole/
├── Program.cs                      # Application entry point
├── MedRecProConsole.csproj         # Project file
├── README.md                       # This file
├── Models/
│   ├── ImportParameters.cs         # User input parameters
│   └── ImportResults.cs            # Import statistics and results
├── Services/
│   └── ImportService.cs            # Import orchestration
└── Helpers/
    ├── ConsoleHelper.cs            # Console UI operations
    └── ConfigurationHelper.cs      # Configuration management
```

## Usage

### Running the Application

```bash
cd MedRecProConsole
dotnet run
```

### User Prompts

1. **Database Selection**: Choose between Local Database Dev or enter a custom connection string
2. **Folder Path**: Enter the root folder containing ZIP files to import
3. **Max Runtime** (Optional): Set a timeout limit in minutes (1-1440)
4. **Confirmation**: Review settings and confirm before import begins

### Example Session

```
  __  __          _ ____           ____
 |  \/  | ___  __| |  _ \ ___  ___|  _ \ _ __ ___
 | |\/| |/ _ \/ _` | |_) / _ \/ __| |_) | '__/ _ \
 | |  | |  __/ (_| |  _ <  __/ (__|  __/| | | (_) |
 |_|  |_|\___|\__,_|_| \_\___|\___|_|   |_|  \___/

─────────────────── SPL Bulk Import Console ───────────────────

Select database connection:
> Local Database Dev (MedRecLocal)
  Custom Connection String

Enter folder path to import (contains ZIP files): C:\SPL_Data\monthly

Set a maximum runtime limit? [y/n] (n): n

Found 150 ZIP file(s)

┌──────────────────┬─────────────────────────────────────────┐
│ Setting          │ Value                                   │
├──────────────────┼─────────────────────────────────────────┤
│ Database         │ Data Source=(localdb)\MSSQLLocalDB;...  │
│ Import Folder    │ C:\SPL_Data\monthly                     │
│ ZIP Files Found  │ 150                                     │
│ Max Runtime      │ No limit                                │
└──────────────────┴─────────────────────────────────────────┘

Proceed with import? [Y/n]: y
```

## Configuration

### Default Database Connection

The application defaults to the Local Database Dev connection:

```
Data Source=(localdb)\MSSQLLocalDB;Database=MedRecLocal;
Min Pool Size=5;Max Pool Size=100;Pooling=true;
Integrated Security=True;Connect Timeout=30;
Encrypt=False;Trust Server Certificate=False;
Application Intent=ReadWrite;Multi Subnet Failover=False
```

### Feature Flags

The application uses the following default feature flags for optimal import performance:

| Flag | Default | Description |
|------|---------|-------------|
| `UseBulkOperations` | false | Bulk database operations |
| `UseBulkStagingOperations` | true | Staged bulk operations for sections |
| `UseBatchSaving` | true | Deferred save operations |
| `UseEnhancedDebugging` | false | Verbose debug output |

### Custom Configuration

Use `ConfigurationHelper.BuildConfiguration()` overloads to customize:

```csharp
// Custom encryption key
var config = ConfigurationHelper.BuildConfiguration("YourCustomKey");

// Full control over feature flags
var config = ConfigurationHelper.BuildConfiguration(
    encryptionKey: "YourKey",
    useBulkOperations: true,
    useBulkStagingOperations: true,
    useBatchSaving: true,
    useEnhancedDebugging: false);
```

## Import Behavior

### Success Handling

- Each ZIP file is processed independently
- XML files within ZIPs are parsed and imported
- Duplicate content is detected via SHA256 hash and skipped
- Statistics are aggregated across all files

### Error Handling

- Failed ZIP imports are logged with error details
- Processing continues with remaining files
- Final report shows success/failure counts
- Error details are displayed (up to 50 errors shown)

### Timeout Behavior

When a maximum runtime is set:
- Import stops gracefully when timeout is reached
- Current ZIP file completes before stopping
- Partial results are displayed

## Output

### Progress Display

During import, the application shows:
- Overall progress bar with percentage
- Per-file progress with status indicators
- Elapsed and remaining time estimates

### Final Report

After import completes:

```
┌─────────────────┬──────────┐
│ Metric          │ Value    │
├─────────────────┼──────────┤
│ Total ZIP Files │ 150      │
│ Successful      │ 148      │
│ Failed          │ 2        │
│ Elapsed Time    │ 01:23:45 │
└─────────────────┴──────────┘

         Entities Created
┌─────────────────┬──────────┐
│ Entity Type     │ Count    │
├─────────────────┼──────────┤
│ Documents       │ 1,250    │
│ Organizations   │ 342      │
│ Products        │ 2,891    │
│ Sections        │ 45,678   │
│ Ingredients     │ 12,456   │
└─────────────────┴──────────┘
```

## Dependencies

### NuGet Packages

- `Spectre.Console` (0.54.0) - Console UI styling and prompts
- `Microsoft.Extensions.Hosting` (8.0.0) - Dependency injection
- `Microsoft.Extensions.Configuration.Json` (8.0.0) - Configuration

### Project References

- `MedRecPro` - Core application with import services, models, and data access

## Building

```bash
# Build the project
dotnet build

# Build in Release mode
dotnet build -c Release

# Publish as self-contained
dotnet publish -c Release -r win-x64 --self-contained
```

## Exit Codes

| Code | Description |
|------|-------------|
| 0 | Import completed successfully (all ZIPs succeeded) |
| 1 | Import completed with failures or was cancelled |

## Troubleshooting

### Common Issues

**No ZIP files found**
- Verify the folder path exists and contains .zip files
- Check for read permissions on the directory

**Database connection failed**
- Ensure SQL Server/LocalDB is running
- Verify connection string is correct
- Check network connectivity for remote databases

**Import timeout**
- Increase the max runtime limit
- Process smaller batches of files
- Check database performance

### Logging

The application uses Microsoft.Extensions.Logging with console output at Warning level. For more verbose output, modify `LogLevel` in `ImportService.buildServiceProvider()`.

## Related Documentation

- MedRecPro main application
- SPL (Structured Product Labeling) FDA specification
- Spectre.Console documentation: https://spectreconsole.net/
