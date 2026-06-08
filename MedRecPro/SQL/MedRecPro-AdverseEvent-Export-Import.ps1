<#
.SYNOPSIS
    MedRecPro Adverse Event Migration Script using BCP (Bulk Copy Program)

.DESCRIPTION
    This script bulk transfers the materialized adverse event tables from a local SQL Server
    database to Azure SQL Database using the BCP utility. These tables are rebuilt locally by
    the adverse event denormalization pipeline, then refreshed in production as a complete set.

    The script operates in two distinct phases:
    1. EXPORT: Extracts adverse event tables from local SQL Server to native binary files (.dat)
    2. IMPORT: Truncates Azure target tables in reverse pipeline order, then loads the data files

    Tables Handled (5 total):
    - tmp_FlattenedStandardizedTable              - Stage 3 standardized source rows
    - tmp_FlattenedAdverseEventTable              - Stage 5 adverse event rows
    - tmp_FlattenedAdverseEventCoverageTable      - Stage 5 coverage diagnostics
    - tmp_FlattenedAdverseEventRiskTable          - Stage 5 risk projection snapshot
    - tmp_AeDashboardProductCatalog               - Dashboard product catalog cache

    Key Features:
    - Pipeline-aware import order (source tables first, dashboard cache last)
    - Reverse-order truncation before import (full refresh, not append)
    - Identity value preservation (-E flag) for stable row identity
    - Native binary format (-n) for SQL Server to SQL Server throughput
    - Quoted identifiers enabled (-q) for tables with persisted computed columns/indexes
    - Interactive confirmation before destructive import operations
    - Aborts import when truncation fails to prevent accidental duplicate appends
    - Requires all expected .dat files before import so partial refreshes are not accidental

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
    Directory for storing exported .dat files (default: C:\MedRecPro-Migration\AdverseEvents)

.PARAMETER BatchSize
    Number of rows per transaction batch (default: 50000)

.PARAMETER ParallelThrottle
    Maximum number of concurrent import operations when running PowerShell 7+ (default: 3)

.EXAMPLE
    # Export only from the local MedRecLocal database
    .\MedRecPro-AdverseEvent-Export-Import.ps1 -Operation Export

.EXAMPLE
    # Import only after copying exported files to the target machine
    .\MedRecPro-AdverseEvent-Export-Import.ps1 -Operation Import -AzureServer "myserver.database.windows.net" -AzureDatabase "MedRecPro" -AzureUser "admin"

.EXAMPLE
    # Full export plus production refresh
    .\MedRecPro-AdverseEvent-Export-Import.ps1 -Operation Both -AzureServer "myserver.database.windows.net" -AzureDatabase "MedRecPro" -AzureUser "admin"

.NOTES
    Author: MedRecPro Development Team
    Version: 1.0.0
    Requires:
        - SQL Server BCP utility (installed with SQL Server or SQL Server command line utilities)
        - SQLCMD utility for row counts, connection testing, and truncation
        - PowerShell 5.1+ (PowerShell 7+ enables parallel imports)
        - Network access to Azure SQL Database (firewall rules configured)
        - Target adverse event tables must already exist in Azure with matching schema

    Security Note:
        - Avoid storing passwords in scripts; use -AzurePassword only from a secure prompt/session
        - Consider Azure AD authentication for production workflows when available
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
    [string]$DataPath = "C:\MedRecPro-Migration\AdverseEvents",

    [Parameter(Mandatory = $false)]
    [int]$BatchSize = 50000,

    [Parameter(Mandatory = $false)]
    [int]$ParallelThrottle = 3
)

#region ==================== CONFIGURATION ====================

# Import/export order follows the AE pipeline from upstream source rows to dashboard cache.
$Tables = @(
    "tmp_FlattenedStandardizedTable"
    "tmp_FlattenedAdverseEventTable"
    "tmp_FlattenedAdverseEventCoverageTable"
    "tmp_FlattenedAdverseEventRiskTable"
    "tmp_AeDashboardProductCatalog"
)

# Truncation order is the reverse of import order so downstream/cache tables clear first.
$TruncationOrder = @(
    "tmp_AeDashboardProductCatalog"
    "tmp_FlattenedAdverseEventRiskTable"
    "tmp_FlattenedAdverseEventCoverageTable"
    "tmp_FlattenedAdverseEventTable"
    "tmp_FlattenedStandardizedTable"
)

$Schema = "dbo"

#endregion

#region ==================== HELPER FUNCTIONS ====================

function Write-LogMessage {
    <#
    .SYNOPSIS
        Writes a timestamped message to the console and migration log.
    #>
    param(
        [Parameter(Mandatory = $true)]
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

    $logFile = Join-Path $DataPath "adverseevent-migration-log.txt"
    "[$timestamp] [$Level] $Message" | Out-File -FilePath $logFile -Append -ErrorAction SilentlyContinue
}

function ConvertTo-PlainText {
    <#
    .SYNOPSIS
        Converts a secure string to plaintext for utilities that require command-line passwords.
    #>
    param(
        [Parameter(Mandatory = $true)]
        [SecureString]$SecureString
    )

    $bstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($SecureString)
    try {
        return [Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstr)
    }
    finally {
        [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr)
    }
}

function Format-FileSize {
    <#
    .SYNOPSIS
        Converts bytes to a human-readable file size string.
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
        Verifies that the BCP utility is available in PATH.
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
        Verifies that the SQLCMD utility is available in PATH.
    #>
    try {
        $null = Get-Command sqlcmd -ErrorAction Stop
        return $true
    }
    catch {
        return $false
    }
}

function Get-TableRowCount {
    <#
    .SYNOPSIS
        Returns the row count for a table using SQLCMD.
    #>
    param(
        [Parameter(Mandatory = $true)]
        [string]$Server,

        [Parameter(Mandatory = $true)]
        [string]$Database,

        [Parameter(Mandatory = $true)]
        [string]$TableName,

        [string]$User,

        [string]$Password
    )

    if (-not (Test-SqlCmdAvailable)) {
        return -1
    }

    $query = "SET NOCOUNT ON; SELECT COUNT_BIG(*) FROM [$Schema].[$TableName]"

    if ($User -and $Password) {
        $result = sqlcmd -S $Server -d $Database -U $User -P $Password -Q $query -h -1 -W 2>$null
    }
    else {
        $result = sqlcmd -S $Server -d $Database -Q $query -h -1 -W -E 2>$null
    }

    if ($result -match '^\s*\d+\s*$') {
        return [long]$result.Trim()
    }

    return -1
}

function Test-AzureSqlConnection {
    <#
    .SYNOPSIS
        Tests Azure SQL connectivity before truncation and import.
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

    $testQuery = "SET NOCOUNT ON; SELECT 1 AS ConnectionTest"

    try {
        $result = sqlcmd -S $Server -d $Database -U $User -P $Password -Q $testQuery -h -1 -W -b -t 30 2>&1
        $exitCode = $LASTEXITCODE

        if ($exitCode -eq 0 -and $result -match '^\s*1\s*$') {
            Write-Host "  OK: Connection successful" -ForegroundColor Green
            Write-Host ""
            return $true
        }

        Write-Host "  ERROR: Connection failed" -ForegroundColor Red
        Write-Host ""

        $errorText = $result | Out-String
        if ($errorText -match "Login failed") {
            Write-Host "    Authentication failed. Check username and password." -ForegroundColor Red
        }
        elseif ($errorText -match "Cannot open server|server was not found") {
            Write-Host "    Server not reachable. Check server name and network connectivity." -ForegroundColor Red
        }
        elseif ($errorText -match "firewall") {
            Write-Host "    Firewall blocked the connection. Add your IP to Azure SQL firewall rules." -ForegroundColor Red
        }
        elseif ($errorText -match "database .* does not exist|Cannot open database") {
            Write-Host "    Database not found. Verify database name." -ForegroundColor Red
        }
        else {
            Write-Host "    $errorText" -ForegroundColor Red
        }

        Write-Host ""
        return $false
    }
    catch {
        Write-Host "  ERROR: Connection test failed with exception:" -ForegroundColor Red
        Write-Host "    $($_.Exception.Message)" -ForegroundColor Red
        Write-Host ""
        return $false
    }
}

function Invoke-AzureTruncate {
    <#
    .SYNOPSIS
        Truncates the target AE tables on Azure SQL before import.
    .DESCRIPTION
        Executes TRUNCATE TABLE in reverse pipeline order. The caller aborts import when
        any truncation fails, preventing accidental append/duplicate behavior.
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

    Write-Host "Truncating target tables on Azure (reverse pipeline order)..." -ForegroundColor Cyan
    $allSuccess = $true

    foreach ($table in $TablesToTruncate) {
        $truncateQuery = "SET XACT_ABORT ON; TRUNCATE TABLE [$Schema].[$table];"

        try {
            $result = sqlcmd -S $Server -d $Database -U $User -P $Password -Q $truncateQuery -b -t 120 2>&1
            $exitCode = $LASTEXITCODE

            if ($exitCode -eq 0) {
                Write-Host "  OK: Truncated $table" -ForegroundColor Green
            }
            else {
                $errorText = $result | Out-String
                Write-Host "  ERROR: Failed to truncate $table" -ForegroundColor Red
                Write-Host "    $errorText" -ForegroundColor Red
                $allSuccess = $false
            }
        }
        catch {
            Write-Host "  ERROR: Exception truncating ${table}: $($_.Exception.Message)" -ForegroundColor Red
            $allSuccess = $false
        }
    }

    Write-Host ""
    return $allSuccess
}

function Get-ImportInventoryFromFiles {
    <#
    .SYNOPSIS
        Builds import inventory from expected .dat files in the data directory.
    #>
    $inventory = @()
    $missingTables = @()

    foreach ($table in $Tables) {
        $datFile = Join-Path $DataPath "$table.dat"

        if (-not (Test-Path $datFile)) {
            $missingTables += $table
            continue
        }

        $fileInfo = Get-Item $datFile
        $inventory += [PSCustomObject]@{
            Table    = $table
            Rows     = -1
            FileSize = $fileInfo.Length
            FilePath = $fileInfo.FullName
            Status   = "Pending"
        }
    }

    if ($missingTables.Count -gt 0) {
        Write-LogMessage "Missing required .dat files for import:" -Level Error
        foreach ($table in $missingTables) {
            Write-Host "  - $table.dat" -ForegroundColor Red
        }
        Write-Host ""
        return $null
    }

    return $inventory
}

function Get-RequiredImportInventory {
    <#
    .SYNOPSIS
        Filters an export inventory to the complete expected AE table set.
    #>
    param(
        [array]$Inventory
    )

    if (-not $Inventory) {
        return Get-ImportInventoryFromFiles
    }

    $byTable = @{}
    foreach ($item in $Inventory) {
        if (($item.Status -eq "Success" -or $item.Status -eq "Pending") -and $Tables -contains $item.Table) {
            $byTable[$item.Table] = $item
        }
    }

    $missingTables = @()
    foreach ($table in $Tables) {
        if (-not $byTable.ContainsKey($table)) {
            $missingTables += $table
        }
    }

    if ($missingTables.Count -gt 0) {
        Write-LogMessage "Import requires all five AE export files. Missing tables:" -Level Error
        foreach ($table in $missingTables) {
            Write-Host "  - $table" -ForegroundColor Red
        }
        Write-Host ""
        return $null
    }

    return @($Tables | ForEach-Object { $byTable[$_] })
}

#endregion

#region ==================== EXPORT OPERATIONS ====================

function Invoke-ExportPhase {
    <#
    .SYNOPSIS
        Exports AE tables from local SQL Server to native binary files.
    .DESCRIPTION
        Iterates through the configured AE tables and exports each to a .dat file using
        BCP native format. Inventory is collected for row counts, file sizes, and import handoff.
    #>

    $existingDatFiles = Get-ChildItem -Path $DataPath -Filter "*.dat" -ErrorAction SilentlyContinue
    if ($existingDatFiles -and $existingDatFiles.Count -gt 0) {
        Write-Host ""
        Write-Host "==================================================================" -ForegroundColor Yellow
        Write-Host "EXISTING FILES DETECTED" -ForegroundColor Yellow
        Write-Host "Found existing .dat files in the export directory." -ForegroundColor Yellow
        Write-Host "BCP appends to existing files, which can duplicate exported data." -ForegroundColor Yellow
        Write-Host "==================================================================" -ForegroundColor Yellow
        Write-Host ""

        $cleanup = Read-Host "Delete existing .dat/.err files before export? (Y/N)"
        if ($cleanup -eq 'Y' -or $cleanup -eq 'y') {
            $existingDatFiles | Remove-Item -Force
            Get-ChildItem -Path $DataPath -Filter "*.err" -ErrorAction SilentlyContinue | Remove-Item -Force
            Write-LogMessage "Cleaned up existing export files" -Level Success
        }
        else {
            Write-LogMessage "Export cancelled. Clean up files manually or confirm cleanup to proceed." -Level Warning
            return $null
        }
    }

    Write-Host ""
    Write-Host "==================================================================" -ForegroundColor Cyan
    Write-Host "EXPORT PHASE - LOCAL ADVERSE EVENT TABLES" -ForegroundColor Cyan
    Write-Host "==================================================================" -ForegroundColor Cyan
    Write-Host ""

    Write-LogMessage "Starting export from $LocalServer.$LocalDatabase" -Level Info
    Write-LogMessage "Data directory: $DataPath" -Level Info
    Write-LogMessage "Total tables to export: $($Tables.Count)" -Level Info
    Write-Host ""

    $inventory = @()
    $successCount = 0
    $failCount = 0
    $totalRows = 0
    $totalSize = 0
    $tableIndex = 0

    foreach ($table in $Tables) {
        $tableIndex++
        $percentComplete = [math]::Round(($tableIndex / $Tables.Count) * 100)

        Write-Progress -Activity "Exporting Adverse Event Tables" `
                       -Status "Processing $table ($tableIndex of $($Tables.Count))" `
                       -PercentComplete $percentComplete

        $datFile = Join-Path $DataPath "$table.dat"
        $errFile = Join-Path $DataPath "$table.err"

        $bcpArgs = @(
            "$LocalDatabase.$Schema.$table"
            "out"
            $datFile
            "-S", $LocalServer
            "-T"
            "-n"
            "-q"
            "-e", $errFile
        )

        Write-LogMessage "Exporting: $table" -Level Info

        try {
            $bcpOutput = & bcp @bcpArgs 2>&1
            $bcpExitCode = $LASTEXITCODE

            if ($bcpExitCode -eq 0 -and (Test-Path $datFile)) {
                $rowCount = Get-TableRowCount -Server $LocalServer `
                                              -Database $LocalDatabase `
                                              -TableName $table

                $fileInfo = Get-Item $datFile
                $fileSize = $fileInfo.Length

                $inventory += [PSCustomObject]@{
                    Table    = $table
                    Rows     = $rowCount
                    FileSize = $fileSize
                    FilePath = $datFile
                    Status   = "Success"
                }

                $successCount++
                if ($rowCount -gt 0) { $totalRows += $rowCount }
                $totalSize += $fileSize

                $rowText = if ($rowCount -ge 0) { $rowCount.ToString("N0") } else { "unknown" }
                Write-LogMessage "  OK: $table - $rowText rows ($(Format-FileSize $fileSize))" -Level Success

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
                Write-LogMessage "  ERROR: $table export failed (exit code: $bcpExitCode)" -Level Error

                if ($bcpOutput) {
                    $bcpOutput | Out-File -FilePath (Join-Path $DataPath "$table-export-error.log") -Force
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
            Write-LogMessage "  ERROR: $table export exception: $($_.Exception.Message)" -Level Error
        }
    }

    Write-Progress -Activity "Exporting Adverse Event Tables" -Completed

    Write-Host ""
    Write-Host "==================================================================" -ForegroundColor Green
    Write-Host "EXPORT INVENTORY REPORT" -ForegroundColor Green
    Write-Host "==================================================================" -ForegroundColor Green
    Write-Host ""

    $headerFormat = "{0,-50} {1,15} {2,15} {3,-10}"
    Write-Host ($headerFormat -f "TABLE NAME", "ROWS", "FILE SIZE", "STATUS") -ForegroundColor White
    Write-Host ("-" * 95) -ForegroundColor Gray

    foreach ($item in $inventory) {
        $color = if ($item.Status -eq "Success") { "Green" } else { "Red" }
        $rowDisplay = if ($item.Rows -ge 0) { $item.Rows.ToString("N0") } else { "unknown" }
        Write-Host ($headerFormat -f $item.Table, $rowDisplay, (Format-FileSize $item.FileSize), $item.Status) -ForegroundColor $color
    }

    Write-Host ""
    Write-Host "Export Summary:" -ForegroundColor White
    Write-Host "  Successful: $successCount tables" -ForegroundColor Green
    if ($failCount -gt 0) {
        Write-Host "  Failed: $failCount tables" -ForegroundColor Red
    }
    Write-Host "  Total Rows: $($totalRows.ToString('N0'))" -ForegroundColor Cyan
    Write-Host "  Total Size: $(Format-FileSize $totalSize)" -ForegroundColor Cyan
    Write-Host ""

    $inventoryCsv = Join-Path $DataPath "adverseevent-export-inventory.csv"
    $inventory | Export-Csv -Path $inventoryCsv -NoTypeInformation
    Write-LogMessage "Inventory saved to: $inventoryCsv" -Level Info

    return $inventory
}

#endregion

#region ==================== IMPORT OPERATIONS ====================

function Invoke-ImportPhase {
    <#
    .SYNOPSIS
        Truncates and imports AE tables into Azure SQL.
    .DESCRIPTION
        Validates Azure connectivity and expected .dat files, prompts for destructive-operation
        confirmation, truncates all target tables, then imports with BCP using native format.
    #>
    param(
        [array]$Inventory
    )

    Write-Host ""
    Write-Host "==================================================================" -ForegroundColor Yellow
    Write-Host "IMPORT PHASE - AZURE SQL ADVERSE EVENT TABLES" -ForegroundColor Yellow
    Write-Host "==================================================================" -ForegroundColor Yellow
    Write-Host ""

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
        $AzurePassword = ConvertTo-PlainText -SecureString $securePassword
    }

    $connectionValid = Test-AzureSqlConnection -Server $AzureServer `
                                               -Database $AzureDatabase `
                                               -User $AzureUser `
                                               -Password $AzurePassword

    if (-not $connectionValid) {
        Write-LogMessage "Cannot proceed with import because Azure SQL connection failed." -Level Error
        return
    }

    $tablesToImport = Get-RequiredImportInventory -Inventory $Inventory
    if (-not $tablesToImport) {
        return
    }

    Write-Host "Import Configuration:" -ForegroundColor White
    Write-Host "  Target Server:    $AzureServer" -ForegroundColor Cyan
    Write-Host "  Target Database:  $AzureDatabase" -ForegroundColor Cyan
    Write-Host "  Tables to Import: $($tablesToImport.Count)" -ForegroundColor Cyan
    Write-Host "  Batch Size:       $($BatchSize.ToString('N0')) rows" -ForegroundColor Cyan
    if ($PSVersionTable.PSVersion.Major -ge 7) {
        Write-Host "  Import Mode:      Parallel ($ParallelThrottle threads)" -ForegroundColor Cyan
    }
    else {
        Write-Host "  Import Mode:      Sequential (PowerShell 7+ enables parallel)" -ForegroundColor Cyan
    }

    $totalImportSize = ($tablesToImport | Measure-Object -Property FileSize -Sum).Sum
    if ($null -eq $totalImportSize) { $totalImportSize = 0 }
    Write-Host "  Total Data Size:  $(Format-FileSize $totalImportSize)" -ForegroundColor Cyan
    Write-Host ""

    Write-Host "Tables queued for import (pipeline order):" -ForegroundColor White
    Write-Host ("-" * 70) -ForegroundColor Gray
    foreach ($item in $tablesToImport) {
        Write-Host "  $($item.Table) ($(Format-FileSize $item.FileSize))" -ForegroundColor Gray
    }
    Write-Host ""

    Write-Host "==================================================================" -ForegroundColor Red
    Write-Host "WARNING" -ForegroundColor Red
    Write-Host "This operation will TRUNCATE and re-populate the target adverse" -ForegroundColor Red
    Write-Host "event tables on Azure SQL. This is a full refresh, not an append." -ForegroundColor Red
    Write-Host "" -ForegroundColor Red
    Write-Host "Truncation order:" -ForegroundColor Red
    foreach ($table in $TruncationOrder) {
        Write-Host "  - $table" -ForegroundColor Red
    }
    Write-Host "" -ForegroundColor Red
    Write-Host "If any truncate fails, the import will abort to avoid duplicates." -ForegroundColor Red
    Write-Host "==================================================================" -ForegroundColor Red
    Write-Host ""

    $confirmation = Read-Host "Type 'IMPORT' to proceed with truncate + import"

    if ($confirmation -ne 'IMPORT') {
        Write-LogMessage "Import cancelled by user." -Level Warning
        return
    }

    Write-Host ""
    $truncateSuccess = Invoke-AzureTruncate -Server $AzureServer `
                                            -Database $AzureDatabase `
                                            -User $AzureUser `
                                            -Password $AzurePassword `
                                            -TablesToTruncate $TruncationOrder

    if (-not $truncateSuccess) {
        Write-LogMessage "Import aborted because one or more truncates failed. No BCP import was attempted." -Level Error
        return
    }

    Write-LogMessage "Starting import to Azure SQL..." -Level Info
    Write-Host ""

    $startTime = Get-Date
    $importResults = @()
    $useParallel = $PSVersionTable.PSVersion.Major -ge 7

    if ($useParallel) {
        Write-LogMessage "Using parallel import (PowerShell $($PSVersionTable.PSVersion.Major), ThrottleLimit: $ParallelThrottle)" -Level Info
        Write-Host ""

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

            $bcpArgs = @(
                "$($params.AzureDatabase).$($params.Schema).$table"
                "in"
                $datFile
                "-S", $params.AzureServer
                "-U", $params.AzureUser
                "-P", $params.AzurePassword
                "-n"
                "-q"
                "-b", $params.BatchSize.ToString()
                "-h", "TABLOCK"
                "-E"
                "-e", $errFile
            )

            try {
                $bcpOutput = & bcp @bcpArgs 2>&1
                $exitCode = $LASTEXITCODE
                $bcpOutputText = $bcpOutput | Out-String
                $rowsCopied = 0

                if ($bcpOutputText -match '(\d+) rows copied') {
                    $rowsCopied = [long]$Matches[1]
                }

                [PSCustomObject]@{
                    Table      = $table
                    Success    = ($exitCode -eq 0)
                    RowsCopied = $rowsCopied
                    ExitCode   = $exitCode
                    Output     = $bcpOutputText
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
        Write-LogMessage "Using sequential import (PowerShell $($PSVersionTable.PSVersion))" -Level Warning
        Write-Host ""

        $tableIndex = 0
        $tableCount = @($tablesToImport).Count

        foreach ($item in $tablesToImport) {
            $tableIndex++
            $table = $item.Table
            $datFile = $item.FilePath

            $percentComplete = [math]::Round(($tableIndex / $tableCount) * 100)
            Write-Progress -Activity "Importing Adverse Event Tables" `
                           -Status "Processing $table ($tableIndex of $tableCount)" `
                           -PercentComplete $percentComplete

            $errFile = Join-Path $DataPath "$table-import.err"

            $bcpArgs = @(
                "$AzureDatabase.$Schema.$table"
                "in"
                $datFile
                "-S", $AzureServer
                "-U", $AzureUser
                "-P", $AzurePassword
                "-n"
                "-q"
                "-b", $BatchSize.ToString()
                "-h", "TABLOCK"
                "-E"
                "-e", $errFile
            )

            try {
                $bcpOutput = & bcp @bcpArgs 2>&1
                $exitCode = $LASTEXITCODE
                $bcpOutputText = $bcpOutput | Out-String
                $rowsCopied = 0

                if ($bcpOutputText -match '(\d+) rows copied') {
                    $rowsCopied = [long]$Matches[1]
                }

                $result = [PSCustomObject]@{
                    Table      = $table
                    Success    = ($exitCode -eq 0)
                    RowsCopied = $rowsCopied
                    ExitCode   = $exitCode
                    Output     = $bcpOutputText
                }

                if ($result.Success) {
                    Write-Host "  OK: $table - $($rowsCopied.ToString('N0')) rows" -ForegroundColor Green
                }
                else {
                    Write-Host "  ERROR: $table failed (exit: $exitCode)" -ForegroundColor Red
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

                Write-Host "  ERROR: $table exception: $($_.Exception.Message)" -ForegroundColor Red
                $importResults += $result
            }
        }

        Write-Progress -Activity "Importing Adverse Event Tables" -Completed
    }

    $endTime = Get-Date
    $duration = $endTime - $startTime

    Write-Host ""
    Write-Host "==================================================================" -ForegroundColor Green
    Write-Host "IMPORT RESULTS REPORT" -ForegroundColor Green
    Write-Host "==================================================================" -ForegroundColor Green
    Write-Host ""

    $successImports = @($importResults | Where-Object { $_.Success })
    $failedImports = @($importResults | Where-Object { -not $_.Success })
    $totalRowsImported = ($successImports | Measure-Object -Property RowsCopied -Sum).Sum
    if ($null -eq $totalRowsImported) { $totalRowsImported = 0 }

    if ($successImports.Count -gt 0) {
        Write-Host "Successful Imports:" -ForegroundColor Green
        $headerFormat = "{0,-50} {1,15}"
        Write-Host ($headerFormat -f "TABLE NAME", "ROWS IMPORTED") -ForegroundColor White
        Write-Host ("-" * 70) -ForegroundColor Gray

        foreach ($result in $successImports | Sort-Object -Property RowsCopied -Descending) {
            Write-Host ($headerFormat -f $result.Table, $result.RowsCopied.ToString("N0")) -ForegroundColor Green
        }
        Write-Host ""
    }

    if ($failedImports.Count -gt 0) {
        Write-Host "Failed Imports:" -ForegroundColor Red
        foreach ($result in $failedImports) {
            Write-Host "  ERROR: $($result.Table) - exit code: $($result.ExitCode)" -ForegroundColor Red

            $errorLog = Join-Path $DataPath "$($result.Table)-import-error.log"
            $result.Output | Out-File -FilePath $errorLog -Force
            Write-Host "    Error log saved to: $errorLog" -ForegroundColor Gray
        }
        Write-Host ""
    }

    Write-Host ("-" * 70) -ForegroundColor Gray
    Write-Host "Import Summary:" -ForegroundColor White
    Write-Host "  Successful: $($successImports.Count) tables" -ForegroundColor Green
    if ($failedImports.Count -gt 0) {
        Write-Host "  Failed: $($failedImports.Count) tables" -ForegroundColor Red
    }
    Write-Host "  Total Rows Imported: $($totalRowsImported.ToString('N0'))" -ForegroundColor Cyan
    Write-Host "  Duration: $($duration.ToString('hh\:mm\:ss'))" -ForegroundColor Cyan

    $throughput = 0
    if ($duration.TotalSeconds -gt 0) {
        $throughput = [math]::Round($totalRowsImported / $duration.TotalSeconds)
    }
    Write-Host "  Throughput: $($throughput.ToString('N0')) rows/sec" -ForegroundColor Cyan
    Write-Host ""

    if ($importResults.Count -gt 0) {
        $resultsCsv = Join-Path $DataPath "adverseevent-import-results.csv"
        $importResults | Export-Csv -Path $resultsCsv -NoTypeInformation
        Write-LogMessage "Import results saved to: $resultsCsv" -Level Info
    }
}

#endregion

#region ==================== MAIN EXECUTION ====================

Write-Host ""
Write-Host "==================================================================" -ForegroundColor Magenta
Write-Host "MedRecPro Adverse Event BCP Migration Utility v1.0" -ForegroundColor Magenta
Write-Host "Bulk Copy Program - AE full refresh data transfer" -ForegroundColor Magenta
Write-Host "==================================================================" -ForegroundColor Magenta
Write-Host ""

Write-Host "Checking prerequisites..." -ForegroundColor White

if (-not (Test-BcpAvailable)) {
    Write-Host "  ERROR: BCP utility not found in PATH" -ForegroundColor Red
    Write-Host "    Install SQL Server Command Line Utilities or ensure BCP is in PATH" -ForegroundColor Gray
    exit 1
}
Write-Host "  OK: BCP utility available" -ForegroundColor Green

$sqlCmdAvailable = Test-SqlCmdAvailable
if (-not $sqlCmdAvailable) {
    Write-Host "  WARNING: SQLCMD utility not found" -ForegroundColor Yellow
    if ($Operation -eq 'Import' -or $Operation -eq 'Both') {
        Write-Host "    SQLCMD is required for connection testing and truncation during import." -ForegroundColor Red
        exit 1
    }
}
else {
    Write-Host "  OK: SQLCMD utility available" -ForegroundColor Green
}

if ($PSVersionTable.PSVersion.Major -lt 7) {
    Write-Host "  WARNING: PowerShell $($PSVersionTable.PSVersion) detected (sequential import mode)" -ForegroundColor Yellow
    Write-Host "    Tip: Install PowerShell 7+ for parallel imports: winget install Microsoft.PowerShell" -ForegroundColor Gray
}
else {
    Write-Host "  OK: PowerShell $($PSVersionTable.PSVersion.Major) detected (parallel import enabled)" -ForegroundColor Green
}

if (-not (Test-Path $DataPath)) {
    New-Item -ItemType Directory -Path $DataPath -Force | Out-Null
    Write-Host "  OK: Created data directory: $DataPath" -ForegroundColor Green
}
else {
    Write-Host "  OK: Data directory exists: $DataPath" -ForegroundColor Green
}

Write-Host ""
Write-Host "Target tables (import order):" -ForegroundColor White
foreach ($table in $Tables) {
    Write-Host "  - $table" -ForegroundColor Gray
}
Write-Host ""
Write-Host "Target tables (truncate order):" -ForegroundColor White
foreach ($table in $TruncationOrder) {
    Write-Host "  - $table" -ForegroundColor Gray
}
Write-Host ""

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
            $successfulExports = @($exportInventory | Where-Object { $_.Status -eq "Success" })
            if ($successfulExports.Count -eq $Tables.Count) {
                Write-Host ""
                Write-Host "Export phase complete. Ready for import phase." -ForegroundColor Cyan
                Write-Host ""
                Invoke-ImportPhase -Inventory $exportInventory
            }
            else {
                Write-LogMessage "Export phase did not produce all required files. Import skipped." -Level Error
            }
        }
    }
}

Write-Host ""
Write-LogMessage "Adverse event migration script completed." -Level Info
Write-Host ""

#endregion
