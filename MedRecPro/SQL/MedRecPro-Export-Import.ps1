<#
.SYNOPSIS
    MedRecPro Database Migration Script using BCP (Bulk Copy Program)
    
.DESCRIPTION
    This script facilitates bulk data transfer from a local SQL Server instance to Azure SQL Database
    using the BCP utility. It is designed to minimize Azure vCore consumption by performing heavy
    data processing locally before uploading to the cloud.
    
    The script operates in two distinct phases:
    1. EXPORT: Extracts data from local SQL Server to native binary files (.dat)
    2. IMPORT: Loads data files into Azure SQL Database using parallel operations
    
    Key Features:
    - Native binary format (-n) for maximum performance between SQL Server instances
    - Parallel import operations with configurable throttle limit
    - Detailed inventory reporting after export phase
    - Interactive confirmation before destructive import operations
    - Comprehensive error logging and progress tracking
    - No foreign key dependencies assumed (tables can be processed in any order)
    
.PARAMETER Operation
    Specifies which phase to execute: 'Export', 'Import', or 'Both'
    
.PARAMETER LocalServer
    The local SQL Server instance name (default: localhost)
    
.PARAMETER LocalDatabase
    The local database name (default: MedRecPro)
    
.PARAMETER AzureServer
    The Azure SQL Database server (e.g., yourserver.database.windows.net)
    
.PARAMETER AzureDatabase
    The Azure SQL Database name
    
.PARAMETER AzureUser
    The Azure SQL Database username
    
.PARAMETER AzurePassword
    The Azure SQL Database password (will prompt securely if not provided)
    
.PARAMETER DataPath
    Directory for storing exported .dat files (default: C:\MedRecPro-Migration)
    
.PARAMETER BatchSize
    Number of rows per transaction batch (default: 50000)
    
.PARAMETER ParallelThrottle
    Maximum number of concurrent import operations (default: 4)
    
.EXAMPLE
    # Export only
    .\MedRecPro-BCP-Migration.ps1 -Operation Export
    
.EXAMPLE
    # Import only (after export completed on another machine)
    .\MedRecPro-BCP-Migration.ps1 -Operation Import -AzureServer "myserver.database.windows.net" -AzureDatabase "MedRecPro" -AzureUser "admin"
    
.EXAMPLE
    # Full migration
    .\MedRecPro-BCP-Migration.ps1 -Operation Both -AzureServer "myserver.database.windows.net" -AzureDatabase "MedRecPro" -AzureUser "admin"
    
.NOTES
    Author: MedRecPro Development Team
    Version: 1.0.0
    Requires: 
        - SQL Server BCP utility (installed with SQL Server or SSMS)
        - PowerShell 7.0+ (for ForEach-Object -Parallel support)
        - Network access to Azure SQL Database (firewall rules configured)

    Preparation Recommendations:
        - Clear the label data prior to importing new table data
        - Use MedRecPro-AzureNuke.sql for truncation
    
    Performance Recommendations:
        - Disable indexes on Azure target tables before import, rebuild after
        - Temporarily scale up Azure SQL tier during import for faster throughput
        - Ensure adequate disk space for .dat files (estimate ~1.5x raw data size)

        -- Disable all non-clustered indexes
            MedRecPro-AzureDisableIndex.sql

        -- After import, rebuild them
            MedRecPro-AzureRebuildIndex.sql
        
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
    [string]$DataPath = "C:\MedRecPro-Migration",
    
    [Parameter(Mandatory = $false)]
    [int]$BatchSize = 50000,
    
    [Parameter(Mandatory = $false)]
    [int]$ParallelThrottle = 4
)

#region ==================== CONFIGURATION ====================

# Complete list of MedRecPro tables for migration
# No foreign key relationships defined - tables can be processed in any order
$Tables = @(
    "ActiveMoiety"
    "AdditionalIdentifier"
    "Address"
    "Analyte"
    "ApplicationType"
    "AttachedDocument"
    "BillingUnitIndex"
    "BusinessOperation"
    "BusinessOperationProductLink"
    "BusinessOperationQualifier"
    "CertificationProductLink"
    "Characteristic"
    "Commodity"
    "ComplianceAction"
    "ContactParty"
    "ContactPartyTelecom"
    "ContactPerson"
    "ContributingFactor"
    "DisciplinaryAction"
    "Document"
    "DocumentAuthor"
    "DocumentRelationship"
    "DocumentRelationshipIdentifier"
    "DosingSpecification"
    "EquivalentEntity"
    "FacilityProductLink"
    "GenericMedicine"
    "Holder"
    "IdentifiedSubstance"
    "Ingredient"
    "IngredientInstance"
    "IngredientSourceProduct"
    "IngredientSubstance"
    "InteractionConsequence"
    "InteractionIssue"
    "LegalAuthenticator"
    "License"
    "LotHierarchy"
    "LotIdentifier"
    "MarketingCategory"
    "MarketingStatus"
    "Moiety"
    "NamedEntity"
    "NCTLink"
    "ObservationCriterion"
    "ObservationMedia"
    "Organization"
    "OrganizationIdentifier"
    "OrganizationTelecom"
    "PackageIdentifier"
    "PackagingHierarchy"
    "PackagingLevel"
    "PartOfAssembly"
    "PharmacologicClass"
    "PharmacologicClassHierarchy"
    "PharmacologicClassLink"
    "PharmacologicClassName"
    "Policy"
    "Product"
    "ProductConcept"
    "ProductConceptEquivalence"
    "ProductEvent"
    "ProductIdentifier"
    "ProductInstance"
    "ProductPart"
    "ProductRouteOfAdministration"
    "ProductWebLink"
    "Protocol"
    "ReferenceSubstance"
    "RelatedDocument"
    "REMSApproval"
    "REMSElectronicResource"
    "REMSMaterial"
    "RenderedMedia"
    "Requirement"
    "ResponsiblePersonLink"
    "Section"
    "SectionExcerptHighlight"
    "SectionHierarchy"
    "SectionTextContent"
    "SpecializedKind"
    "SpecifiedSubstance"
    "SplData"
    "Stakeholder"
    "StructuredBody"
    "SubstanceSpecification"
    "Telecom"
    "TerritorialAuthority"
    "TextList"
    "TextListItem"
    "TextTable"
    "TextTableCell"
    "TextTableColumn"
    "TextTableRow"
    "WarningLetterDate"
    "WarningLetterProductInfo"
)

# Schema name (modify if using a different schema)
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
    $logFile = Join-Path $DataPath "migration-log.txt"
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

#endregion

#region ==================== EXPORT OPERATIONS ====================

function Invoke-ExportPhase {
    <#
    .SYNOPSIS
        Exports all tables from local SQL Server to native binary files
        
    .DESCRIPTION
        Iterates through all configured tables and exports each to a .dat file
        using BCP in native format (-n). Collects row counts and file sizes
        for inventory reporting.
    #>
    
    Write-Host ""
    Write-Host "╔══════════════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
    Write-Host "║                    EXPORT PHASE - LOCAL SERVER                   ║" -ForegroundColor Cyan
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
        
        Write-Progress -Activity "Exporting Tables" `
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
    
    Write-Progress -Activity "Exporting Tables" -Completed
    
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
    $inventoryCsv = Join-Path $DataPath "export-inventory.csv"
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
        Imports all exported data files to Azure SQL Database in parallel
        
    .DESCRIPTION
        Loads .dat files into Azure SQL using parallel BCP operations.
        Requires user confirmation before proceeding. Uses TABLOCK hint
        and configurable batch sizes for optimal performance.
    #>
    param(
        [array]$Inventory
    )
    
    Write-Host ""
    Write-Host "╔══════════════════════════════════════════════════════════════════╗" -ForegroundColor Yellow
    Write-Host "║                   IMPORT PHASE - AZURE SQL                       ║" -ForegroundColor Yellow
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
    
    # Discover available .dat files if no inventory provided
    if (-not $Inventory) {
        $datFiles = Get-ChildItem -Path $DataPath -Filter "*.dat" -File
        $Inventory = $datFiles | ForEach-Object {
            [PSCustomObject]@{
                Table    = $_.BaseName
                FilePath = $_.FullName
                FileSize = $_.Length
                Rows     = -1  # Unknown when loading from files
                Status   = "Pending"
            }
        }
    }
    
    # Filter to only successful exports
    $tablesToImport = $Inventory | Where-Object { 
        $_.Status -eq "Success" -or $_.Status -eq "Pending" 
    }
    
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
    Write-Host "  Parallel Threads: $ParallelThrottle" -ForegroundColor Cyan
    Write-Host ""
    
    # Calculate total data size
    $totalImportSize = ($tablesToImport | Measure-Object -Property FileSize -Sum).Sum
    Write-Host "  Total Data Size:  $(Format-FileSize $totalImportSize)" -ForegroundColor Cyan
    Write-Host ""
    
    # List tables to be imported
    Write-Host "Tables queued for import:" -ForegroundColor White
    Write-Host ("-" * 60) -ForegroundColor Gray
    
    $tablesToImport | Sort-Object -Property FileSize -Descending | Select-Object -First 20 | ForEach-Object {
        Write-Host "  $($_.Table) ($(Format-FileSize $_.FileSize))" -ForegroundColor Gray
    }
    
    if ($tablesToImport.Count -gt 20) {
        Write-Host "  ... and $($tablesToImport.Count - 20) more tables" -ForegroundColor Gray
    }
    Write-Host ""
    
    # Confirmation prompt
    Write-Host "╔══════════════════════════════════════════════════════════════════╗" -ForegroundColor Red
    Write-Host "║                             WARNING                              ║" -ForegroundColor Red
    Write-Host "║                                                                  ║" -ForegroundColor Red
    Write-Host "║  This operation will INSERT data into the target Azure SQL       ║" -ForegroundColor Red
    Write-Host "║  database. Ensure that:                                          ║" -ForegroundColor Red
    Write-Host "║                                                                  ║" -ForegroundColor Red
    Write-Host "║  1. Target tables exist with matching schema                     ║" -ForegroundColor Red
    Write-Host "║  2. Target tables are empty (or you intend to append data)       ║" -ForegroundColor Red
    Write-Host "║  3. Indexes are disabled for optimal import performance          ║" -ForegroundColor Red
    Write-Host "║  4. Azure SQL firewall allows your current IP address            ║" -ForegroundColor Red
    Write-Host "║                                                                  ║" -ForegroundColor Red
    Write-Host "╚══════════════════════════════════════════════════════════════════╝" -ForegroundColor Red
    Write-Host ""
    
    $confirmation = Read-Host "Type 'IMPORT' to proceed with the import operation"
    
    if ($confirmation -ne 'IMPORT') {
        Write-LogMessage "Import cancelled by user." -Level Warning
        return
    }
    
    Write-Host ""
    Write-LogMessage "Starting parallel import to Azure SQL..." -Level Info
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
    
    # Execute parallel imports
    $startTime = Get-Date
    
    $importResults = $tablesToImport | ForEach-Object -Parallel {
        $params = $using:importParams
        $table = $_.Table
        $datFile = $_.FilePath
        
        $errFile = Join-Path $params.DataPath "$table-import.err"
        
        # Build BCP import command
        # -n: Native format
        # -b: Batch size (rows per transaction)
        # -h "TABLOCK": Table lock hint for faster bulk insert
        $bcpArgs = @(
            "$($params.AzureDatabase).$($params.Schema).$table"
            "in"
            "`"$datFile`""
            "-S", $params.AzureServer
            "-U", $params.AzureUser
            "-P", $params.AzurePassword
            "-n"
            "-b", $params.BatchSize
            "-h", "TABLOCK"
            "-e", "`"$errFile`""
        )
        
        try {
            $bcpOutput = & bcp $bcpArgs 2>&1
            $exitCode = $LASTEXITCODE
            
            # Parse rows copied from BCP output
            $rowsCopied = 0
            if ($bcpOutput -match '(\d+) rows copied') {
                $rowsCopied = [int]$Matches[1]
            }
            
            [PSCustomObject]@{
                Table      = $table
                Success    = ($exitCode -eq 0)
                RowsCopied = $rowsCopied
                ExitCode   = $exitCode
                Output     = ($bcpOutput | Out-String)
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
    
    $endTime = Get-Date
    $duration = $endTime - $startTime
    
    # Display import results
    Write-Host ""
    Write-Host "╔══════════════════════════════════════════════════════════════════╗" -ForegroundColor Green
    Write-Host "║                      IMPORT RESULTS REPORT                       ║" -ForegroundColor Green
    Write-Host "╚══════════════════════════════════════════════════════════════════╝" -ForegroundColor Green
    Write-Host ""
    
    $successImports = $importResults | Where-Object { $_.Success }
    $failedImports = $importResults | Where-Object { -not $_.Success }
    $totalRowsImported = ($successImports | Measure-Object -Property RowsCopied -Sum).Sum
    
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
    Write-Host "  Throughput: $([math]::Round($totalRowsImported / $duration.TotalSeconds).ToString('N0')) rows/sec" -ForegroundColor Cyan
    Write-Host ""
    
    # Save results to CSV
    $resultsCsv = Join-Path $DataPath "import-results.csv"
    $importResults | Export-Csv -Path $resultsCsv -NoTypeInformation
    Write-LogMessage "Import results saved to: $resultsCsv" -Level Info
}

#endregion

#region ==================== MAIN EXECUTION ====================

# Banner
Write-Host ""
Write-Host "╔══════════════════════════════════════════════════════════════════╗" -ForegroundColor Magenta
Write-Host "║                                                                  ║" -ForegroundColor Magenta
Write-Host "║              MedRecPro BCP Migration Utility v1.0                ║" -ForegroundColor Magenta
Write-Host "║                                                                  ║" -ForegroundColor Magenta
Write-Host "║              Bulk Copy Program Data Transfer Tool                ║" -ForegroundColor Magenta
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
    Write-Host "  ⚠ PowerShell 7+ recommended for parallel import support" -ForegroundColor Yellow
    Write-Host "    Current version: $($PSVersionTable.PSVersion)" -ForegroundColor Gray
    Write-Host "    Falling back to sequential import..." -ForegroundColor Gray
    $ParallelThrottle = 1
}
else {
    Write-Host "  ✓ PowerShell $($PSVersionTable.PSVersion.Major) detected (parallel support enabled)" -ForegroundColor Green
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
Write-LogMessage "Migration script completed." -Level Info
Write-Host ""

#endregion