# MedRecPro Console

A console application for bulk importing SPL (Structured Product Labeling) ZIP files into the MedRecPro database. Designed for high-volume import operations that are not suitable for a web interface.

## Overview

This application walks an entire directory tree, identifies ZIP files containing SPL XML data, and imports them into a specified database using the existing MedRecPro import infrastructure.

## Features

- **Bulk Import**: Process thousands of SPL ZIP files in a single operation
- **Recursive Scanning**: Automatically discovers ZIP files in all subdirectories
- **Progress Tracking**: Real-time progress bars for overall and per-file processing
- **Crash Recovery**: Queue-based progress tracking persists import state to disk, enabling resume capability across application restarts, crashes, and timer expirations
- **Multi-Session Import**: Resume imports from where you left off when restarting on the same folder
- **Error Handling**: Failed imports are logged and skipped; processing continues
- **Timeout Support**: Optional maximum runtime limit to control long-running imports
- **Unattended Operation**: Full command-line support for Windows Task Scheduler automation
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
├── appsettings.json                # Application configuration
├── automation-example.json         # Example automation config for Task Scheduler
├── Models/
│   ├── AppSettings.cs              # Configuration model classes
│   ├── CommandLineArgs.cs          # Command-line argument parsing
│   ├── ImportParameters.cs         # User input parameters
│   ├── ImportResults.cs            # Import statistics and results
│   ├── ImportQueueItem.cs          # Individual file tracking model
│   └── ImportProgressFile.cs       # Queue file root model
├── Services/
│   ├── ImportService.cs            # Import orchestration
│   └── ImportProgressTracker.cs    # Async queue management service
└── Helpers/
    ├── ConsoleHelper.cs            # Console UI operations
    ├── ConfigurationHelper.cs      # Configuration management
    └── HelpDocumentation.cs        # Help and version display
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

## Unattended Operation (Task Scheduler)

The application supports fully automated operation for Windows Task Scheduler or other automation scenarios. When the `--folder` argument is specified, the application runs in unattended mode without user interaction.

### Command-Line Arguments

| Argument | Description |
|----------|-------------|
| `--help, -h` | Display help information |
| `--version, -v` | Display application version |
| `--verbose, -V` | Enable verbose output (debug messages) |
| `--quiet, -q` | Minimal output mode - suppress non-essential messages |
| `--folder <path>` | Import folder path - enables unattended mode |
| `--connection <name>` | Database connection name from appsettings.json |
| `--time <minutes>` | Maximum runtime in minutes (1-1440) |
| `--auto-quit` | Exit immediately after import completes |
| `--config <path>` | Use alternate configuration file |

### Example Task Scheduler Usage

```bash
# Simple unattended import with default database
MedRecProConsole.exe --folder "C:\SPL\Imports" --auto-quit

# Specify database connection and time limit
MedRecProConsole.exe --folder "C:\SPL\Imports" --connection "Local Database Dev (MedRecLocal)" --time 120 --auto-quit

# Use a custom automation config file
MedRecProConsole.exe --config "C:\Jobs\nightly-import.json" --folder "C:\SPL\NightlyImports" --auto-quit

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
