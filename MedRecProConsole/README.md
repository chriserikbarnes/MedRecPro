# MedRecPro Console

A console application for bulk importing SPL (Structured Product Labeling) ZIP files and FDA Orange Book data into the MedRecPro database. Designed for high-volume import operations that are not suitable for a web interface.

## Overview

This application supports two import modes:

- **SPL Import**: Walks an entire directory tree, identifies ZIP files containing SPL XML data, and imports them into a specified database using the existing MedRecPro import infrastructure.
- **Orange Book Import**: Imports FDA Orange Book `products.txt` data from a ZIP file, upserting applicants, products, and junction matches to existing SPL entities (organizations, ingredients, marketing categories).

## Features

- **SPL Bulk Import**: Process thousands of SPL ZIP files in a single operation
- **Orange Book Import**: Import FDA Orange Book `products.txt` from ZIP with entity matching to existing SPL data
- **Interactive Menu**: Command-driven menu (`import`, `orange-book`, `database`, `help`, `quit`) for selecting operations and switching databases at runtime
- **Recursive Scanning**: Automatically discovers ZIP files in all subdirectories
- **Progress Tracking**: Real-time progress bars for overall and per-file processing, with concurrent task display for multi-phase Orange Book imports
- **Crash Recovery**: Queue-based progress tracking persists SPL import state to disk, enabling resume capability across application restarts, crashes, and timer expirations
- **Multi-Session Import**: Resume SPL imports from where you left off when restarting on the same folder
- **Error Handling**: Failed imports are logged and skipped; processing continues
- **Timeout Support**: Optional maximum runtime limit to control long-running SPL imports
- **Unattended Operation**: Full command-line support for Windows Task Scheduler automation (SPL and Orange Book modes)
- **Styled Console UI**: Uses Spectre.Console for rich, interactive terminal output

## Requirements

- .NET 8.0 SDK
- SQL Server (LocalDB or full instance)
- MedRecPro project reference

## Project Structure

```
MedRecProConsole/
├── Program.cs                      # Application entry point (SPL, Orange Book, and interactive modes)
├── MedRecProConsole.csproj         # Project file
├── README.md                       # This file
├── appsettings.json                # Application configuration
├── automation-example.json         # Example automation config for Task Scheduler
├── Models/
│   ├── AppSettings.cs              # Configuration model classes
│   ├── CommandLineArgs.cs          # Command-line argument parsing (--folder, --orange-book, --nuke)
│   ├── ImportParameters.cs         # User input parameters
│   ├── ImportResults.cs            # Import statistics and results
│   ├── ImportQueueItem.cs          # Individual file tracking model
│   └── ImportProgressFile.cs       # Queue file root model
├── Services/
│   ├── ImportService.cs            # SPL import orchestration
│   ├── ImportProgressTracker.cs    # Async queue management service
│   └── OrangeBookImportService.cs  # Orange Book import orchestration
└── Helpers/
    ├── ConsoleHelper.cs            # Console UI, interactive menu, and Orange Book prompts
    ├── ConfigurationHelper.cs      # Configuration management
    └── HelpDocumentation.cs        # Help and version display
```

## Usage

### Running the Application

```bash
cd MedRecProConsole
dotnet run
```

### Interactive Menu

After startup, the application presents a command-driven menu:

```
Enter command (import, orange-book, database, help, quit):
```

| Command | Alias | Description |
|---------|-------|-------------|
| `import` | | Start an SPL bulk import (prompts for folder, runtime limit) |
| `orange-book` | `ob` | Start an Orange Book import (prompts for ZIP path, truncation) |
| `database` | `db` | Switch the active database connection |
| `help` | `h`, `?` | Display available commands |
| `quit` | `q` | Exit the application |

### SPL Import Prompts

1. **Database Selection**: Choose between preconfigured databases or enter a custom connection string
2. **Folder Path**: Enter the root folder containing ZIP files to import
3. **Max Runtime** (Optional): Set a timeout limit in minutes (1-1440)
4. **Confirmation**: Review settings and confirm before import begins

### Orange Book Import Prompts

1. **ZIP File Path**: Enter the path to an Orange Book ZIP file (e.g., `EOBZIP_2026_01.zip`)
2. **Truncation**: Choose whether to truncate all Orange Book tables before import
3. **Confirmation**: Review settings and confirm before import begins

### Example Session

```
  __  __          _ ____           ____
 |  \/  | ___  __| |  _ \ ___  ___|  _ \ _ __ ___
 | |\/| |/ _ \/ _` | |_) / _ \/ __| |_) | '__/ _ \
 | |  | |  __/ (_| |  _ <  __/ (__|  __/| | | (_) |
 |_|  |_|\___|\__,_|_| \_\___|\___|_|   |_|  \___/

─────────────────── Bulk Import Console ───────────────────

Enter command (import, orange-book, database, help, quit): database

Select database connection:
> Local Database Dev (MedRecLocal)
  Local Database Test
  Custom Connection String

Enter command (import, orange-book, database, help, quit): ob

Enter path to Orange Book ZIP file (or empty to cancel): C:\OrangeBook\EOBZIP_2026_01.zip
Truncate all existing Orange Book data before import? [y/n] (n): y

┌──────────────────┬──────────────────────────────────────────────┐
│ Setting          │ Value                                        │
├──────────────────┼──────────────────────────────────────────────┤
│ ZIP File         │ EOBZIP_2026_01.zip                           │
│ Full Path        │ C:\OrangeBook\EOBZIP_2026_01.zip             │
│ Truncate First   │ Yes - ALL Orange Book data will be deleted   │
└──────────────────┴──────────────────────────────────────────────┘

Proceed with Orange Book import? [Y/n]: y
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

## Unattended Operation (Task Scheduler)

The application supports fully automated operation for Windows Task Scheduler or other automation scenarios. When the `--folder` argument is specified, the application runs in unattended mode without user interaction.

### Command-Line Arguments

| Argument | Description |
|----------|-------------|
| `--help, -h` | Display help information |
| `--version, -v` | Display application version |
| `--verbose, -V` | Enable verbose output (debug messages) |
| `--quiet, -q` | Minimal output mode - suppress non-essential messages |
| `--folder <path>` | SPL import folder path - enables unattended SPL mode |
| `--orange-book <path>` | Orange Book ZIP file path - enables Orange Book mode |
| `--nuke` | Truncate all Orange Book tables before import (requires `--orange-book`) |
| `--connection <name>` | Database connection name from appsettings.json |
| `--time <minutes>` | Maximum runtime in minutes (1-1440, SPL mode only) |
| `--auto-quit` | Exit immediately after import completes |
| `--config <path>` | Use alternate configuration file |

**Note:** `--folder` and `--orange-book` are mutually exclusive. Each selects a different import mode.

### Example CLI Usage

```bash
# SPL: Simple unattended import with default database
MedRecProConsole.exe --folder "C:\SPL\Imports" --auto-quit

# SPL: Specify database connection and time limit
MedRecProConsole.exe --folder "C:\SPL\Imports" --connection "Local Database Dev (MedRecLocal)" --time 120 --auto-quit

# SPL: Use a custom automation config file
MedRecProConsole.exe --config "C:\Jobs\nightly-import.json" --folder "C:\SPL\NightlyImports" --auto-quit

# Orange Book: Import with default database
MedRecProConsole.exe --orange-book "C:\OrangeBook\EOBZIP_2026_01.zip" --auto-quit

# Orange Book: Truncate tables first, specify database
MedRecProConsole.exe --orange-book "C:\OrangeBook\EOBZIP_2026_01.zip" --nuke --connection "Local Database Dev" --auto-quit

# Quiet mode for cleaner log output
MedRecProConsole.exe --folder "C:\SPL\Imports" --auto-quit --quiet

# Verbose mode for debugging
MedRecProConsole.exe --folder "C:\SPL\Imports" --auto-quit --verbose
```

### Automation Configuration

The `Automation` section in `appsettings.json` controls default behavior for unattended operation:

```json
"Automation": {
  "AutoQuitOnCompletion": true,
  "DefaultImportFolder": null,
  "DefaultConnectionName": null,
  "DefaultMaxRuntimeMinutes": null,
  "SuppressConfirmations": true,
  "EnableUnattendedLogging": true,
  "UnattendedLogPath": "logs/unattended-{date}.log"
}
```

| Setting | Description |
|---------|-------------|
| `AutoQuitOnCompletion` | Exit after processing completes (required for Task Scheduler) |
| `DefaultImportFolder` | Default folder when `--folder` is used without a path |
| `DefaultConnectionName` | Default database when `--connection` is not specified |
| `DefaultMaxRuntimeMinutes` | Default time limit for unattended imports |
| `SuppressConfirmations` | Skip confirmation prompts in unattended mode |
| `EnableUnattendedLogging` | Log output to file during unattended operation |
| `UnattendedLogPath` | Log file path (supports `{date}`, `{datetime}` placeholders) |

### Custom Automation Config Files

Create a separate JSON config file for specific automation jobs. Settings layer on top of `appsettings.json`, so you only need to specify overrides:

```json
{
  "Automation": {
    "AutoQuitOnCompletion": true,
    "DefaultConnectionName": "Local Database Dev (MedRecLocal)",
    "DefaultMaxRuntimeMinutes": 120
  },
  "Display": {
    "ShowBanner": false,
    "ShowSpinners": false
  }
}
```

Use with: `MedRecProConsole.exe --config "C:\Jobs\my-job.json" --folder "C:\SPL\Imports"`

See `automation-example.json` in the project root for a complete example.

### Setting Up Windows Task Scheduler

1. Open Task Scheduler and create a new task
2. Set the trigger (e.g., daily at 2:00 AM)
3. Set the action:
   - **Program**: `C:\Path\To\MedRecProConsole.exe`
   - **Arguments**: `--folder "C:\SPL\DailyImports" --time 120 --auto-quit`
   - **Start in**: `C:\Path\To\` (application directory)
4. Configure settings:
   - Enable "Run whether user is logged on or not"
   - Configure credentials with database access
5. Test the task with "Run" to verify configuration

### Unattended Mode Behavior

- **Resume Support**: Automatically resumes from existing queue files
- **New File Detection**: Detects files added since last run
- **Connection Validation**: Starts fresh if database connection changed
- **Graceful Timeout**: Completes current file before stopping on time limit
- **Exit Codes**: Returns 0 for success, 1 for any failures (for Task Scheduler monitoring)

## SPL Import Behavior

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
- Progress is saved to the queue file for later resumption

### Crash Recovery and Resume

The application creates a `.medrecpro-import-queue.json` file at the import folder root to track progress:

**Queue File Contents:**
- File statuses: `queued`, `in-progress`, `completed`, `failed`, `skipped`
- Per-file statistics (documents, products, sections, etc.)
- Atomic file writes prevent corruption on crash

**Resume Behavior:**
- When restarting an import on a folder with an existing queue file, you're prompted to resume or start fresh
- Resuming continues from where the previous session left off
- If a completed queue exists and you decline to delete it, the application proceeds to a post-import menu (import, help, quit) instead of exiting

**Nested Folder Support:**
- Queue files in subdirectories are respected when importing from a parent folder
- This allows importing remaining files while honoring completed child folder progress

## Orange Book Import Behavior

The Orange Book import processes the FDA Orange Book `products.txt`, `patent.txt`, and `exclusivity.txt` files (tilde-delimited) from a ZIP archive.

### Import Phases

1. **Extraction**: Extracts `products.txt`, `patent.txt`, and `exclusivity.txt` from the ZIP file
2. **Truncation** (optional): Truncates all Orange Book tables (junctions first, then fact tables) when `--nuke` is used
3. **Applicant Upsert**: Creates or updates applicant records from the Applicant and Applicant_Full_Name columns
4. **Product Upsert**: Creates or updates product records, processed in batches of 5,000
5. **Entity Matching**: Links Orange Book entities to existing SPL data via junction tables:
   - **Applicant → Organization**: Normalized exact match, then token-based similarity (Jaccard/containment) with corporate suffix and pharma noise-word stripping
   - **Product → IngredientSubstance**: Exact name and regex-based matching
   - **Product → MarketingCategory**: Exact name and regex-based matching
6. **Patent Upsert**: Creates or updates patent records from `patent.txt`, processed in batches of 5,000. Each patent is linked to its parent product via the composite natural key (ApplType, ApplNo, ProductNo). Patent fields include expiration date, substance/product flags, use codes, delist flag, and submission date.
7. **Exclusivity Upsert**: Creates or updates exclusivity records from `exclusivity.txt`, processed in batches of 5,000. Each exclusivity record is linked to its parent product via the composite natural key (ApplType, ApplNo, ProductNo). Common exclusivity codes include NCE (New Chemical Entity), ODE (Orphan Drug), RTO (Rare Therapeutic Orphan), and M (Method of Use).

### Idempotent Design

The import is upsert-based, so running it multiple times on the same data is safe. Crash recovery queues are not needed since partial imports can simply be re-run.

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

- `MedRecProImportClass` - Import class library with SPL/Orange Book parsing services, models, and data access

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
- MedRecProImportClass library
- SPL (Structured Product Labeling) FDA specification
- FDA Orange Book: https://www.fda.gov/drugs/drug-approvals-and-databases/approved-drug-products-therapeutic-equivalence-evaluations-orange-book
- Spectre.Console documentation: https://spectreconsole.net/
