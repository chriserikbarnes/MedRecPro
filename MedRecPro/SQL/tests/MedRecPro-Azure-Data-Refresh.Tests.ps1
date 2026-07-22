$scriptPath = Join-Path $PSScriptRoot '..\MedRecPro-Azure-Data-Refresh.ps1'
$workerModulePath = Join-Path $PSScriptRoot '..\MedRecPro-DataRefreshWorker.psm1'
$sqlRoot = Split-Path -Parent $scriptPath

. $scriptPath
Import-Module $workerModulePath -Force

Describe 'MedRecPro unified Azure data refresh orchestration' {
    It 'keeps the complete stage graph in its required order' {
        $actual = (Get-MedRecProRefreshStageCatalog | ForEach-Object { $_.Id }) -join ','
        $expected = 'Preflight,ExportCore,ExportOrangeBook,ExportTempTables,ExportAdverseEvents,ConfirmTarget,DisableIndexes,ClearPrimary,ImportCore,ClearOrangeBook,ImportOrangeBook,ImportTempTables,ImportAdverseEvents,RebuildIndexes,ReconcileIndexes,FinalVerification'
        $actual | Should Be $expected
    }

    It 'requires the exact current target confirmation token' {
        (Test-MedRecProRefreshConfirmation -Confirmation 'REFRESH server.database.windows.net/MedRecPro' -AzureServer 'server.database.windows.net' -AzureDatabase 'MedRecPro') | Should Be $true
        (Test-MedRecProRefreshConfirmation -Confirmation 'REFRESH other.database.windows.net/MedRecPro' -AzureServer 'server.database.windows.net' -AzureDatabase 'MedRecPro') | Should Be $false
    }

    It 'preserves the legacy domain counts and BCP flag contracts' {
        @(Get-MedRecProDomainConfiguration Core).Tables.Count | Should Be 97
        @(Get-MedRecProDomainConfiguration OrangeBook).Tables.Count | Should Be 8
        @(Get-MedRecProDomainConfiguration TempTables).Tables.Count | Should Be 3
        @(Get-MedRecProDomainConfiguration AdverseEvents).Tables.Count | Should Be 5
        ((Get-MedRecProDomainConfiguration Core).ImportFlags -join ' ') | Should Be '-n -E'
        ((Get-MedRecProDomainConfiguration OrangeBook).ImportFlags -join ' ') | Should Be '-n -q -E'
        ((Get-MedRecProDomainConfiguration TempTables).ImportFlags -join ' ') | Should Be '-n'
        ((Get-MedRecProDomainConfiguration AdverseEvents).ExportFlags -join ' ') | Should Be '-n -q'
    }

    It 'fails preflight when a nuke would clear a populated unmanaged table' {
        $facts = @(
            [PSCustomObject]@{ Table = 'Product'; Rows = 10 },
            [PSCustomObject]@{ Table = 'temp_orangeBookpatent'; Rows = 1 },
            [PSCustomObject]@{ Table = 'AspNetUsers'; Rows = 2 }
        )
        { Test-MedRecProRefreshNukeReconciliation -TargetFacts $facts -ManagedPrimaryTables @('Product') -ManagedOrangeBookTables @('OrangeBookProduct') } | Should Throw
    }

    It 'allows preserved framework and exclusion-rule tables outside the selected workers' {
        $facts = @(
            [PSCustomObject]@{ Table = 'Product'; Rows = 10 },
            [PSCustomObject]@{ Table = 'AspNetUsers'; Rows = 2 },
            [PSCustomObject]@{ Table = 'PharmClassDosageFormExclusion'; Rows = 3 }
        )
        { Test-MedRecProRefreshNukeReconciliation -TargetFacts $facts -ManagedPrimaryTables @('Product') -ManagedOrangeBookTables @('OrangeBookProduct') } | Should Not Throw
    }

    It 'enables exactly one declaration only in generated nuke copies' {
        $source = Join-Path $sqlRoot 'MedRecPro-AzureNuke.sql'
        $destination = Join-Path $TestDrive 'MedRecPro-AzureNuke.execute.sql'
        New-MedRecProRefreshExecutableNuke -SourcePath $source -DestinationPath $destination
        (Get-Content -LiteralPath $source -Raw) | Should Match 'DECLARE @ExecuteCommands BIT = 0;'
        (Get-Content -LiteralPath $destination -Raw) | Should Match 'DECLARE @ExecuteCommands BIT = 1;'
        ([regex]::Matches((Get-Content -LiteralPath $destination -Raw), '(?m)^DECLARE @ExecuteCommands BIT = 1;').Count) | Should Be 1
    }

    It 'rejects a changed manifest-bound export file before import' {
        $dataFile = Join-Path $TestDrive 'Product.dat'
        [IO.File]::WriteAllText($dataFile, 'original')
        $manifest = [PSCustomObject]@{
            Domains = [PSCustomObject]@{
                Core = [PSCustomObject]@{
                    Export = [PSCustomObject]@{
                        Success = $true
                        FileInventory = @([PSCustomObject]@{ Table = 'Product'; FilePath = $dataFile; Hash = (Get-FileHash -LiteralPath $dataFile -Algorithm SHA256).Hash })
                    }
                }
            }
        }
        Add-Content -LiteralPath $dataFile -Value 'changed'
        { Test-MedRecProRefreshManifestFiles -Manifest $manifest } | Should Throw
    }

    It 'treats a SQL postcondition failure as a failed rebuild even when sqlcmd itself succeeded' {
        Mock Invoke-MedRecProSqlCmd { [PSCustomObject]@{ Success = $true; ExitCode = 0; Output = '1' } }
        { Test-MedRecProRefreshNoDisabledIndexes -AzureServer 'server.database.windows.net' -AzureDatabase 'MedRecPro' -AzureUser 'user' -AzurePassword (ConvertTo-SecureString 'x' -AsPlainText -Force) } | Should Throw
        Assert-MockCalled Invoke-MedRecProSqlCmd -Times 1 -Exactly
    }

    It 'surfaces every non-empty table after a cleanup postcondition' {
        $facts = @([PSCustomObject]@{ Table = 'Product'; Rows = 2 })
        { Test-MedRecProRefreshTablesEmpty -TargetFacts $facts -Tables @('Product') -StageName 'Primary cleanup' } | Should Throw
    }
    It 'redacts password-shaped text before adding it to the run log' {
        $logPath = Join-Path $TestDrive 'refresh.log'
        Write-MedRecProRefreshLog -LogPath $logPath -Message 'native output Password=not-for-log completed'
        (Get-Content -LiteralPath $logPath -Raw) | Should Not Match 'not-for-log'
        (Get-Content -LiteralPath $logPath -Raw) | Should Match 'Password=\*\*\*'
    }

    It 'keeps worker-level truncation suppressed for every unified import' {
        $scriptText = Get-Content -LiteralPath $scriptPath -Raw
        ([regex]::Matches($scriptText, '-SkipTargetTruncate').Count -ge 3) | Should Be $true
    }

    It 'uses an unlimited query timeout for rebuild and reconciliation' {
        $scriptText = Get-Content -LiteralPath $scriptPath -Raw
        ([regex]::Matches($scriptText, 'RebuildIndexes.*QueryTimeoutSeconds 0').Count -ge 1) | Should Be $true
        ([regex]::Matches($scriptText, 'ReconcileIndexes.*QueryTimeoutSeconds 0').Count -ge 1) | Should Be $true
    }

    It 'builds a complete export bcp command with the out direction resolved at runtime' {
        $global:MedRecProBcpArgs = $null
        function global:bcp {
            $global:MedRecProBcpArgs = @($args)
            Set-Content -LiteralPath $args[2] -Value 'native'
            cmd /c exit 0
            '5 rows copied'
        }
        try {
            $dataFile = Join-Path $TestDrive 'Product.dat'
            $result = Invoke-MedRecProBcp -Operation Export -Server 'localhost' -Database 'MedRecLocal' -Table 'Product' -DataFile $dataFile -FormatFlags @('-n')
            $result.Success | Should Be $true
            $result.RowsCopied | Should Be 5
            $global:MedRecProBcpArgs[0] | Should Be 'MedRecLocal.dbo.Product'
            $global:MedRecProBcpArgs[1] | Should Be 'out'
            ($global:MedRecProBcpArgs -contains '-T') | Should Be $true
            ($global:MedRecProBcpArgs -contains '-n') | Should Be $true
        }
        finally {
            Remove-Item function:\bcp -ErrorAction SilentlyContinue
            Remove-Variable -Name MedRecProBcpArgs -Scope Global -ErrorAction SilentlyContinue
        }
    }

    It 'builds a complete import bcp command with identity, quoting, and batching flags' {
        $global:MedRecProBcpArgs = $null
        function global:bcp {
            $global:MedRecProBcpArgs = @($args)
            cmd /c exit 0
            '3 rows copied'
        }
        try {
            $dataFile = Join-Path $TestDrive 'OrangeBookProduct.dat'
            Set-Content -LiteralPath $dataFile -Value 'native'
            $secure = ConvertTo-SecureString 'test-secret' -AsPlainText -Force
            $result = Invoke-MedRecProBcp -Operation Import -Server 'server.database.windows.net' -Database 'MedRecPro' -Table 'OrangeBookProduct' -DataFile $dataFile -FormatFlags @('-n', '-q', '-E') -BatchSize 1234 -User 'migration-user' -Password $secure
            $result.Success | Should Be $true
            $global:MedRecProBcpArgs[1] | Should Be 'in'
            ($global:MedRecProBcpArgs -contains '-E') | Should Be $true
            ($global:MedRecProBcpArgs -contains '-q') | Should Be $true
            ($global:MedRecProBcpArgs -contains 'TABLOCK') | Should Be $true
            ($global:MedRecProBcpArgs -contains '1234') | Should Be $true
        }
        finally {
            Remove-Item function:\bcp -ErrorAction SilentlyContinue
            Remove-Variable -Name MedRecProBcpArgs -Scope Global -ErrorAction SilentlyContinue
        }
    }

    It 'rejects a resume when a manifest-bound script asset changed after the run was created' {
        $asset = Join-Path $TestDrive 'Worker.ps1'
        Set-Content -LiteralPath $asset -Value 'original'
        $manifest = [PSCustomObject]@{
            AssetHashes = [PSCustomObject]@{
                Worker = [PSCustomObject]@{ Path = $asset; Hash = (Get-FileHash -LiteralPath $asset -Algorithm SHA256).Hash }
            }
        }
        Add-Content -LiteralPath $asset -Value 'tampered'
        { Test-MedRecProRefreshAssetHashes -Manifest $manifest } | Should Throw
    }

    It 'fails asset resolution when a required worker or SQL file is missing' {
        { Get-MedRecProRefreshAssets -ScriptRoot $TestDrive } | Should Throw
    }

    It 'writes terminal errors non-fatally so deterministic exit codes survive the Stop preference' {
        $scriptText = Get-Content -LiteralPath $scriptPath -Raw
        $writeErrors = [regex]::Matches($scriptText, 'Write-Error[^\r\n]*')
        ($writeErrors.Count -gt 0) | Should Be $true
        foreach ($writeError in $writeErrors) {
            ($writeError.Value -match '-ErrorAction\s+Continue') | Should Be $true
        }
    }

    It 'echoes every stage transition to the console as a timestamped banner' {
        $manifest = [PSCustomObject]@{ Stages = [ordered]@{} }
        $banner = Set-MedRecProRefreshStage -Manifest $manifest -StageId 'RebuildIndexes' -Status Running -Message 'Rebuilding.' 6>&1 | Out-String
        ($banner -match 'Rebuild indexes and statistics') | Should Be $true
        ($banner -match 'Running') | Should Be $true
        $manifest.Stages['RebuildIndexes'].Status | Should Be 'Running'
    }
}

# sqlcmd behavior runs in its own Describe because Pester 3 mocks live for the whole
# Describe that created them, and the orchestration block above mocks Invoke-MedRecProSqlCmd.
Describe 'MedRecPro worker module sqlcmd behavior' {
    It 'computes export row totals without strict-mode Measure-Object failures' {
        function global:bcp {
            Set-Content -LiteralPath $args[2] -Value 'native'
            cmd /c exit 0
            '10 rows copied'
        }
        Mock Get-MedRecProSqlRowCount { [PSCustomObject]@{ Success = $true; Count = [long]10; Error = $null } } -ModuleName MedRecPro-DataRefreshWorker
        try {
            $result = Invoke-MedRecProDomainWorker -Domain TempTables -Operation Export -DataPath (Join-Path $TestDrive 'TempExport') -Strict -NonInteractive -OverwriteData
            $result.Success | Should Be $true
            $result.RowTotals.Source | Should Be 30
            $result.RowTotals.Target | Should Be 0
        }
        finally {
            Remove-Item function:\bcp -ErrorAction SilentlyContinue
        }
    }

    It 'passes credentials to sqlcmd as -U and -P arguments like the legacy workers' {
        $global:MedRecProSqlcmdArgs = $null
        function global:sqlcmd {
            $global:MedRecProSqlcmdArgs = @($args)
            cmd /c exit 0
            '1'
        }
        try {
            $secure = ConvertTo-SecureString 'legacy-pattern' -AsPlainText -Force
            $result = Invoke-MedRecProSqlCmd -Server 'server.database.windows.net' -Database 'MedRecPro' -User 'migration-user' -Password $secure -Query 'SELECT 1;'
            $result.Success | Should Be $true
            $userIndex = [Array]::IndexOf($global:MedRecProSqlcmdArgs, '-U')
            ($userIndex -ge 0) | Should Be $true
            $global:MedRecProSqlcmdArgs[$userIndex + 1] | Should Be 'migration-user'
            $passwordIndex = [Array]::IndexOf($global:MedRecProSqlcmdArgs, '-P')
            ($passwordIndex -ge 0) | Should Be $true
            $global:MedRecProSqlcmdArgs[$passwordIndex + 1] | Should Be 'legacy-pattern'
        }
        finally {
            Remove-Item function:\sqlcmd -ErrorAction SilentlyContinue
            Remove-Variable -Name MedRecProSqlcmdArgs -Scope Global -ErrorAction SilentlyContinue
        }
    }

    It 'retries a transient serverless resume failure and then succeeds' {
        $global:MedRecProSqlcmdCalls = 0
        function global:sqlcmd {
            $global:MedRecProSqlcmdCalls++
            if ($global:MedRecProSqlcmdCalls -eq 1) {
                cmd /c exit 1
                'Sqlcmd: Error: Database on server is not currently available. Error code 40613.'
            }
            else {
                cmd /c exit 0
                '1'
            }
        }
        try {
            $result = Invoke-MedRecProSqlCmd -Server 'server.database.windows.net' -Database 'MedRecPro' -Query 'SELECT 1;' -RetryServerless -RetryDelaysSeconds @(0, 0, 0, 0)
            $result.Success | Should Be $true
            $global:MedRecProSqlcmdCalls | Should Be 2
        }
        finally {
            Remove-Item function:\sqlcmd -ErrorAction SilentlyContinue
            Remove-Variable -Name MedRecProSqlcmdCalls -Scope Global -ErrorAction SilentlyContinue
        }
    }

    It 'still captures complete output when streaming SQL progress live' {
        function global:sqlcmd {
            'line one'
            'line two'
            cmd /c exit 0
        }
        try {
            $result = Invoke-MedRecProSqlCmd -Server 'server.database.windows.net' -Database 'MedRecPro' -Query 'PRINT 1;' -StreamOutput 6>$null
            $result.Success | Should Be $true
            ($result.Output -match 'line one') | Should Be $true
            ($result.Output -match 'line two') | Should Be $true
        }
        finally {
            Remove-Item function:\sqlcmd -ErrorAction SilentlyContinue
        }
    }

    It 'does not retry a non-transient SQL failure even in serverless retry mode' {
        $global:MedRecProSqlcmdCalls = 0
        function global:sqlcmd {
            $global:MedRecProSqlcmdCalls++
            cmd /c exit 1
            "Msg 102, Level 15, State 1: Incorrect syntax near 'SELEC'."
        }
        try {
            $result = Invoke-MedRecProSqlCmd -Server 'server.database.windows.net' -Database 'MedRecPro' -Query 'SELEC 1;' -RetryServerless -RetryDelaysSeconds @(0, 0, 0, 0)
            $result.Success | Should Be $false
            $global:MedRecProSqlcmdCalls | Should Be 1
        }
        finally {
            Remove-Item function:\sqlcmd -ErrorAction SilentlyContinue
            Remove-Variable -Name MedRecProSqlcmdCalls -Scope Global -ErrorAction SilentlyContinue
        }
    }
}

# The Read-Host mock lives in its own Describe because Pester 3 mocks persist for the whole
# Describe that created them, and no other test should see a mocked interactive prompt.
Describe 'MedRecPro refresh interactive prompting' {
    It 'requests a missing connection value once and trims the response' {
        Mock Read-Host { '  server.database.windows.net  ' }
        Read-MedRecProRefreshRequiredValue -Prompt 'Azure SQL server' | Should Be 'server.database.windows.net'
        Assert-MockCalled Read-Host -Times 1 -Exactly
    }

    It 'returns an empty string for a blank response so the caller can fail closed' {
        Mock Read-Host { '   ' }
        Read-MedRecProRefreshRequiredValue -Prompt 'Azure SQL user' | Should Be ''
    }

    It 'defaults the run-mode question to the validate-only pass' {
        Mock Read-Host { '' }
        Read-MedRecProRefreshRunMode | Should Be 'Validate'
    }

    It 'starts the full refresh only on an explicit choice' {
        Mock Read-Host { 'r' }
        Read-MedRecProRefreshRunMode | Should Be 'Refresh'
    }

    It 'rejects an unrecognized run mode instead of assuming a refresh' {
        Mock Read-Host { 'zz' }
        { Read-MedRecProRefreshRunMode } | Should Throw
    }
}
