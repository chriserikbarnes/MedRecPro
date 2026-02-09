<#
.SYNOPSIS
    MedRecPro Temp Table Migration Script using BCP (Bulk Copy Program)

.DESCRIPTION
    This script facilitates bulk data transfer of materialized temp tables from a local SQL Server
    instance to Azure SQL Database using the BCP utility. These temp tables are populated locally
    by usp_RefreshTempTables (MedRecPro_Batch.sql) to avoid expensive vCore consumption on Azure.

    The script operates in two distinct phases:
    1. EXPORT: Extracts temp table data from local SQL Server to native binary files (.dat)
    2. IMPORT: Truncates Azure target tables, then loads data files using parallel operations

    Tables Handled:
    - tmp_SectionContent        (~274K rows) - Materialized from vw_SectionContent CTE logic
    - tmp_LabelSectionMarkdown  (~274K rows) - Materialized from vw_LabelSectionMarkdown CTE logic
    - tmp_InventorySummary      (~50 rows)   - Materialized from vw_InventorySummary

    Key Features:
    - Native binary format (-n) for maximum performance between SQL Server instances
    - Parallel import operations with configurable throttle limit
    - Pre-import TRUNCATE of Azure target tables (full refresh, not append)
    - Detailed inventory reporting after export phase
    - Interactive confirmation before destructive import operations
    - Comprehensive error logging and progress tracking

.PARAMETER Operation
    Specifies which phase to execute: 'Export', 'Import', or 'Both'

.PARAMETER LocalServer
    The local SQL Server instance name (default: localhost)

.PARAMETER LocalDatabase
    The local database name (default: MedRecLocal)

.PARAMETER AzureServer
    The Azure SQL Database server (e.g., yourserver.database.windows.net)

.PARAMETER AzureDatabase
    The Azure SQL Database name

.PARAMETER AzureUser
    The Azure SQL Database username

.PARAMETER AzurePassword
    The Azure SQL Database password (will prompt securely if not provided)

.PARAMETER DataPath
    Directory for storing exported .dat files (default: C:\MedRecPro-Migration\TempTables)

.PARAMETER BatchSize
    Number of rows per transaction batch (default: 50000)

.PARAMETER ParallelThrottle
    Maximum number of concurrent import operations (default: 3)

.EXAMPLE
    # Export only
    .\MedRecPro-TempTable-Export-Import.ps1 -Operation Export

.EXAMPLE
    # Import only (after export completed on another machine)
    .\MedRecPro-TempTable-Export-Import.ps1 -Operation Import -AzureServer "myserver.database.windows.net" -AzureDatabase "MedRecPro" -AzureUser "admin"

.EXAMPLE
    # Full migration
    .\MedRecPro-TempTable-Export-Import.ps1 -Operation Both -AzureServer "myserver.database.windows.net" -AzureDatabase "MedRecPro" -AzureUser "admin"

.NOTES
    Author: MedRecPro Development Team
    Version: 1.0.0
    Requires:
        - SQL Server BCP utility (installed with SQL Server or SSMS)
        - PowerShell 5.1+ (PS7+ enables parallel imports for faster performance)
        - Network access to Azure SQL Database (firewall rules configured)
        - Temp tables must already exist on Azure (run usp_RefreshTempTables once, or create manually)

    Prerequisites:
        - Run usp_RefreshTempTables locally first to populate the temp tables
        - Ensure Azure target tables exist with matching schema

    Performance Recommendations:
        - Use PowerShell 7+ for parallel import capability (winget install Microsoft.PowerShell)

    Security Note:
        - Avoid storing passwords in scripts; use -AzurePassword parameter or secure prompt
        - Consider using Azure AD authentication for production environments
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('Export', 'Import', 'Both')]
    [string]$Operation,

    [Parameter(Mandatory = $false)]
    [string]$LocalServer = "localhost",

    [Parameter(Mandatory = $false)]
    [string]$LocalDatabase = "MedRecLocal",

    [Parameter(Mandatory = $false)]
    [string]$AzureServer,

    [Parameter(Mandatory = $false)]
    [string]$AzureDatabase,

    [Parameter(Mandatory = $false)]
    [string]$AzureUser,

    [Parameter(Mandatory = $false)]
    [string]$AzurePassword,

    [Parameter(Mandatory = $false)]
    [string]$DataPath = "C:\MedRecPro-Migration\TempTables",

    [Parameter(Mandatory = $false)]
    [int]$BatchSize = 50000,

    [Parameter(Mandatory = $false)]
    [int]$ParallelThrottle = 3
)

#region ==================== CONFIGURATION ====================

# Materialized temp tables created by usp_RefreshTempTables
$Tables = @(
    "tmp_SectionContent"
    "tmp_LabelSectionMarkdown"
    "tmp_InventorySummary"
)

# Schema name
$Schema = "dbo"

#endregion

#region ==================== HELPER FUNCTIONS ====================

function Write-LogMessage {
    <#
    .SYNOPSIS
        Writes a timestamped message to console and optionally to a log file
    #>
    param(
        [string]$Message,
        [ValidateSet('Info', 'Success', 'Warning', 'Error')]
        [string]$Level = 'Info'
    )

    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    $colors = @{
        'Info'    = 'Cyan'
        'Success' = 'Green'
        'Warning' = 'Yellow'
        'Error'   = 'Red'
    }

    Write-Host "[$timestamp] " -NoNewline -ForegroundColor Gray
    Write-Host $Message -ForegroundColor $colors[$Level]

    # Append to log file
    $logFile = Join-Path $DataPath "temptable-migration-log.txt"
    "[$timestamp] [$Level] $Message" | Out-File -FilePath $logFile -Append -ErrorAction SilentlyContinue
}

function Get-TableRowCount {
    <#
    .SYNOPSIS
        Returns the row count for a specified table using sqlcmd
    #>
    param(
        [string]$Server,
        [string]$Database,
        [string]$TableName,
        [string]$User,
        [string]$Password
    )

    $query = "SET NOCOUNT ON; SELECT COUNT(*) FROM [$Schema].[$TableName]"

    if ($User -and $Password) {
        # SQL Authentication (Azure)
        $result = sqlcmd -S $Server -d $Database -U $User -P $Password -Q $query -h -1 -W 2>$null
    }
    else {
        # Windows Authentication (Local)
        $result = sqlcmd -S $Server -d $Database -Q $query -h -1 -W -E 2>$null
    }

    if ($result -match '^\d+$') {
        return [int]$result.Trim()
    }
    return -1
}

function Format-FileSize {
    <#
    .SYNOPSIS
        Converts bytes to human-readable format
    #>
    param([long]$Bytes)

    if ($Bytes -ge 1GB) { return "{0:N2} GB" -f ($Bytes / 1GB) }
    if ($Bytes -ge 1MB) { return "{0:N2} MB" -f ($Bytes / 1MB) }
    if ($Bytes -ge 1KB) { return "{0:N2} KB" -f ($Bytes / 1KB) }
    return "$Bytes Bytes"
}

function Test-BcpAvailable {
    <#
    .SYNOPSIS
        Verifies that BCP utility is available in PATH
    #>
    try {
        $null = Get-Command bcp -ErrorAction Stop
        return $true
    }
    catch {
        return $false
    }
}

function Test-SqlCmdAvailable {
    <#
    .SYNOPSIS
        Verifies that SQLCMD utility is available in PATH
    #>
    try {
        $null = Get-Command sqlcmd -ErrorAction Stop
        return $true
    }
    catch {
        return $false
    }
}

function Test-AzureSqlConnection {
    <#
    .SYNOPSIS
        Tests connectivity to Azure SQL Database before import operations
    .DESCRIPTION
        Attempts a simple query against the target Azure SQL Database to verify:
        - Network connectivity (firewall rules allow connection)
        - Authentication credentials are valid
        - Database exists and is accessible
    .RETURNS
        $true if connection successful, $false otherwise
    #>
    param(
        [Parameter(Mandatory = $true)]
        [string]$Server,

        [Parameter(Mandatory = $true)]
        [string]$Database,

        [Parameter(Mandatory = $true)]
        [string]$User,

        [Parameter(Mandatory = $true)]
        [string]$Password
    )

    Write-Host ""
    Write-Host "Testing connection to Azure SQL Database..." -ForegroundColor Cyan
    Write-Host "  Server:   $Server" -ForegroundColor Gray
    Write-Host "  Database: $Database" -ForegroundColor Gray
    Write-Host "  User:     $User" -ForegroundColor Gray
    Write-Host ""

    # Simple query to test connectivity
    $testQuery = "SET NOCOUNT ON; SELECT 1 AS ConnectionTest"

    try {
        $result = sqlcmd -S $Server -d $Database -U $User -P $Password -Q $testQuery -h -1 -W -t 30 2>&1
        $exitCode = $LASTEXITCODE

        if ($exitCode -eq 0 -and $result -match '^\s*1\s*$') {
            Write-Host "  ✓ Connection successful!" -ForegroundColor Green
            Write-Host ""
            return $true
        }
        else {
            Write-Host "  ✗ Connection failed!" -ForegroundColor Red
            Write-Host ""
            Write-Host "  Error details:" -ForegroundColor Yellow

            # Parse common error messages for user-friendly output
            $errorText = $result | Out-String

            if ($errorText -match "Login failed") {
                Write-Host "    - Authentication failed. Check username and password." -ForegroundColor Red
            }
            elseif ($errorText -match "Cannot open server|server was not found") {
                Write-Host "    - Server not reachable. Check server name and network connectivity." -ForegroundColor Red
            }
            elseif ($errorText -match "firewall") {
                Write-Host "    - Firewall blocking connection. Add your IP to Azure SQL firewall rules." -ForegroundColor Red
            }
            elseif ($errorText -match "database .* does not exist|Cannot open database") {
                Write-Host "    - Database not found. Verify database name." -ForegroundColor Red
            }
            else {
                Write-Host "    $errorText" -ForegroundColor Red
            }

            Write-Host ""
            return $false
        }
    }
    catch {
        Write-Host "  ✗ Connection test failed with exception:" -ForegroundColor Red
        Write-Host "    $($_.Exception.Message)" -ForegroundColor Red
        Write-Host ""
        return $false
    }
}

function Invoke-AzureTruncate {
    <#
    .SYNOPSIS
        Truncates target tables on Azure SQL before import (full refresh)
    .DESCRIPTION
        Executes TRUNCATE TABLE for each temp table on the Azure target.
        These tables are full refreshes — old data must be cleared first.
    .RETURNS
        $true if all truncates succeeded, $false otherwise
    #>
    param(
        [Parameter(Mandatory = $true)]
        [string]$Server,

        [Parameter(Mandatory = $true)]
        [string]$Database,

        [Parameter(Mandatory = $true)]
        [string]$User,

        [Parameter(Mandatory = $true)]
        [string]$Password,

        [Parameter(Mandatory = $true)]
        [array]$TablesToTruncate
    )

    Write-Host "Truncating target tables on Azure..." -ForegroundColor Cyan
    $allSuccess = $true

    foreach ($table in $TablesToTruncate) {
        $truncateQuery = "TRUNCATE TABLE [$Schema].[$table]"

        try {
            $result = sqlcmd -S $Server -d $Database -U $User -P $Password -Q $truncateQuery -t 60 2>&1
            $exitCode = $LASTEXITCODE

            if ($exitCode -eq 0) {
                Write-Host "  ✓ Truncated $table" -ForegroundColor Green
            }
            else {
                $errorText = $result | Out-String
                Write-Host "  ✗ Failed to truncate $table" -ForegroundColor Red
                Write-Host "    $errorText" -ForegroundColor Red
                $allSuccess = $false
            }
        }
        catch {
            Write-Host "  ✗ Exception truncating ${table}: $($_.Exception.Message)" -ForegroundColor Red
            $allSuccess = $false
        }
    }

    Write-Host ""
    return $allSuccess
}

#endregion

#region ==================== EXPORT OPERATIONS ====================

function Invoke-ExportPhase {
    <#
    .SYNOPSIS
        Exports temp tables from local SQL Server to native binary files

    .DESCRIPTION
        Iterates through the configured temp tables and exports each to a .dat file
        using BCP in native format (-n). Collects row counts and file sizes
        for inventory reporting.
    #>

    # Check for existing export files
    $existingDatFiles = Get-ChildItem -Path $DataPath -Filter "*.dat" -ErrorAction SilentlyContinue
    if ($existingDatFiles -and $existingDatFiles.Count -gt 0) {
        Write-Host ""
        Write-Host "╔══════════════════════════════════════════════════════════════════╗" -ForegroundColor Yellow
        Write-Host "║                    EXISTING FILES DETECTED                       ║" -ForegroundColor Yellow
        Write-Host "║                                                                  ║" -ForegroundColor Yellow
        Write-Host "║  Found existing .dat files in the export directory.              ║" -ForegroundColor Yellow
        Write-Host "║                                                                  ║" -ForegroundColor Yellow
        Write-Host "║  BCP appends to existing files, which will cause data            ║" -ForegroundColor Yellow
        Write-Host "║  duplication if not removed.                                     ║" -ForegroundColor Yellow
        Write-Host "║                                                                  ║" -ForegroundColor Yellow
        Write-Host "╚══════════════════════════════════════════════════════════════════╝" -ForegroundColor Yellow
        Write-Host ""

        $cleanup = Read-Host "Delete existing .dat files before export? (Y/N)"
        if ($cleanup -eq 'Y' -or $cleanup -eq 'y') {
            $existingDatFiles | Remove-Item -Force
            Get-ChildItem -Path $DataPath -Filter "*.err" -ErrorAction SilentlyContinue | Remove-Item -Force
            Write-LogMessage "Cleaned up existing export files" -Level Success
        }
        else {
            Write-LogMessage "Export cancelled - clean up files manually or confirm to proceed" -Level Warning
            return $null
        }
    }

    Write-Host ""
    Write-Host "╔══════════════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
    Write-Host "║              EXPORT PHASE - LOCAL TEMP TABLES                    ║" -ForegroundColor Cyan
    Write-Host "╚══════════════════════════════════════════════════════════════════╝" -ForegroundColor Cyan
    Write-Host ""

    Write-LogMessage "Starting export from $LocalServer.$LocalDatabase" -Level Info
    Write-LogMessage "Data directory: $DataPath" -Level Info
    Write-LogMessage "Total tables to export: $($Tables.Count)" -Level Info
    Write-Host ""

    # Initialize inventory collection
    $inventory = @()
    $successCount = 0
    $failCount = 0
    $totalRows = 0
    $totalSize = 0

    # Progress tracking
    $tableIndex = 0

    foreach ($table in $Tables) {
        $tableIndex++
        $percentComplete = [math]::Round(($tableIndex / $Tables.Count) * 100)

        Write-Progress -Activity "Exporting Temp Tables" `
                       -Status "Processing $table ($tableIndex of $($Tables.Count))" `
                       -PercentComplete $percentComplete

        $datFile = Join-Path $DataPath "$table.dat"
        $errFile = Join-Path $DataPath "$table.err"

        # Build BCP export command
        # -n: Native format (binary, fastest for SQL Server to SQL Server)
        # -T: Trusted connection (Windows Authentication)
        # -e: Error file for failed rows
        $bcpArgs = @(
            "$LocalDatabase.$Schema.$table"
            "out"
            "`"$datFile`""
            "-S", $LocalServer
            "-T"
            "-n"
            "-e", "`"$errFile`""
        )

        Write-LogMessage "Exporting: $table" -Level Info

        try {
            # Execute BCP
            $bcpOutput = & bcp $bcpArgs 2>&1
            $bcpExitCode = $LASTEXITCODE

            if ($bcpExitCode -eq 0 -and (Test-Path $datFile)) {
                # Get row count from local database
                $rowCount = Get-TableRowCount -Server $LocalServer `
                                              -Database $LocalDatabase `
                                              -TableName $table

                # Get file size
                $fileInfo = Get-Item $datFile
                $fileSize = $fileInfo.Length

                # Add to inventory
                $inventory += [PSCustomObject]@{
                    Table    = $table
                    Rows     = $rowCount
                    FileSize = $fileSize
                    FilePath = $datFile
                    Status   = "Success"
                }

                $successCount++
                $totalRows += $rowCount
                $totalSize += $fileSize

                Write-LogMessage "  ✓ $table - $($rowCount.ToString('N0')) rows ($(Format-FileSize $fileSize))" -Level Success

                # Clean up error file if empty
                if ((Test-Path $errFile) -and (Get-Item $errFile).Length -eq 0) {
                    Remove-Item $errFile -Force
                }
            }
            else {
                $inventory += [PSCustomObject]@{
                    Table    = $table
                    Rows     = 0
                    FileSize = 0
                    FilePath = $datFile
                    Status   = "Failed"
                }
                $failCount++
                Write-LogMessage "  ✗ $table - Export failed (Exit code: $bcpExitCode)" -Level Error

                # Log BCP output for debugging
                if ($bcpOutput) {
                    $bcpOutput | Out-File -FilePath (Join-Path $DataPath "$table-error.log") -Force
                }
            }
        }
        catch {
            $inventory += [PSCustomObject]@{
                Table    = $table
                Rows     = 0
                FileSize = 0
                FilePath = $datFile
                Status   = "Error: $($_.Exception.Message)"
            }
            $failCount++
            Write-LogMessage "  ✗ $table - Exception: $($_.Exception.Message)" -Level Error
        }
    }

    Write-Progress -Activity "Exporting Temp Tables" -Completed

    # Display inventory report
    Write-Host ""
    Write-Host "╔══════════════════════════════════════════════════════════════════╗" -ForegroundColor Green
    Write-Host "║                      EXPORT INVENTORY REPORT                     ║" -ForegroundColor Green
    Write-Host "╚══════════════════════════════════════════════════════════════════╝" -ForegroundColor Green
    Write-Host ""

    # Table header
    $headerFormat = "{0,-40} {1,15} {2,15} {3,10}"
    Write-Host ($headerFormat -f "TABLE NAME", "ROW COUNT", "FILE SIZE", "STATUS") -ForegroundColor White
    Write-Host ("-" * 85) -ForegroundColor Gray

    foreach ($item in $inventory | Sort-Object -Property Rows -Descending) {
        $statusColor = if ($item.Status -eq "Success") { "Green" } else { "Red" }
        $rowDisplay = if ($item.Rows -ge 0) { $item.Rows.ToString("N0") } else { "N/A" }
        $sizeDisplay = Format-FileSize $item.FileSize

        Write-Host ($headerFormat -f $item.Table, $rowDisplay, $sizeDisplay, "") -NoNewline
        Write-Host $item.Status -ForegroundColor $statusColor
    }

    Write-Host ("-" * 85) -ForegroundColor Gray
    Write-Host ($headerFormat -f "TOTAL", $totalRows.ToString("N0"), (Format-FileSize $totalSize), "") -ForegroundColor Cyan
    Write-Host ""

    # Summary
    Write-Host "Export Summary:" -ForegroundColor White
    Write-Host "  Successful: $successCount tables" -ForegroundColor Green
    if ($failCount -gt 0) {
        Write-Host "  Failed: $failCount tables" -ForegroundColor Red
    }
    Write-Host "  Total Rows: $($totalRows.ToString('N0'))" -ForegroundColor Cyan
    Write-Host "  Total Size: $(Format-FileSize $totalSize)" -ForegroundColor Cyan
    Write-Host ""

    # Save inventory to CSV for reference
    $inventoryCsv = Join-Path $DataPath "temptable-export-inventory.csv"
    $inventory | Export-Csv -Path $inventoryCsv -NoTypeInformation
    Write-LogMessage "Inventory saved to: $inventoryCsv" -Level Info

    # Return inventory for potential use in import phase
    return $inventory
}

#endregion

#region ==================== IMPORT OPERATIONS ====================

function Invoke-ImportPhase {
    <#
    .SYNOPSIS
        Imports all exported temp table data files to Azure SQL Database in parallel

    .DESCRIPTION
        Truncates target tables on Azure, then loads .dat files using parallel BCP operations.
        Requires user confirmation before proceeding. Uses TABLOCK hint
        and configurable batch sizes for optimal performance.
    #>
    param(
        [array]$Inventory
    )

    Write-Host ""
    Write-Host "╔══════════════════════════════════════════════════════════════════╗" -ForegroundColor Yellow
    Write-Host "║             IMPORT PHASE - AZURE SQL (TEMP TABLES)              ║" -ForegroundColor Yellow
    Write-Host "╚══════════════════════════════════════════════════════════════════╝" -ForegroundColor Yellow
    Write-Host ""

    # Validate Azure parameters
    if (-not $AzureServer) {
        $AzureServer = Read-Host "Enter Azure SQL Server (e.g., server.database.windows.net)"
    }
    if (-not $AzureDatabase) {
        $AzureDatabase = Read-Host "Enter Azure SQL Database name"
    }
    if (-not $AzureUser) {
        $AzureUser = Read-Host "Enter Azure SQL Username"
    }
    if (-not $AzurePassword) {
        $securePassword = Read-Host "Enter Azure SQL Password" -AsSecureString
        $AzurePassword = [Runtime.InteropServices.Marshal]::PtrToStringAuto(
            [Runtime.InteropServices.Marshal]::SecureStringToBSTR($securePassword)
        )
    }

    # Test connection to Azure SQL before proceeding
    $connectionValid = Test-AzureSqlConnection -Server $AzureServer `
                                                -Database $AzureDatabase `
                                                -User $AzureUser `
                                                -Password $AzurePassword

    if (-not $connectionValid) {
        Write-LogMessage "Cannot proceed with import - connection to Azure SQL failed." -Level Error
        Write-Host "Please verify:" -ForegroundColor Yellow
        Write-Host "  1. Azure SQL server name is correct (include .database.windows.net)" -ForegroundColor Gray
        Write-Host "  2. Database name exists on the server" -ForegroundColor Gray
        Write-Host "  3. Username and password are correct" -ForegroundColor Gray
        Write-Host "  4. Your IP address is allowed in Azure SQL firewall rules" -ForegroundColor Gray
        Write-Host ""
        return
    }

    # Discover available .dat files if no inventory provided
    if (-not $Inventory) {
        $datFiles = @(Get-ChildItem -Path $DataPath -Filter "*.dat" -File -ErrorAction SilentlyContinue)

        if ($datFiles.Count -eq 0) {
            Write-LogMessage "No .dat files found in $DataPath" -Level Error
            Write-LogMessage "Run the Export operation first, or verify the DataPath parameter." -Level Info
            return
        }

        $Inventory = @($datFiles | ForEach-Object {
            [PSCustomObject]@{
                Table    = $_.BaseName
                FilePath = $_.FullName
                FileSize = $_.Length
                Rows     = -1  # Unknown when loading from files
                Status   = "Pending"
            }
        })
    }

    # Filter to only successful exports
    $tablesToImport = @($Inventory | Where-Object {
        $_.Status -eq "Success" -or $_.Status -eq "Pending"
    })

    if ($tablesToImport.Count -eq 0) {
        Write-LogMessage "No valid data files found for import!" -Level Error
        return
    }

    # Display import preview
    Write-Host "Import Configuration:" -ForegroundColor White
    Write-Host "  Target Server:    $AzureServer" -ForegroundColor Cyan
    Write-Host "  Target Database:  $AzureDatabase" -ForegroundColor Cyan
    Write-Host "  Tables to Import: $($tablesToImport.Count)" -ForegroundColor Cyan
    Write-Host "  Batch Size:       $($BatchSize.ToString('N0')) rows" -ForegroundColor Cyan
    if ($PSVersionTable.PSVersion.Major -ge 7) {
        Write-Host "  Import Mode:      Parallel ($ParallelThrottle threads)" -ForegroundColor Cyan
    }
    else {
        Write-Host "  Import Mode:      Sequential (PS7+ enables parallel)" -ForegroundColor Cyan
    }
    Write-Host ""

    # Calculate total data size
    $totalImportSize = ($tablesToImport | Measure-Object -Property FileSize -Sum).Sum
    if ($null -eq $totalImportSize) { $totalImportSize = 0 }
    Write-Host "  Total Data Size:  $(Format-FileSize $totalImportSize)" -ForegroundColor Cyan
    Write-Host ""

    # List tables to be imported
    Write-Host "Tables queued for import:" -ForegroundColor White
    Write-Host ("-" * 60) -ForegroundColor Gray

    $tablesToImport | Sort-Object -Property FileSize -Descending | ForEach-Object {
        Write-Host "  $($_.Table) ($(Format-FileSize $_.FileSize))" -ForegroundColor Gray
    }
    Write-Host ""

    # Confirmation prompt
    Write-Host "╔══════════════════════════════════════════════════════════════════╗" -ForegroundColor Red
    Write-Host "║                           WARNING                                ║" -ForegroundColor Red
    Write-Host "║                                                                  ║" -ForegroundColor Red
    Write-Host "║  This operation will TRUNCATE and re-populate the target         ║" -ForegroundColor Red
    Write-Host "║  temp tables on Azure SQL. This is a full refresh.               ║" -ForegroundColor Red
    Write-Host "║                                                                  ║" -ForegroundColor Red
    Write-Host "║  Ensure that:                                                    ║" -ForegroundColor Red
    Write-Host "║  1. Target tables exist with matching schema                     ║" -ForegroundColor Red
    Write-Host "║  2. No active queries are reading from these tables              ║" -ForegroundColor Red
    Write-Host "║  3. Azure SQL firewall allows your current IP address            ║" -ForegroundColor Red
    Write-Host "║                                                                  ║" -ForegroundColor Red
    Write-Host "╚══════════════════════════════════════════════════════════════════╝" -ForegroundColor Red
    Write-Host ""

    $confirmation = Read-Host "Type 'IMPORT' to proceed with truncate + import"

    if ($confirmation -ne 'IMPORT') {
        Write-LogMessage "Import cancelled by user." -Level Warning
        return
    }

    Write-Host ""

    # Truncate target tables on Azure before importing
    $tableNames = @($tablesToImport | ForEach-Object { $_.Table })
    $truncateSuccess = Invoke-AzureTruncate -Server $AzureServer `
                                             -Database $AzureDatabase `
                                             -User $AzureUser `
                                             -Password $AzurePassword `
                                             -TablesToTruncate $tableNames

    if (-not $truncateSuccess) {
        Write-LogMessage "One or more tables failed to truncate. Check that tables exist on Azure." -Level Error
        $proceed = Read-Host "Continue with import anyway? (Y/N)"
        if ($proceed -ne 'Y' -and $proceed -ne 'y') {
            Write-LogMessage "Import cancelled after truncate failure." -Level Warning
            return
        }
    }

    Write-LogMessage "Starting import to Azure SQL..." -Level Info
    Write-Host ""

    # Execute imports (parallel if PS7+, sequential otherwise)
    $startTime = Get-Date
    $importResults = @()

    # Check if we can use parallel processing (PowerShell 7+)
    $useParallel = $PSVersionTable.PSVersion.Major -ge 7

    if ($useParallel) {
        Write-LogMessage "Using parallel import (PowerShell $($PSVersionTable.PSVersion.Major) detected, ThrottleLimit: $ParallelThrottle)" -Level Info
        Write-Host ""

        # Prepare import parameters for parallel execution
        $importParams = @{
            AzureServer   = $AzureServer
            AzureDatabase = $AzureDatabase
            AzureUser     = $AzureUser
            AzurePassword = $AzurePassword
            Schema        = $Schema
            BatchSize     = $BatchSize
            DataPath      = $DataPath
        }

        $importResults = $tablesToImport | ForEach-Object -Parallel {
            $params = $using:importParams
            $table = $_.Table
            $datFile = $_.FilePath

            $errFile = Join-Path $params.DataPath "$table-import.err"

            # Build BCP import command as a flat array
            $bcpArgs = [System.Collections.ArrayList]@()
            [void]$bcpArgs.Add("$($params.AzureDatabase).$($params.Schema).$table")
            [void]$bcpArgs.Add("in")
            [void]$bcpArgs.Add($datFile)
            [void]$bcpArgs.Add("-S")
            [void]$bcpArgs.Add($params.AzureServer)
            [void]$bcpArgs.Add("-U")
            [void]$bcpArgs.Add($params.AzureUser)
            [void]$bcpArgs.Add("-P")
            [void]$bcpArgs.Add($params.AzurePassword)
            [void]$bcpArgs.Add("-n")
            [void]$bcpArgs.Add("-b")
            [void]$bcpArgs.Add($params.BatchSize.ToString())
            [void]$bcpArgs.Add("-h")
            [void]$bcpArgs.Add("TABLOCK")
            [void]$bcpArgs.Add("-e")
            [void]$bcpArgs.Add($errFile)

            try {
                $bcpOutput = & bcp $bcpArgs 2>&1
                $exitCode = $LASTEXITCODE

                # Parse rows copied from BCP output
                $rowsCopied = 0
                $bcpOutputStr = $bcpOutput | Out-String
                if ($bcpOutputStr -match '(\d+) rows copied') {
                    $rowsCopied = [int]$Matches[1]
                }

                [PSCustomObject]@{
                    Table      = $table
                    Success    = ($exitCode -eq 0)
                    RowsCopied = $rowsCopied
                    ExitCode   = $exitCode
                    Output     = $bcpOutputStr
                }
            }
            catch {
                [PSCustomObject]@{
                    Table      = $table
                    Success    = $false
                    RowsCopied = 0
                    ExitCode   = -1
                    Output     = $_.Exception.Message
                }
            }
        } -ThrottleLimit $ParallelThrottle
    }
    else {
        Write-LogMessage "Using sequential import (PowerShell $($PSVersionTable.PSVersion) - upgrade to PS7+ for parallel processing)" -Level Warning
        Write-Host ""

        $tableIndex = 0
        $tableCount = @($tablesToImport).Count
        foreach ($item in $tablesToImport) {
            $tableIndex++
            $table = $item.Table
            $datFile = $item.FilePath

            $percentComplete = [math]::Round(($tableIndex / $tableCount) * 100)
            Write-Progress -Activity "Importing Temp Tables" `
                           -Status "Processing $table ($tableIndex of $tableCount)" `
                           -PercentComplete $percentComplete

            $errFile = Join-Path $DataPath "$table-import.err"

            # Build BCP import command as a flat array
            $bcpArgs = [System.Collections.ArrayList]@()
            [void]$bcpArgs.Add("$AzureDatabase.$Schema.$table")
            [void]$bcpArgs.Add("in")
            [void]$bcpArgs.Add($datFile)
            [void]$bcpArgs.Add("-S")
            [void]$bcpArgs.Add($AzureServer)
            [void]$bcpArgs.Add("-U")
            [void]$bcpArgs.Add($AzureUser)
            [void]$bcpArgs.Add("-P")
            [void]$bcpArgs.Add($AzurePassword)
            [void]$bcpArgs.Add("-n")
            [void]$bcpArgs.Add("-b")
            [void]$bcpArgs.Add($BatchSize.ToString())
            [void]$bcpArgs.Add("-h")
            [void]$bcpArgs.Add("TABLOCK")
            [void]$bcpArgs.Add("-e")
            [void]$bcpArgs.Add($errFile)

            try {
                $bcpOutput = & bcp $bcpArgs 2>&1
                $exitCode = $LASTEXITCODE

                # Parse rows copied from BCP output
                $rowsCopied = 0
                $bcpOutputStr = $bcpOutput | Out-String
                if ($bcpOutputStr -match '(\d+) rows copied') {
                    $rowsCopied = [int]$Matches[1]
                }

                $result = [PSCustomObject]@{
                    Table      = $table
                    Success    = ($exitCode -eq 0)
                    RowsCopied = $rowsCopied
                    ExitCode   = $exitCode
                    Output     = $bcpOutputStr
                }

                # Show progress inline
                if ($result.Success) {
                    Write-Host "  ✓ $table - $($rowsCopied.ToString('N0')) rows" -ForegroundColor Green
                }
                else {
                    Write-Host "  ✗ $table - Failed (Exit: $exitCode)" -ForegroundColor Red
                }

                $importResults += $result
            }
            catch {
                $result = [PSCustomObject]@{
                    Table      = $table
                    Success    = $false
                    RowsCopied = 0
                    ExitCode   = -1
                    Output     = $_.Exception.Message
                }
                Write-Host "  ✗ $table - Error: $($_.Exception.Message)" -ForegroundColor Red
                $importResults += $result
            }
        }

        Write-Progress -Activity "Importing Temp Tables" -Completed
    }

    $endTime = Get-Date
    $duration = $endTime - $startTime

    # Display import results
    Write-Host ""
    Write-Host "╔══════════════════════════════════════════════════════════════════╗" -ForegroundColor Green
    Write-Host "║                      IMPORT RESULTS REPORT                       ║" -ForegroundColor Green
    Write-Host "╚══════════════════════════════════════════════════════════════════╝" -ForegroundColor Green
    Write-Host ""

    $successImports = @($importResults | Where-Object { $_.Success })
    $failedImports = @($importResults | Where-Object { -not $_.Success })
    $totalRowsImported = ($successImports | Measure-Object -Property RowsCopied -Sum).Sum
    if ($null -eq $totalRowsImported) { $totalRowsImported = 0 }

    # Show successful imports
    if ($successImports.Count -gt 0) {
        Write-Host "Successful Imports:" -ForegroundColor Green
        $headerFormat = "{0,-40} {1,15}"
        Write-Host ($headerFormat -f "TABLE NAME", "ROWS IMPORTED") -ForegroundColor White
        Write-Host ("-" * 60) -ForegroundColor Gray

        foreach ($result in $successImports | Sort-Object -Property RowsCopied -Descending) {
            Write-Host ($headerFormat -f $result.Table, $result.RowsCopied.ToString("N0")) -ForegroundColor Green
        }
        Write-Host ""
    }

    # Show failed imports
    if ($failedImports.Count -gt 0) {
        Write-Host "Failed Imports:" -ForegroundColor Red
        foreach ($result in $failedImports) {
            Write-Host "  ✗ $($result.Table) - Exit code: $($result.ExitCode)" -ForegroundColor Red

            # Save error details
            $errorLog = Join-Path $DataPath "$($result.Table)-import-error.log"
            $result.Output | Out-File -FilePath $errorLog -Force
            Write-Host "    Error log saved to: $errorLog" -ForegroundColor Gray
        }
        Write-Host ""
    }

    # Summary
    Write-Host ("-" * 60) -ForegroundColor Gray
    Write-Host "Import Summary:" -ForegroundColor White
    Write-Host "  Successful: $($successImports.Count) tables" -ForegroundColor Green
    if ($failedImports.Count -gt 0) {
        Write-Host "  Failed: $($failedImports.Count) tables" -ForegroundColor Red
    }
    Write-Host "  Total Rows Imported: $($totalRowsImported.ToString('N0'))" -ForegroundColor Cyan
    Write-Host "  Duration: $($duration.ToString('hh\:mm\:ss'))" -ForegroundColor Cyan

    # Calculate throughput safely (avoid divide by zero)
    $throughput = 0
    if ($duration.TotalSeconds -gt 0) {
        $throughput = [math]::Round($totalRowsImported / $duration.TotalSeconds)
    }
    Write-Host "  Throughput: $($throughput.ToString('N0')) rows/sec" -ForegroundColor Cyan
    Write-Host ""

    # Save results to CSV (only if we have results)
    if ($importResults.Count -gt 0) {
        $resultsCsv = Join-Path $DataPath "temptable-import-results.csv"
        $importResults | Export-Csv -Path $resultsCsv -NoTypeInformation
        Write-LogMessage "Import results saved to: $resultsCsv" -Level Info
    }
}

#endregion

#region ==================== MAIN EXECUTION ====================

# Banner
Write-Host ""
Write-Host "╔══════════════════════════════════════════════════════════════════╗" -ForegroundColor Magenta
Write-Host "║                                                                  ║" -ForegroundColor Magenta
Write-Host "║          MedRecPro Temp Table Migration Utility v1.0             ║" -ForegroundColor Magenta
Write-Host "║                                                                  ║" -ForegroundColor Magenta
Write-Host "║    Bulk Copy Program — Materialized View Data Transfer           ║" -ForegroundColor Magenta
Write-Host "║                                                                  ║" -ForegroundColor Magenta
Write-Host "╚══════════════════════════════════════════════════════════════════╝" -ForegroundColor Magenta
Write-Host ""

# Prerequisites check
Write-Host "Checking prerequisites..." -ForegroundColor White

if (-not (Test-BcpAvailable)) {
    Write-Host "  ✗ BCP utility not found in PATH" -ForegroundColor Red
    Write-Host "    Install SQL Server Command Line Utilities or ensure BCP is in your PATH" -ForegroundColor Gray
    exit 1
}
Write-Host "  ✓ BCP utility available" -ForegroundColor Green

if (-not (Test-SqlCmdAvailable)) {
    Write-Host "  ⚠ SQLCMD utility not found (row counts will be unavailable)" -ForegroundColor Yellow
}
else {
    Write-Host "  ✓ SQLCMD utility available" -ForegroundColor Green
}

# Verify PowerShell version for parallel support
if ($PSVersionTable.PSVersion.Major -lt 7) {
    Write-Host "  ⚠ PowerShell $($PSVersionTable.PSVersion) detected (sequential import mode)" -ForegroundColor Yellow
    Write-Host "    Tip: Install PowerShell 7+ for parallel imports: winget install Microsoft.PowerShell" -ForegroundColor Gray
}
else {
    Write-Host "  ✓ PowerShell $($PSVersionTable.PSVersion.Major) detected (parallel import enabled)" -ForegroundColor Green
}

# Create data directory if needed
if (-not (Test-Path $DataPath)) {
    New-Item -ItemType Directory -Path $DataPath -Force | Out-Null
    Write-Host "  ✓ Created data directory: $DataPath" -ForegroundColor Green
}
else {
    Write-Host "  ✓ Data directory exists: $DataPath" -ForegroundColor Green
}

Write-Host ""

# Display tables being handled
Write-Host "Target tables:" -ForegroundColor White
foreach ($t in $Tables) {
    Write-Host "  - $t" -ForegroundColor Gray
}
Write-Host ""

# Execute requested operation
$exportInventory = $null

switch ($Operation) {
    'Export' {
        $exportInventory = Invoke-ExportPhase
    }
    'Import' {
        Invoke-ImportPhase
    }
    'Both' {
        $exportInventory = Invoke-ExportPhase

        if ($exportInventory) {
            $successfulExports = $exportInventory | Where-Object { $_.Status -eq "Success" }
            if ($successfulExports.Count -gt 0) {
                Write-Host ""
                Write-Host "Export phase complete. Ready for import phase." -ForegroundColor Cyan
                Write-Host ""
                Invoke-ImportPhase -Inventory $exportInventory
            }
            else {
                Write-LogMessage "No successful exports to import." -Level Warning
            }
        }
    }
}

Write-Host ""
Write-LogMessage "Temp table migration script completed." -Level Info
Write-Host ""

#endregion
