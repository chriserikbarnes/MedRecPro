<#
.SYNOPSIS
Runs one deliberate, manifest-bound local-to-Azure MedRecPro data refresh.

.DESCRIPTION
Exports all supported data domains before Azure mutation, requires one exact target-specific
confirmation, imports each domain with strict row-count verification, rebuilds indexes, and writes
non-secret run state that can be resumed only against the same verified source, target, scripts,
and data files. Existing domain scripts remain supported lower-level tools.

.PARAMETER AzureServer
Azure SQL logical server name. A normal refresh requires a *.database.windows.net target.

.PARAMETER AzureDatabase
Azure SQL database name that must match DB_NAME() after connection.

.PARAMETER AzureUser
Azure SQL user used for sqlcmd and BCP imports.

.PARAMETER AzurePassword
Optional secure Azure SQL password. The script prompts once when a target operation requires it.

.PARAMETER ExportOnly
Creates and validates a local snapshot without connecting to or mutating Azure.

.PARAMETER ValidateOnly
Runs local and Azure preflight plus target-state checks without exporting or mutating Azure.

.PARAMETER ResumeRun
Existing run directory to resume after its manifest and hashes are verified.

.NOTES
Exit codes: 0 success; 1 preflight; 2 cancellation; 3 export; 4 SQL cleanup; 5 import;
6 rebuild/recovery; 7 final verification; 99 unexpected failure. No generic Force switch exists.

.EXAMPLE
.\MedRecPro-Azure-Data-Refresh.ps1 -AzureServer 'server.database.windows.net' -AzureDatabase 'MedRecPro' -AzureUser 'migration-user'

.EXAMPLE
.\MedRecPro-Azure-Data-Refresh.ps1 -ExportOnly -LocalServer localhost -LocalDatabase MedRecLocal

.EXAMPLE
.\MedRecPro-Azure-Data-Refresh.ps1 -ValidateOnly -AzureServer 'server.database.windows.net' -AzureDatabase 'MedRecPro' -AzureUser 'migration-user'

.EXAMPLE
.\MedRecPro-Azure-Data-Refresh.ps1 -AzureServer 'server.database.windows.net' -AzureDatabase 'MedRecPro' -AzureUser 'migration-user' -RefreshExclusionRules

.EXAMPLE
.\MedRecPro-Azure-Data-Refresh.ps1 -ResumeRun 'C:\MedRecPro-Migration\Refreshes\20260721-12345678' -AzureUser 'migration-user'
#>

[CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'Medium')]
param(
    [string]$LocalServer = 'localhost',

    [string]$LocalDatabase = 'MedRecLocal',

    [string]$AzureServer,

    [string]$AzureDatabase,

    [string]$AzureUser,

    [Security.SecureString]$AzurePassword,

    [string]$DataRoot = 'C:\MedRecPro-Migration\Refreshes',

    [ValidateRange(1, 1000000)]
    [int]$BatchSize = 50000,

    [ValidateRange(1, 32)]
    [int]$ParallelThrottle = 3,

    [switch]$RefreshExclusionRules,

    [switch]$SkipIndexReconciliation,

    [switch]$ExportOnly,

    [string]$ResumeRun,

    [switch]$ValidateOnly
)

Set-StrictMode -Version Latest

#region implementation

function Get-MedRecProRefreshStageCatalog {
    <#
    .SYNOPSIS
    Returns the immutable refresh stage graph used for logs, manifests, and tests.
    #>
    return @(
        [PSCustomObject]@{ Id = 'Preflight'; Name = 'Preflight'; Destructive = $false; Recovery = 'Correct the reported prerequisite before retrying.' },
        [PSCustomObject]@{ Id = 'ExportCore'; Name = 'Export primary data'; Destructive = $false; Recovery = 'Re-export into the run-bound Core directory.' },
        [PSCustomObject]@{ Id = 'ExportOrangeBook'; Name = 'Export Orange Book data'; Destructive = $false; Recovery = 'Re-export into the run-bound OrangeBook directory.' },
        [PSCustomObject]@{ Id = 'ExportTempTables'; Name = 'Export temp tables'; Destructive = $false; Recovery = 'Re-export into the run-bound TempTables directory.' },
        [PSCustomObject]@{ Id = 'ExportAdverseEvents'; Name = 'Export adverse-event tables'; Destructive = $false; Recovery = 'Re-export into the run-bound AdverseEvents directory.' },
        [PSCustomObject]@{ Id = 'ConfirmTarget'; Name = 'Confirm target'; Destructive = $false; Recovery = 'Enter the exact current target token.' },
        [PSCustomObject]@{ Id = 'DisableIndexes'; Name = 'Disable eligible indexes'; Destructive = $true; Recovery = 'Rebuild indexes before leaving the run.' },
        [PSCustomObject]@{ Id = 'ClearPrimary'; Name = 'Clear primary, temp, and adverse-event tables'; Destructive = $true; Recovery = 'Re-run primary cleanup before primary import.' },
        [PSCustomObject]@{ Id = 'ImportCore'; Name = 'Import primary data'; Destructive = $true; Recovery = 'Re-run primary cleanup then import Core.' },
        [PSCustomObject]@{ Id = 'ClearOrangeBook'; Name = 'Clear Orange Book tables'; Destructive = $true; Recovery = 'Re-run Orange Book cleanup before import.' },
        [PSCustomObject]@{ Id = 'ImportOrangeBook'; Name = 'Import Orange Book data'; Destructive = $true; Recovery = 'Re-run Orange Book cleanup then import OrangeBook.' },
        [PSCustomObject]@{ Id = 'ImportTempTables'; Name = 'Import temp tables'; Destructive = $true; Recovery = 'Scope cleanup to temp tables then import TempTables.' },
        [PSCustomObject]@{ Id = 'ImportAdverseEvents'; Name = 'Import adverse-event tables'; Destructive = $true; Recovery = 'Scope cleanup to adverse-event tables then import AdverseEvents.' },
        [PSCustomObject]@{ Id = 'RebuildIndexes'; Name = 'Rebuild indexes and statistics'; Destructive = $true; Recovery = 'Retry rebuild only; use the Query Editor fallback if indexes remain disabled.' },
        [PSCustomObject]@{ Id = 'ReconcileIndexes'; Name = 'Reconcile canonical index definitions'; Destructive = $true; Recovery = 'Retry canonical index reconciliation.' },
        [PSCustomObject]@{ Id = 'FinalVerification'; Name = 'Final verification'; Destructive = $false; Recovery = 'Resolve the reported count, index, or preservation mismatch.' }
    )
}

function Set-MedRecProRefreshMember {
    <#
    .SYNOPSIS
    Sets a named member on either a manifest hashtable or a deserialized manifest object.
    #>
    param(
        [Parameter(Mandatory = $true)]
        [object]$Container,

        [Parameter(Mandatory = $true)]
        [string]$Name,

        [Parameter(Mandatory = $true)]
        [object]$Value
    )

    if ($Container -is [System.Collections.IDictionary]) {
        $Container[$Name] = $Value
    }
    elseif ($Container.PSObject.Properties.Name -contains $Name) {
        $Container.$Name = $Value
    }
    else {
        $Container | Add-Member -MemberType NoteProperty -Name $Name -Value $Value
    }
}

function Set-MedRecProRefreshStage {
    <#
    .SYNOPSIS
    Records one durable stage transition without persisting secrets.
    #>
    param(
        [Parameter(Mandatory = $true)]
        [object]$Manifest,

        [Parameter(Mandatory = $true)]
        [string]$StageId,

        [ValidateSet('Pending', 'Running', 'Succeeded', 'Failed', 'RecoveryRequired', 'Skipped', 'Cancelled')]
        [string]$Status,

        [string]$Message
    )

    Set-MedRecProRefreshMember -Container $Manifest.Stages -Name $StageId -Value ([PSCustomObject]@{
        Status = $Status
        UpdatedAt = (Get-Date).ToUniversalTime().ToString('o')
        Message = $Message
    })
}

function Write-MedRecProRefreshManifest {
    <#
    .SYNOPSIS
    Atomically writes a non-secret refresh manifest in the run directory.
    #>
    param(
        [Parameter(Mandatory = $true)]
        [object]$Manifest,

        [Parameter(Mandatory = $true)]
        [string]$ManifestPath
    )

    $Manifest.UpdatedAt = (Get-Date).ToUniversalTime().ToString('o')
    $temporaryPath = "$ManifestPath.$([Guid]::NewGuid().ToString('N')).tmp"
    $json = $Manifest | ConvertTo-Json -Depth 30
    [IO.File]::WriteAllText($temporaryPath, $json, [Text.UTF8Encoding]::new($true))
    Move-Item -LiteralPath $temporaryPath -Destination $ManifestPath -Force
}

function Write-MedRecProRefreshLog {
    <#
    .SYNOPSIS
    Writes a timestamped and password-redacted line to the run log.
    #>
    param(
        [Parameter(Mandatory = $true)]
        [string]$LogPath,

        [Parameter(Mandatory = $true)]
        [string]$Message
    )

    $redacted = $Message -replace '(?i)(password|pwd)\s*=\s*[^;\s]+', '$1=***'
    $line = '[{0}] {1}' -f (Get-Date).ToString('s'), $redacted
    Add-Content -LiteralPath $LogPath -Value $line -Encoding UTF8
    Write-Host $line
}

function Get-MedRecProRefreshAssets {
    <#
    .SYNOPSIS
    Resolves every worker and SQL asset relative to this script.
    #>
    param([Parameter(Mandatory = $true)][string]$ScriptRoot)

    $assets = [ordered]@{
        WorkerModule = Join-Path $ScriptRoot 'MedRecPro-DataRefreshWorker.psm1'
        CoreWorker = Join-Path $ScriptRoot 'MedRecPro-Export-Import.ps1'
        OrangeBookWorker = Join-Path $ScriptRoot 'MedRecPro-OrangeBook-Export-Import.ps1'
        TempTablesWorker = Join-Path $ScriptRoot 'MedRecPro-TempTable-Export-Import.ps1'
        AdverseEventsWorker = Join-Path $ScriptRoot 'MedRecPro-AdverseEvent-Export-Import.ps1'
        DisableIndexes = Join-Path $ScriptRoot 'MedRecPro-AzureDisableIndex.sql'
        PrimaryNuke = Join-Path $ScriptRoot 'MedRecPro-AzureNuke.sql'
        OrangeBookNuke = Join-Path $ScriptRoot 'MedRecPro-AzureOrangeBookNuke.sql'
        RebuildIndexes = Join-Path $ScriptRoot 'MedRecPro-AzureRebuildIndex.sql'
        ReconcileIndexes = Join-Path $ScriptRoot 'MedRecPro_Indexes.sql'
        QueryEditorRecovery = Join-Path $ScriptRoot 'MedRecPro-AzureOnlineQueryEditorRebuildIndex.sql'
    }

    foreach ($asset in $assets.GetEnumerator()) {
        if (-not (Test-Path -LiteralPath $asset.Value -PathType Leaf)) {
            throw "Required refresh asset is missing: $($asset.Value)"
        }
    }

    return $assets
}

function Get-MedRecProRefreshAssetHashes {
    <#
    .SYNOPSIS
    Gets stable SHA-256 hashes for every manifest-bound script and SQL asset.
    #>
    param([Parameter(Mandatory = $true)][System.Collections.IDictionary]$Assets)

    $hashes = [ordered]@{}
    foreach ($asset in $Assets.GetEnumerator()) {
        $hashes[$asset.Key] = [PSCustomObject]@{
            Path = $asset.Value
            Hash = (Get-FileHash -LiteralPath $asset.Value -Algorithm SHA256).Hash
        }
    }
    return $hashes
}

function Test-MedRecProRefreshAssetHashes {
    <#
    .SYNOPSIS
    Rejects a resume when a worker or SQL asset differs from the recorded run.
    #>
    param([Parameter(Mandatory = $true)][object]$Manifest)

    $records = if ($Manifest.AssetHashes -is [System.Collections.IDictionary]) {
        @($Manifest.AssetHashes.GetEnumerator() | ForEach-Object { [PSCustomObject]@{ Name = $_.Key; Value = $_.Value } })
    }
    else {
        @($Manifest.AssetHashes.PSObject.Properties | ForEach-Object { [PSCustomObject]@{ Name = $_.Name; Value = $_.Value } })
    }
    foreach ($property in $records) {
        $record = $property.Value
        if (-not (Test-Path -LiteralPath $record.Path -PathType Leaf)) {
            throw "Resume rejected because asset '$($property.Name)' is missing: $($record.Path)"
        }
        if ((Get-FileHash -LiteralPath $record.Path -Algorithm SHA256).Hash -ne $record.Hash) {
            throw "Resume rejected because asset '$($property.Name)' changed after this run was created."
        }
    }
}

function Get-MedRecProRefreshScalar {
    <#
    .SYNOPSIS
    Returns the one non-empty scalar line from a sqlcmd result.
    #>
    param([Parameter(Mandatory = $true)][object]$SqlResult)

    if (-not $SqlResult.Success) {
        throw "SQL command failed: $($SqlResult.Output)"
    }
    $lines = @($SqlResult.Output -split "`r?`n" | Where-Object { $_.Trim() })
    if ($lines.Count -ne 1) {
        throw 'SQL command did not return exactly one scalar value.'
    }
    return $lines[0].Trim()
}

function Get-MedRecProRefreshTableFacts {
    <#
    .SYNOPSIS
    Returns dbo user-table names and exact row counts for preflight and postconditions.
    #>
    param(
        [Parameter(Mandatory = $true)][string]$Server,
        [Parameter(Mandatory = $true)][string]$Database,
        [string]$User,
        [Security.SecureString]$Password,
        [switch]$RetryServerless
    )

    $query = @'
SET NOCOUNT ON;
SELECT t.name + '|' + CONVERT(varchar(30), SUM(p.rows))
FROM sys.tables t
INNER JOIN sys.schemas s ON s.schema_id = t.schema_id
INNER JOIN sys.partitions p ON p.object_id = t.object_id AND p.index_id IN (0, 1)
WHERE s.name = 'dbo' AND t.type = 'U'
GROUP BY t.name
ORDER BY t.name;
'@
    $result = Invoke-MedRecProSqlCmd -Server $Server -Database $Database -User $User -Password $Password -Query $query -RetryServerless:$RetryServerless
    if (-not $result.Success) { throw "Unable to enumerate dbo tables: $($result.Output)" }
    $facts = @()
    foreach ($line in @($result.Output -split "`r?`n" | Where-Object { $_.Trim() })) {
        $parts = $line.Trim().Split('|')
        if ($parts.Count -ne 2 -or $parts[1] -notmatch '^\d+$') { throw "Unexpected table fact result: $line" }
        $facts += [PSCustomObject]@{ Table = $parts[0]; Rows = [long]$parts[1] }
    }
    return $facts
}

function Get-MedRecProRefreshSchemaSignature {
    <#
    .SYNOPSIS
    Gets BCP-relevant ordered column metadata for selected tables.
    #>
    param(
        [Parameter(Mandatory = $true)][string]$Server,
        [Parameter(Mandatory = $true)][string]$Database,
        [Parameter(Mandatory = $true)][string[]]$Tables,
        [string]$User,
        [Security.SecureString]$Password,
        [switch]$RetryServerless
    )

    $quotedTables = ($Tables | ForEach-Object { "N'$($_.Replace("'", "''"))'" }) -join ','
    $query = @"
SET NOCOUNT ON;
SELECT t.name + '|' + CONVERT(varchar(10), c.column_id) + '|' + c.name + '|' + ty.name + '|' +
       CONVERT(varchar(10), c.max_length) + '|' + CONVERT(varchar(10), c.precision) + '|' +
       CONVERT(varchar(10), c.scale) + '|' + ISNULL(c.collation_name, '') + '|' +
       CONVERT(varchar(1), c.is_nullable) + '|' + CONVERT(varchar(1), c.is_identity) + '|' + CONVERT(varchar(1), c.is_computed)
FROM sys.tables t
INNER JOIN sys.schemas s ON s.schema_id = t.schema_id
INNER JOIN sys.columns c ON c.object_id = t.object_id
INNER JOIN sys.types ty ON ty.user_type_id = c.user_type_id
WHERE s.name = 'dbo' AND t.name IN ($quotedTables)
ORDER BY t.name, c.column_id;
"@
    $result = Invoke-MedRecProSqlCmd -Server $Server -Database $Database -User $User -Password $Password -Query $query -RetryServerless:$RetryServerless
    if (-not $result.Success) { throw "Unable to read schema signatures: $($result.Output)" }
    return @($result.Output -split "`r?`n" | ForEach-Object { $_.Trim() } | Where-Object { $_ })
}

function Test-MedRecProRefreshNukeReconciliation {
    <#
    .SYNOPSIS
    Fails closed when an enabled nuke pattern would clear populated unmanaged data.
    #>
    param(
        [Parameter(Mandatory = $true)][object[]]$TargetFacts,
        [Parameter(Mandatory = $true)][string[]]$ManagedPrimaryTables,
        [Parameter(Mandatory = $true)][string[]]$ManagedOrangeBookTables,
        [switch]$RefreshExclusionRules
    )

    $unmanaged = @()
    foreach ($fact in $TargetFacts) {
        $primaryNukeTargets = $fact.Table -ne '__EFMigrationsHistory' -and $fact.Table -notlike 'AspNet*' -and $fact.Table -notlike 'OrangeBook*' -and ($RefreshExclusionRules -or $fact.Table -ne 'PharmClassDosageFormExclusion')
        if ($primaryNukeTargets -and $fact.Table -notin $ManagedPrimaryTables -and $fact.Rows -gt 0) {
            $unmanaged += "$($fact.Table) ($($fact.Rows) rows)"
        }
        if ($fact.Table -like 'OrangeBook*' -and $fact.Table -notin $ManagedOrangeBookTables -and $fact.Rows -gt 0) {
            $unmanaged += "$($fact.Table) ($($fact.Rows) rows)"
        }
    }

    if ($unmanaged.Count -gt 0) {
        throw "Preflight refused to clear populated tables that no worker restores: $($unmanaged -join '; '). Extend a worker list or preserve the table deliberately."
    }
}

function Test-MedRecProRefreshConfirmation {
    <#
    .SYNOPSIS
    Validates the single exact token that authorizes a destructive refresh.
    #>
    param(
        [Parameter(Mandatory = $true)][string]$Confirmation,
        [Parameter(Mandatory = $true)][string]$AzureServer,
        [Parameter(Mandatory = $true)][string]$AzureDatabase
    )

    return $Confirmation -ceq ("REFRESH {0}/{1}" -f $AzureServer, $AzureDatabase)
}
function New-MedRecProRefreshExecutableNuke {
    <#
    .SYNOPSIS
    Produces a run-local nuke copy with exactly one preview declaration enabled.
    #>
    param(
        [Parameter(Mandatory = $true)][string]$SourcePath,
        [Parameter(Mandatory = $true)][string]$DestinationPath
    )

    $source = [IO.File]::ReadAllText($SourcePath)
    $pattern = '(?m)^DECLARE @ExecuteCommands BIT = 0;[^\r\n]*\r?$'
    if ([regex]::Matches($source, $pattern).Count -ne 1) {
        throw "Expected exactly one preview declaration in $SourcePath."
    }
    $generated = [regex]::Replace($source, $pattern, 'DECLARE @ExecuteCommands BIT = 1;  -- Enabled only in this manifest-bound run copy.')
    [IO.File]::WriteAllText($DestinationPath, $generated, [Text.UTF8Encoding]::new($true))
}

function Invoke-MedRecProRefreshSqlFile {
    <#
    .SYNOPSIS
    Executes a script through the credential-safe sqlcmd helper and records sanitized output.
    #>
    param(
        [Parameter(Mandatory = $true)][string]$ScriptPath,
        [Parameter(Mandatory = $true)][string]$AzureServer,
        [Parameter(Mandatory = $true)][string]$AzureDatabase,
        [Parameter(Mandatory = $true)][string]$AzureUser,
        [Parameter(Mandatory = $true)][Security.SecureString]$AzurePassword,
        [Parameter(Mandatory = $true)][string]$LogPath,
        [int]$QueryTimeoutSeconds = 0
    )

    $result = Invoke-MedRecProSqlCmd -Server $AzureServer -Database $AzureDatabase -User $AzureUser -Password $AzurePassword -InputFile $ScriptPath -LoginTimeoutSeconds 30 -QueryTimeoutSeconds $QueryTimeoutSeconds -RetryServerless
    if ($result.Output.Trim()) { Write-MedRecProRefreshLog -LogPath $LogPath -Message $result.Output.Trim() }
    if (-not $result.Success) { throw "sqlcmd failed for $ScriptPath (exit $($result.ExitCode))." }
}

function Test-MedRecProRefreshNoDisabledIndexes {
    <#
    .SYNOPSIS
    Verifies that the Azure target contains no disabled named dbo index.
    #>
    param(
        [Parameter(Mandatory = $true)][string]$AzureServer,
        [Parameter(Mandatory = $true)][string]$AzureDatabase,
        [Parameter(Mandatory = $true)][string]$AzureUser,
        [Parameter(Mandatory = $true)][Security.SecureString]$AzurePassword
    )

    $query = "SET NOCOUNT ON; SELECT COUNT_BIG(*) FROM sys.indexes i INNER JOIN sys.tables t ON t.object_id = i.object_id INNER JOIN sys.schemas s ON s.schema_id = t.schema_id WHERE s.name = 'dbo' AND i.name IS NOT NULL AND i.type > 0 AND i.is_disabled = 1;"
    $count = [long](Get-MedRecProRefreshScalar -SqlResult (Invoke-MedRecProSqlCmd -Server $AzureServer -Database $AzureDatabase -User $AzureUser -Password $AzurePassword -Query $query -RetryServerless))
    if ($count -ne 0) { throw "$count disabled named dbo index(es) remain." }
}

function Test-MedRecProRefreshTablesEmpty {
    <#
    .SYNOPSIS
    Verifies that every expected cleanup target is empty after a generated nuke.
    #>
    param(
        [Parameter(Mandatory = $true)][object[]]$TargetFacts,
        [Parameter(Mandatory = $true)][string[]]$Tables,
        [Parameter(Mandatory = $true)][string]$StageName
    )

    $notEmpty = @($TargetFacts | Where-Object { $_.Table -in $Tables -and $_.Rows -ne 0 })
    if ($notEmpty.Count -gt 0) {
        $details = (@($notEmpty | ForEach-Object { "$($_.Table)=$($_.Rows)" }) -join ', ')
        throw "$StageName did not empty: $details."
    }
}

function Invoke-MedRecProRefreshWorker {
    <#
    .SYNOPSIS
    Calls one legacy worker through its strict automation-safe contract.
    #>
    param(
        [Parameter(Mandatory = $true)][string]$WorkerPath,
        [Parameter(Mandatory = $true)][ValidateSet('Core', 'OrangeBook', 'TempTables', 'AdverseEvents')][string]$Domain,
        [Parameter(Mandatory = $true)][ValidateSet('Export', 'Import')][string]$Operation,
        [Parameter(Mandatory = $true)][string]$DataPath,
        [Parameter(Mandatory = $true)][string]$LocalServer,
        [Parameter(Mandatory = $true)][string]$LocalDatabase,
        [string]$AzureServer,
        [string]$AzureDatabase,
        [string]$AzureUser,
        [Security.SecureString]$AzurePassword,
        [int]$BatchSize = 50000,
        [int]$ParallelThrottle = 3,
        [string[]]$ExcludeTable,
        [object[]]$ExpectedInventory,
        [switch]$OverwriteData,
        [switch]$SkipTargetTruncate
    )

    $parameters = @{
        Operation = $Operation
        LocalServer = $LocalServer
        LocalDatabase = $LocalDatabase
        AzureServer = $AzureServer
        AzureDatabase = $AzureDatabase
        AzureUser = $AzureUser
        AzureSecurePassword = $AzurePassword
        DataPath = $DataPath
        BatchSize = $BatchSize
        ParallelThrottle = $ParallelThrottle
        ExcludeTable = $ExcludeTable
        ExpectedInventory = $ExpectedInventory
        AutomationMode = $true
        Strict = $true
        NonInteractive = $true
        OverwriteData = $OverwriteData
        SkipTargetTruncate = $SkipTargetTruncate
        StructuredResult = $true
        SuppressStrictThrow = $true
    }
    $result = & $WorkerPath @parameters
    if (@($result).Count -ne 1 -or -not $result.Success) {
        $failed = if ($result) { $result.FailedTables -join ', ' } else { 'no structured worker result' }
        throw "$Domain $Operation worker failed: $failed"
    }
    return $result
}

function Test-MedRecProRefreshManifestFiles {
    <#
    .SYNOPSIS
    Verifies every manifest-bound export file and SHA-256 hash before import or resume.
    #>
    param([Parameter(Mandatory = $true)][object]$Manifest)

    $domains = if ($Manifest.Domains -is [System.Collections.IDictionary]) { @($Manifest.Domains.Values) } else { @($Manifest.Domains.PSObject.Properties | ForEach-Object { $_.Value }) }
    foreach ($domain in $domains) {
        if (-not $domain.Export -or $domain.Export.Success -ne $true) { continue }
        foreach ($file in @($domain.Export.FileInventory)) {
            if (-not (Test-Path -LiteralPath $file.FilePath -PathType Leaf)) {
                throw "Manifest data file is missing: $($file.FilePath)"
            }
            $actualHash = (Get-FileHash -LiteralPath $file.FilePath -Algorithm SHA256).Hash
            if ($actualHash -ne $file.Hash) {
                throw "Manifest data file hash changed: $($file.FilePath)"
            }
        }
    }
}

function Get-MedRecProRefreshStageStatus {
    <#
    .SYNOPSIS
    Gets a manifest stage status or Pending when it has never been recorded.
    #>
    param([Parameter(Mandatory = $true)][object]$Manifest, [Parameter(Mandatory = $true)][string]$StageId)

    if ($Manifest.Stages -is [System.Collections.IDictionary]) {
        if ($Manifest.Stages.Contains($StageId)) { return $Manifest.Stages[$StageId].Status }
    }
    elseif ($Manifest.Stages.PSObject.Properties.Name -contains $StageId) {
        return $Manifest.Stages.$StageId.Status
    }
    return 'Pending'
}

function Invoke-MedRecProRefreshScopedCleanup {
    <#
    .SYNOPSIS
    Clears only a failed temp or adverse-event domain during a manifest-bound resume.
    #>
    param(
        [Parameter(Mandatory = $true)][string[]]$Tables,
        [Parameter(Mandatory = $true)][string]$AzureServer,
        [Parameter(Mandatory = $true)][string]$AzureDatabase,
        [Parameter(Mandatory = $true)][string]$AzureUser,
        [Parameter(Mandatory = $true)][Security.SecureString]$AzurePassword
    )

    foreach ($table in $Tables) {
        $safeTable = $table.Replace(']', ']]')
        $result = Invoke-MedRecProSqlCmd -Server $AzureServer -Database $AzureDatabase -User $AzureUser -Password $AzurePassword -Query "TRUNCATE TABLE [dbo].[$safeTable];"
        if (-not $result.Success) {
            throw "Scoped cleanup failed for ${table}: $($result.Output)"
        }
    }
}

#endregion implementation

if ($MyInvocation.InvocationName -eq '.') { return }

$ErrorActionPreference = 'Stop'
$assets = $null
$manifest = $null
$manifestPath = $null
$logPath = $null
$indexDisableSucceeded = $false
$rebuildVerified = $false
$exitCode = 99

try {
    if ($ExportOnly -and $ValidateOnly) { throw 'ExportOnly and ValidateOnly cannot be used together.' }
    if ($ResumeRun -and ($ExportOnly -or $ValidateOnly)) { throw 'ResumeRun cannot be combined with ExportOnly or ValidateOnly.' }

    if ($WhatIfPreference) {
        Write-Host "WhatIf: target '$AzureServer/$AzureDatabase'; source '$LocalServer/$LocalDatabase'; data root '$DataRoot'."
        Get-MedRecProRefreshStageCatalog | ForEach-Object { Write-Host ("WhatIf: {0} ({1})" -f $_.Name, $(if ($_.Destructive) { 'destructive after confirmation' } else { 'non-destructive' })) }
        return
    }

    $assets = Get-MedRecProRefreshAssets -ScriptRoot $PSScriptRoot
    Import-Module $assets.WorkerModule -Force -ErrorAction Stop

    if ($ResumeRun) {
        $runDirectory = (Resolve-Path -LiteralPath $ResumeRun -ErrorAction Stop).Path
        $manifestPath = Join-Path $runDirectory 'refresh-manifest.json'
        $logPath = Join-Path $runDirectory 'refresh.log'
        if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) { throw "Resume manifest was not found: $manifestPath" }
        $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
        Test-MedRecProRefreshAssetHashes -Manifest $manifest
        if ($AzureServer -and $AzureServer -ne $manifest.Target.Server) { throw 'Resume target server differs from the manifest.' }
        if ($AzureDatabase -and $AzureDatabase -ne $manifest.Target.Database) { throw 'Resume target database differs from the manifest.' }
        if ($PSBoundParameters.ContainsKey('LocalServer') -and $LocalServer -ne $manifest.Source.Server) { throw 'Resume source server differs from the manifest.' }
        if ($PSBoundParameters.ContainsKey('LocalDatabase') -and $LocalDatabase -ne $manifest.Source.Database) { throw 'Resume source database differs from the manifest.' }
        if ($PSBoundParameters.ContainsKey('RefreshExclusionRules') -and [bool]$RefreshExclusionRules -ne [bool]$manifest.Options.RefreshExclusionRules) { throw 'Resume exclusion-rule option differs from the manifest.' }
        if ($PSBoundParameters.ContainsKey('SkipIndexReconciliation') -and [bool]$SkipIndexReconciliation -ne [bool]$manifest.Options.SkipIndexReconciliation) { throw 'Resume index-reconciliation option differs from the manifest.' }
        $AzureServer = $manifest.Target.Server
        $AzureDatabase = $manifest.Target.Database
        $LocalServer = $manifest.Source.Server
        $LocalDatabase = $manifest.Source.Database
        $RefreshExclusionRules = [bool]$manifest.Options.RefreshExclusionRules
        $SkipIndexReconciliation = [bool]$manifest.Options.SkipIndexReconciliation
        Test-MedRecProRefreshManifestFiles -Manifest $manifest
        Write-MedRecProRefreshLog -LogPath $logPath -Message "Resuming manifest $($manifest.RunId)."
    }
    else {
        $runId = '{0:yyyyMMdd}-{1}' -f (Get-Date), ([Guid]::NewGuid().ToString('N').Substring(0, 8))
        $runDirectory = Join-Path $DataRoot $runId
        foreach ($directory in @($runDirectory, (Join-Path $runDirectory 'Core'), (Join-Path $runDirectory 'OrangeBook'), (Join-Path $runDirectory 'TempTables'), (Join-Path $runDirectory 'AdverseEvents'), (Join-Path $runDirectory 'Sql'))) {
            New-Item -ItemType Directory -Path $directory -Force | Out-Null
        }
        $manifestPath = Join-Path $runDirectory 'refresh-manifest.json'
        $logPath = Join-Path $runDirectory 'refresh.log'
        $manifest = [PSCustomObject]@{
            Version = 1
            RunId = $runId
            Status = 'Pending'
            CreatedAt = (Get-Date).ToUniversalTime().ToString('o')
            UpdatedAt = (Get-Date).ToUniversalTime().ToString('o')
            Source = [PSCustomObject]@{ Server = $LocalServer; Database = $LocalDatabase }
            Target = [PSCustomObject]@{ Server = $AzureServer; Database = $AzureDatabase; ConnectedServer = $null }
            Options = [PSCustomObject]@{ BatchSize = $BatchSize; ParallelThrottle = $ParallelThrottle; RefreshExclusionRules = [bool]$RefreshExclusionRules; SkipIndexReconciliation = [bool]$SkipIndexReconciliation }
            AssetHashes = Get-MedRecProRefreshAssetHashes -Assets $assets
            Stages = [ordered]@{}
            Domains = [ordered]@{}
            Preflight = $null
            Preservation = $null
            Failure = $null
        }
        Write-MedRecProRefreshLog -LogPath $logPath -Message "Created refresh run $runId."
    }

    if (-not $ExportOnly -and (-not $AzureServer -or -not $AzureDatabase -or -not $AzureUser)) {
        throw 'AzureServer, AzureDatabase, and AzureUser are required for validation, refresh, and resume.'
    }
    if (-not $ExportOnly -and -not $AzurePassword) {
        $AzurePassword = Read-Host "Enter Azure SQL password for $AzureServer/$AzureDatabase" -AsSecureString
    }

    $exitCode = 1
    Set-MedRecProRefreshStage -Manifest $manifest -StageId 'Preflight' -Status Running -Message 'Validating tools, identities, schemas, permissions, and nuke coverage.'
    Write-MedRecProRefreshManifest -Manifest $manifest -ManifestPath $manifestPath
    if (-not (Get-Command bcp -ErrorAction SilentlyContinue)) { throw 'Preflight failed: bcp was not found in PATH.' }
    if (-not (Get-Command sqlcmd -ErrorAction SilentlyContinue)) { throw 'Preflight failed: sqlcmd was not found in PATH.' }
    if ($PSVersionTable.PSVersion.Major -lt 5) { throw 'Preflight failed: PowerShell 5.1 or later is required.' }
    if (-not $ExportOnly -and ($AzureServer -notmatch '\.database\.windows\.net$' -or $AzureServer -match '^(localhost|\.|\(local\))$')) { throw 'Preflight refused a non-Azure or local AzureServer value.' }
    if (-not $ExportOnly -and $AzureServer.Trim().ToLowerInvariant() -eq $LocalServer.Trim().ToLowerInvariant() -and $AzureDatabase.Trim().ToLowerInvariant() -eq $LocalDatabase.Trim().ToLowerInvariant()) { throw 'Preflight refused identical source and target identities.' }

    $localIdentity = Get-MedRecProRefreshScalar -SqlResult (Invoke-MedRecProSqlCmd -Server $LocalServer -Database $LocalDatabase -Query 'SET NOCOUNT ON; SELECT DB_NAME();')
    if ($localIdentity -ne $LocalDatabase) { throw "Local database identity mismatch: expected $LocalDatabase, connected to $localIdentity." }
    $sourceFacts = Get-MedRecProRefreshTableFacts -Server $LocalServer -Database $LocalDatabase

    $definitions = @(
        [PSCustomObject]@{ Domain = 'Core'; Worker = $assets.CoreWorker; Directory = Join-Path $runDirectory 'Core'; Exclude = if ($RefreshExclusionRules) { @() } else { @('PharmClassDosageFormExclusion') } },
        [PSCustomObject]@{ Domain = 'OrangeBook'; Worker = $assets.OrangeBookWorker; Directory = Join-Path $runDirectory 'OrangeBook'; Exclude = @() },
        [PSCustomObject]@{ Domain = 'TempTables'; Worker = $assets.TempTablesWorker; Directory = Join-Path $runDirectory 'TempTables'; Exclude = @() },
        [PSCustomObject]@{ Domain = 'AdverseEvents'; Worker = $assets.AdverseEventsWorker; Directory = Join-Path $runDirectory 'AdverseEvents'; Exclude = @() }
    )
    foreach ($definition in $definitions) {
        $configuration = Get-MedRecProDomainConfiguration -Domain $definition.Domain
        $definition | Add-Member -MemberType NoteProperty -Name Tables -Value @($configuration.Tables | Where-Object { $definition.Exclude -notcontains $_ })
    }
    $allSelectedTables = @($definitions | ForEach-Object { $_.Tables })
    $missingSource = @($allSelectedTables | Where-Object { $_ -notin $sourceFacts.Table })
    if ($missingSource.Count -gt 0) { throw "Preflight source tables are missing: $($missingSource -join ', ')." }

    $targetFacts = @()
    $serviceObjective = $null
    if (-not $ExportOnly) {
        $targetIdentity = Get-MedRecProRefreshScalar -SqlResult (Invoke-MedRecProSqlCmd -Server $AzureServer -Database $AzureDatabase -User $AzureUser -Password $AzurePassword -Query "SET NOCOUNT ON; SELECT CONVERT(nvarchar(128), SERVERPROPERTY('ServerName')) + '|' + DB_NAME();" -RetryServerless)
        $identityParts = $targetIdentity.Split('|')
        if ($identityParts.Count -ne 2 -or $identityParts[1] -ne $AzureDatabase) { throw "Azure target identity mismatch: $targetIdentity" }
        $requestedLogicalServer = $AzureServer.Split('.')[0]
        if ($identityParts[0] -and $identityParts[0] -ne $requestedLogicalServer) { throw "Azure server identity mismatch: requested $requestedLogicalServer, connected to $($identityParts[0])." }
        $manifest.Target.ConnectedServer = $identityParts[0]
        # The service objective is informational for rebuild planning; the rebuild script's
        # online-first/offline-fallback behavior remains authoritative regardless of tier.
        $serviceObjective = Get-MedRecProRefreshScalar -SqlResult (Invoke-MedRecProSqlCmd -Server $AzureServer -Database $AzureDatabase -User $AzureUser -Password $AzurePassword -Query "SET NOCOUNT ON; SELECT ISNULL(CONVERT(nvarchar(128), DATABASEPROPERTYEX(DB_NAME(), 'ServiceObjective')), 'Unknown');")
        $targetFacts = Get-MedRecProRefreshTableFacts -Server $AzureServer -Database $AzureDatabase -User $AzureUser -Password $AzurePassword -RetryServerless
        $missingTarget = @($allSelectedTables | Where-Object { $_ -notin $targetFacts.Table })
        if ($missingTarget.Count -gt 0) {
            throw "Preflight target tables are missing: $($missingTarget -join ', '). Use MedRecPro-TableCreate-OrangeBook.sql for Orange Book, MedRecPro-Table-tmp_*.sql for dashboard/AE tables, or usp_RefreshTempTables in MedRecPro_Batch.sql; schema creation is intentionally out of scope."
        }
        $sourceSignature = Get-MedRecProRefreshSchemaSignature -Server $LocalServer -Database $LocalDatabase -Tables $allSelectedTables
        $targetSignature = Get-MedRecProRefreshSchemaSignature -Server $AzureServer -Database $AzureDatabase -Tables $allSelectedTables -User $AzureUser -Password $AzurePassword -RetryServerless
        if (Compare-Object -ReferenceObject $sourceSignature -DifferenceObject $targetSignature) { throw 'Preflight failed: BCP-relevant source and target schema signatures differ.' }
        $managedPrimary = @($definitions | Where-Object { $_.Domain -in @('Core', 'TempTables', 'AdverseEvents') } | ForEach-Object { $_.Tables })
        $managedOrangeBook = @($definitions | Where-Object { $_.Domain -eq 'OrangeBook' } | ForEach-Object { $_.Tables })
        Test-MedRecProRefreshNukeReconciliation -TargetFacts $targetFacts -ManagedPrimaryTables $managedPrimary -ManagedOrangeBookTables $managedOrangeBook -RefreshExclusionRules:$RefreshExclusionRules
        $permissionQuery = "SET NOCOUNT ON; SELECT CONVERT(varchar(1), HAS_PERMS_BY_NAME(DB_NAME(), 'DATABASE', 'ALTER')) + '|' + CONVERT(varchar(1), HAS_PERMS_BY_NAME(DB_NAME(), 'DATABASE', 'INSERT')) + '|' + CONVERT(varchar(1), HAS_PERMS_BY_NAME(DB_NAME(), 'DATABASE', 'SELECT'));"
        $permissionValues = Get-MedRecProRefreshScalar -SqlResult (Invoke-MedRecProSqlCmd -Server $AzureServer -Database $AzureDatabase -User $AzureUser -Password $AzurePassword -Query $permissionQuery)
        if ($permissionValues -match '0') { throw "Preflight target database permission check failed (ALTER, INSERT, and SELECT are required): $permissionValues" }
        $exclusionFact = @($targetFacts | Where-Object { $_.Table -eq 'PharmClassDosageFormExclusion' } | Select-Object -First 1)
        $manifest.Preservation = [PSCustomObject]@{ ExclusionRules = [PSCustomObject]@{ Refresh = [bool]$RefreshExclusionRules; BeforeRows = if ($exclusionFact) { $exclusionFact[0].Rows } else { $null }; AfterRows = $null } }
    }

    $manifest.Preflight = [PSCustomObject]@{ SourceTableCount = $sourceFacts.Count; TargetTableCount = $targetFacts.Count; SelectedTableCount = $allSelectedTables.Count; TargetIdentityVerified = (-not $ExportOnly); ServerlessWarmupAttempted = (-not $ExportOnly); ServiceObjective = $serviceObjective }
    Set-MedRecProRefreshStage -Manifest $manifest -StageId 'Preflight' -Status Succeeded -Message 'All required preflight checks passed.'
    $manifest.Status = if ($ValidateOnly) { 'Validated' } else { 'Running' }
    Write-MedRecProRefreshManifest -Manifest $manifest -ManifestPath $manifestPath

    if ($ValidateOnly) {
        Write-MedRecProRefreshLog -LogPath $logPath -Message "Validation succeeded for $AzureServer/$AzureDatabase without export or Azure mutation."
        $exitCode = 0
        return
    }

    $exitCode = 3
    foreach ($definition in $definitions) {
        $stageId = "Export$($definition.Domain)"
        if ($ResumeRun -and (Get-MedRecProRefreshStageStatus -Manifest $manifest -StageId $stageId) -eq 'Succeeded') { continue }
        Set-MedRecProRefreshStage -Manifest $manifest -StageId $stageId -Status Running -Message "Exporting $($definition.Domain)."
        Write-MedRecProRefreshManifest -Manifest $manifest -ManifestPath $manifestPath
        $export = Invoke-MedRecProRefreshWorker -WorkerPath $definition.Worker -Domain $definition.Domain -Operation Export -DataPath $definition.Directory -LocalServer $LocalServer -LocalDatabase $LocalDatabase -BatchSize $BatchSize -ParallelThrottle $ParallelThrottle -ExcludeTable $definition.Exclude -OverwriteData
        Set-MedRecProRefreshMember -Container $manifest.Domains -Name $definition.Domain -Value ([PSCustomObject]@{ Tables = $definition.Tables; Export = $export; Import = $null })
        Set-MedRecProRefreshStage -Manifest $manifest -StageId $stageId -Status Succeeded -Message "$($definition.Domain) export inventory and hashes recorded."
        Write-MedRecProRefreshManifest -Manifest $manifest -ManifestPath $manifestPath
    }
    Test-MedRecProRefreshManifestFiles -Manifest $manifest

    if ($ExportOnly) {
        $manifest.Status = 'ExportComplete'
        Write-MedRecProRefreshManifest -Manifest $manifest -ManifestPath $manifestPath
        Write-MedRecProRefreshLog -LogPath $logPath -Message "Export-only snapshot is complete: $runDirectory"
        $exitCode = 0
        return
    }

    $confirmationToken = "REFRESH $AzureServer/$AzureDatabase"
    Write-Host "Run: $($manifest.RunId)"; Write-Host "Source: $LocalServer/$LocalDatabase"; Write-Host "Target: $AzureServer/$AzureDatabase"; Write-Host "Selected tables: $($allSelectedTables.Count)"; Write-Host "Exclusion rules: $(if ($RefreshExclusionRules) { 'will be refreshed' } else { 'preserved' })"
    $confirmation = Read-Host "Type '$confirmationToken' to authorize the complete destructive sequence"
    if (-not (Test-MedRecProRefreshConfirmation -Confirmation $confirmation -AzureServer $AzureServer -AzureDatabase $AzureDatabase)) { throw [OperationCanceledException]::new('Target confirmation did not match.') }
    if (-not $PSCmdlet.ShouldProcess("$AzureServer/$AzureDatabase", 'perform the confirmed MedRecPro Azure data refresh')) { throw [OperationCanceledException]::new('ShouldProcess declined the refresh.') }
    Set-MedRecProRefreshStage -Manifest $manifest -StageId 'ConfirmTarget' -Status Succeeded -Message 'Exact target-specific confirmation received.'
    Write-MedRecProRefreshManifest -Manifest $manifest -ManifestPath $manifestPath

    $hasIncompleteImports = @('ImportCore', 'ImportOrangeBook', 'ImportTempTables', 'ImportAdverseEvents' | ForEach-Object { (Get-MedRecProRefreshStageStatus -Manifest $manifest -StageId $_) -ne 'Succeeded' }) -contains $true
    $exitCode = 4
    if ($hasIncompleteImports) {
        Set-MedRecProRefreshStage -Manifest $manifest -StageId 'DisableIndexes' -Status Running -Message 'Disabling eligible nonclustered indexes.'
        Write-MedRecProRefreshManifest -Manifest $manifest -ManifestPath $manifestPath
        Invoke-MedRecProRefreshSqlFile -ScriptPath $assets.DisableIndexes -AzureServer $AzureServer -AzureDatabase $AzureDatabase -AzureUser $AzureUser -AzurePassword $AzurePassword -LogPath $logPath -QueryTimeoutSeconds 0
        $enabledEligible = [long](Get-MedRecProRefreshScalar -SqlResult (Invoke-MedRecProSqlCmd -Server $AzureServer -Database $AzureDatabase -User $AzureUser -Password $AzurePassword -Query "SET NOCOUNT ON; SELECT COUNT_BIG(*) FROM sys.indexes i INNER JOIN sys.tables t ON t.object_id=i.object_id INNER JOIN sys.schemas s ON s.schema_id=t.schema_id WHERE s.name='dbo' AND i.type_desc='NONCLUSTERED' AND i.name IS NOT NULL AND i.is_primary_key=0 AND i.is_unique_constraint=0 AND i.is_disabled=0;"))
        if ($enabledEligible -ne 0) { throw "Index-disable postcondition failed: $enabledEligible eligible indexes remain enabled." }
        $indexDisableSucceeded = $true
        Set-MedRecProRefreshStage -Manifest $manifest -StageId 'DisableIndexes' -Status Succeeded -Message 'No eligible nonclustered index remains enabled.'
        Write-MedRecProRefreshManifest -Manifest $manifest -ManifestPath $manifestPath
    }

    $exitCode = 5
    $primaryDefinitions = @($definitions | Where-Object { $_.Domain -in @('Core', 'TempTables', 'AdverseEvents') })
    $primaryTargets = @($primaryDefinitions | ForEach-Object { $_.Tables })
    if ((Get-MedRecProRefreshStageStatus -Manifest $manifest -StageId 'ImportCore') -ne 'Succeeded') {
        $exitCode = 4
        Set-MedRecProRefreshStage -Manifest $manifest -StageId 'ClearPrimary' -Status Running -Message 'Generating and executing the primary nuke copy.'
        $primaryNukeCopy = Join-Path $runDirectory 'Sql\MedRecPro-AzureNuke.execute.sql'
        New-MedRecProRefreshExecutableNuke -SourcePath $assets.PrimaryNuke -DestinationPath $primaryNukeCopy
        Invoke-MedRecProRefreshSqlFile -ScriptPath $primaryNukeCopy -AzureServer $AzureServer -AzureDatabase $AzureDatabase -AzureUser $AzureUser -AzurePassword $AzurePassword -LogPath $logPath -QueryTimeoutSeconds 0
        if ($RefreshExclusionRules) {
            # The generated nuke always preserves the exclusion-rules table, so the opted-in
            # scoped clear must run before the emptiness postcondition that now includes it.
            Invoke-MedRecProRefreshScopedCleanup -Tables @('PharmClassDosageFormExclusion') -AzureServer $AzureServer -AzureDatabase $AzureDatabase -AzureUser $AzureUser -AzurePassword $AzurePassword
        }
        $targetFacts = Get-MedRecProRefreshTableFacts -Server $AzureServer -Database $AzureDatabase -User $AzureUser -Password $AzurePassword
        Test-MedRecProRefreshTablesEmpty -TargetFacts $targetFacts -Tables $primaryTargets -StageName 'Primary cleanup'
        Set-MedRecProRefreshStage -Manifest $manifest -StageId 'ClearPrimary' -Status Succeeded -Message 'Primary, temp, and adverse-event cleanup postcondition passed.'
        $exitCode = 5
        Set-MedRecProRefreshStage -Manifest $manifest -StageId 'ImportCore' -Status Running -Message 'Importing Core from verified manifest files.'
        $core = @($definitions | Where-Object { $_.Domain -eq 'Core' })[0]
        $coreImport = Invoke-MedRecProRefreshWorker -WorkerPath $core.Worker -Domain Core -Operation Import -DataPath $core.Directory -LocalServer $LocalServer -LocalDatabase $LocalDatabase -AzureServer $AzureServer -AzureDatabase $AzureDatabase -AzureUser $AzureUser -AzurePassword $AzurePassword -BatchSize $BatchSize -ParallelThrottle $ParallelThrottle -ExcludeTable $core.Exclude -ExpectedInventory $manifest.Domains.Core.Export.FileInventory -SkipTargetTruncate
        $manifest.Domains.Core.Import = $coreImport
        Set-MedRecProRefreshStage -Manifest $manifest -StageId 'ImportCore' -Status Succeeded -Message 'Core target counts match the export manifest.'
        Write-MedRecProRefreshManifest -Manifest $manifest -ManifestPath $manifestPath
    }

    if ((Get-MedRecProRefreshStageStatus -Manifest $manifest -StageId 'ImportOrangeBook') -ne 'Succeeded') {
        $exitCode = 4
        Set-MedRecProRefreshStage -Manifest $manifest -StageId 'ClearOrangeBook' -Status Running -Message 'Generating and executing the Orange Book nuke copy.'
        $orangeNukeCopy = Join-Path $runDirectory 'Sql\MedRecPro-AzureOrangeBookNuke.execute.sql'
        New-MedRecProRefreshExecutableNuke -SourcePath $assets.OrangeBookNuke -DestinationPath $orangeNukeCopy
        Invoke-MedRecProRefreshSqlFile -ScriptPath $orangeNukeCopy -AzureServer $AzureServer -AzureDatabase $AzureDatabase -AzureUser $AzureUser -AzurePassword $AzurePassword -LogPath $logPath -QueryTimeoutSeconds 0
        $targetFacts = Get-MedRecProRefreshTableFacts -Server $AzureServer -Database $AzureDatabase -User $AzureUser -Password $AzurePassword
        $orange = @($definitions | Where-Object { $_.Domain -eq 'OrangeBook' })[0]
        Test-MedRecProRefreshTablesEmpty -TargetFacts $targetFacts -Tables $orange.Tables -StageName 'Orange Book cleanup'
        Set-MedRecProRefreshStage -Manifest $manifest -StageId 'ClearOrangeBook' -Status Succeeded -Message 'Orange Book cleanup postcondition passed.'
        $exitCode = 5
        Set-MedRecProRefreshStage -Manifest $manifest -StageId 'ImportOrangeBook' -Status Running -Message 'Importing OrangeBook from verified manifest files.'
        $orangeImport = Invoke-MedRecProRefreshWorker -WorkerPath $orange.Worker -Domain OrangeBook -Operation Import -DataPath $orange.Directory -LocalServer $LocalServer -LocalDatabase $LocalDatabase -AzureServer $AzureServer -AzureDatabase $AzureDatabase -AzureUser $AzureUser -AzurePassword $AzurePassword -BatchSize $BatchSize -ParallelThrottle $ParallelThrottle -ExpectedInventory $manifest.Domains.OrangeBook.Export.FileInventory -SkipTargetTruncate
        $manifest.Domains.OrangeBook.Import = $orangeImport
        Set-MedRecProRefreshStage -Manifest $manifest -StageId 'ImportOrangeBook' -Status Succeeded -Message 'Orange Book target counts match the export manifest.'
        Write-MedRecProRefreshManifest -Manifest $manifest -ManifestPath $manifestPath
    }

    foreach ($domainName in @('TempTables', 'AdverseEvents')) {
        $stageId = "Import$domainName"
        if ((Get-MedRecProRefreshStageStatus -Manifest $manifest -StageId $stageId) -eq 'Succeeded') { continue }
        $definition = @($definitions | Where-Object { $_.Domain -eq $domainName })[0]
        $exitCode = 4
        Set-MedRecProRefreshStage -Manifest $manifest -StageId $stageId -Status Running -Message "Scoped cleanup and import for $domainName."
        Invoke-MedRecProRefreshScopedCleanup -Tables $definition.Tables -AzureServer $AzureServer -AzureDatabase $AzureDatabase -AzureUser $AzureUser -AzurePassword $AzurePassword
        $exitCode = 5
        $import = Invoke-MedRecProRefreshWorker -WorkerPath $definition.Worker -Domain $domainName -Operation Import -DataPath $definition.Directory -LocalServer $LocalServer -LocalDatabase $LocalDatabase -AzureServer $AzureServer -AzureDatabase $AzureDatabase -AzureUser $AzureUser -AzurePassword $AzurePassword -BatchSize $BatchSize -ParallelThrottle $ParallelThrottle -ExpectedInventory $manifest.Domains.$domainName.Export.FileInventory -SkipTargetTruncate
        $manifest.Domains.$domainName.Import = $import
        Set-MedRecProRefreshStage -Manifest $manifest -StageId $stageId -Status Succeeded -Message "$domainName target counts match the export manifest."
        Write-MedRecProRefreshManifest -Manifest $manifest -ManifestPath $manifestPath
    }

    $exitCode = 6
    Set-MedRecProRefreshStage -Manifest $manifest -StageId 'RebuildIndexes' -Status Running -Message 'Rebuilding indexes and updating statistics without a query timeout.'
    Write-MedRecProRefreshManifest -Manifest $manifest -ManifestPath $manifestPath
    Invoke-MedRecProRefreshSqlFile -ScriptPath $assets.RebuildIndexes -AzureServer $AzureServer -AzureDatabase $AzureDatabase -AzureUser $AzureUser -AzurePassword $AzurePassword -LogPath $logPath -QueryTimeoutSeconds 0
    Test-MedRecProRefreshNoDisabledIndexes -AzureServer $AzureServer -AzureDatabase $AzureDatabase -AzureUser $AzureUser -AzurePassword $AzurePassword
    $rebuildVerified = $true
    Set-MedRecProRefreshStage -Manifest $manifest -StageId 'RebuildIndexes' -Status Succeeded -Message 'No disabled named dbo index remains.'

    if ($SkipIndexReconciliation) {
        Set-MedRecProRefreshStage -Manifest $manifest -StageId 'ReconcileIndexes' -Status Skipped -Message 'Skipped by explicit time-sensitive recovery option.'
    }
    else {
        Set-MedRecProRefreshStage -Manifest $manifest -StageId 'ReconcileIndexes' -Status Running -Message 'Running canonical index-definition script.'
        Invoke-MedRecProRefreshSqlFile -ScriptPath $assets.ReconcileIndexes -AzureServer $AzureServer -AzureDatabase $AzureDatabase -AzureUser $AzureUser -AzurePassword $AzurePassword -LogPath $logPath -QueryTimeoutSeconds 0
        Test-MedRecProRefreshNoDisabledIndexes -AzureServer $AzureServer -AzureDatabase $AzureDatabase -AzureUser $AzureUser -AzurePassword $AzurePassword
        Set-MedRecProRefreshStage -Manifest $manifest -StageId 'ReconcileIndexes' -Status Succeeded -Message 'Canonical index-definition script completed and index-state check passed.'
    }

    $exitCode = 7
    Set-MedRecProRefreshStage -Manifest $manifest -StageId 'FinalVerification' -Status Running -Message 'Verifying manifest row counts, index state, and preservation policy.'
    foreach ($definition in $definitions) {
        $expected = @($manifest.Domains.$($definition.Domain).Export.FileInventory)
        foreach ($table in $expected) {
            $targetCount = Get-MedRecProSqlRowCount -Server $AzureServer -Database $AzureDatabase -Table $table.Table -User $AzureUser -Password $AzurePassword
            if (-not $targetCount.Success -or $targetCount.Count -ne [long]$table.SourceRows) { throw "Final verification failed for $($table.Table)." }
        }
    }
    if (-not $RefreshExclusionRules) {
        $postExclusion = Get-MedRecProSqlRowCount -Server $AzureServer -Database $AzureDatabase -Table 'PharmClassDosageFormExclusion' -User $AzureUser -Password $AzurePassword
        if (-not $postExclusion.Success -or $postExclusion.Count -ne [long]$manifest.Preservation.ExclusionRules.BeforeRows) { throw 'Preserved exclusion-rules row count changed.' }
        $manifest.Preservation.ExclusionRules.AfterRows = $postExclusion.Count
    }
    Test-MedRecProRefreshNoDisabledIndexes -AzureServer $AzureServer -AzureDatabase $AzureDatabase -AzureUser $AzureUser -AzurePassword $AzurePassword
    Set-MedRecProRefreshStage -Manifest $manifest -StageId 'FinalVerification' -Status Succeeded -Message 'Row counts, index state, and preservation policy passed.'
    $manifest.Status = 'Complete'
    Write-MedRecProRefreshManifest -Manifest $manifest -ManifestPath $manifestPath
    Write-MedRecProRefreshLog -LogPath $logPath -Message "Refresh complete for $AzureServer/$AzureDatabase."
    $exitCode = 0
}
catch [OperationCanceledException] {
    $exitCode = 2
    if ($manifest) {
        $manifest.Status = 'Cancelled'
        $manifest.Failure = [PSCustomObject]@{ At = (Get-Date).ToUniversalTime().ToString('o'); Summary = $_.Exception.Message }
        if ($manifestPath) { Write-MedRecProRefreshManifest -Manifest $manifest -ManifestPath $manifestPath }
    }
    if ($logPath) { Write-MedRecProRefreshLog -LogPath $logPath -Message "Refresh cancelled: $($_.Exception.Message)" }
    # Continue keeps this error record non-terminating under the script's Stop preference
    # so the deterministic exit statement below still runs.
    Write-Error $_.Exception.Message -ErrorAction Continue
}
catch {
    if ($exitCode -eq 99) { $exitCode = 1 }
    if ($manifest) {
        $manifest.Status = 'Failed'
        $manifest.Failure = [PSCustomObject]@{ At = (Get-Date).ToUniversalTime().ToString('o'); Summary = $_.Exception.Message }
        if ($manifestPath) { Write-MedRecProRefreshManifest -Manifest $manifest -ManifestPath $manifestPath }
    }
    if ($logPath) { Write-MedRecProRefreshLog -LogPath $logPath -Message "Refresh failed: $($_.Exception.Message)" }
    Write-Error $_.Exception.Message -ErrorAction Continue
}
finally {
    if ($indexDisableSucceeded -and -not $rebuildVerified -and $assets -and $AzurePassword) {
        try {
            if ($manifest) {
                $manifest.Status = 'RecoveryRequired'
                Set-MedRecProRefreshStage -Manifest $manifest -StageId 'RebuildIndexes' -Status RecoveryRequired -Message 'Attempting mandatory index recovery after incomplete refresh.'
                Write-MedRecProRefreshManifest -Manifest $manifest -ManifestPath $manifestPath
            }
            Invoke-MedRecProRefreshSqlFile -ScriptPath $assets.RebuildIndexes -AzureServer $AzureServer -AzureDatabase $AzureDatabase -AzureUser $AzureUser -AzurePassword $AzurePassword -LogPath $logPath -QueryTimeoutSeconds 0
            Test-MedRecProRefreshNoDisabledIndexes -AzureServer $AzureServer -AzureDatabase $AzureDatabase -AzureUser $AzureUser -AzurePassword $AzurePassword
            if ($manifest) {
                Set-MedRecProRefreshStage -Manifest $manifest -StageId 'RebuildIndexes' -Status Succeeded -Message 'Automatic recovery rebuilt all named dbo indexes; resume can continue data stages.'
                Write-MedRecProRefreshManifest -Manifest $manifest -ManifestPath $manifestPath
            }
        }
        catch {
            $exitCode = 6
            $recoveryMessage = "INDEX RECOVERY FAILED for $AzureServer/$AzureDatabase. Run $($assets.QueryEditorRecovery) in Azure Query Editor, then resume this run. $($_.Exception.Message)"
            if ($manifest) {
                $manifest.Status = 'RecoveryRequired'
                Set-MedRecProRefreshStage -Manifest $manifest -StageId 'RebuildIndexes' -Status RecoveryRequired -Message $recoveryMessage
                Write-MedRecProRefreshManifest -Manifest $manifest -ManifestPath $manifestPath
            }
            if ($logPath) { Write-MedRecProRefreshLog -LogPath $logPath -Message $recoveryMessage }
            Write-Error $recoveryMessage -ErrorAction Continue
        }
    }
}

exit $exitCode
