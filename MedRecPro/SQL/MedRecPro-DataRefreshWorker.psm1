Set-StrictMode -Version Latest

#region implementation

function ConvertTo-MedRecProPlainText {
    <#
    .SYNOPSIS
    Converts a secure string only for the lifetime of a native utility invocation.
    #>
    param(
        [Parameter(Mandatory = $true)]
        [Security.SecureString]$SecureString
    )

    $buffer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($SecureString)
    try {
        return [Runtime.InteropServices.Marshal]::PtrToStringBSTR($buffer)
    }
    finally {
        [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($buffer)
    }
}

function Get-MedRecProDomainConfiguration {
    <#
    .SYNOPSIS
    Returns the fixed BCP table order and flag contract for one refresh domain.
    #>
    param(
        [Parameter(Mandatory = $true)]
        [ValidateSet('Core', 'OrangeBook', 'TempTables', 'AdverseEvents')]
        [string]$Domain
    )

    $configuration = switch ($Domain) {
        'Core' {
            [PSCustomObject]@{
                Name = 'Core'
                Tables = @(
                    'ActiveMoiety', 'AdditionalIdentifier', 'Address', 'Analyte', 'ApplicationType', 'AttachedDocument',
                    'BillingUnitIndex', 'BusinessOperation', 'BusinessOperationProductLink', 'BusinessOperationQualifier',
                    'CertificationProductLink', 'Characteristic', 'Commodity', 'ComplianceAction', 'ContactParty',
                    'ContactPartyTelecom', 'ContactPerson', 'ContributingFactor', 'DisciplinaryAction', 'Document',
                    'DocumentAuthor', 'DocumentRelationship', 'DocumentRelationshipIdentifier', 'DosingSpecification',
                    'EquivalentEntity', 'FacilityProductLink', 'GenericMedicine', 'Holder', 'IdentifiedSubstance', 'Ingredient',
                    'IngredientInstance', 'IngredientSourceProduct', 'IngredientSubstance', 'InteractionConsequence',
                    'InteractionIssue', 'LegalAuthenticator', 'License', 'LotHierarchy', 'LotIdentifier', 'MarketingCategory',
                    'MarketingStatus', 'Moiety', 'NamedEntity', 'NCTLink', 'ObservationCriterion', 'ObservationMedia',
                    'Organization', 'OrganizationIdentifier', 'OrganizationTelecom', 'PackageIdentifier', 'PackagingHierarchy',
                    'PackagingLevel', 'PartOfAssembly', 'PharmacologicClass', 'PharmacologicClassHierarchy',
                    'PharmacologicClassLink', 'PharmacologicClassName', 'PharmClassDosageFormExclusion', 'Policy', 'Product',
                    'ProductConcept', 'ProductConceptEquivalence', 'ProductEvent', 'ProductIdentifier', 'ProductInstance',
                    'ProductPart', 'ProductRouteOfAdministration', 'ProductWebLink', 'Protocol', 'ReferenceSubstance',
                    'RelatedDocument', 'REMSApproval', 'REMSElectronicResource', 'REMSMaterial', 'RenderedMedia', 'Requirement',
                    'ResponsiblePersonLink', 'Section', 'SectionExcerptHighlight', 'SectionHierarchy', 'SectionTextContent',
                    'SpecializedKind', 'SpecifiedSubstance', 'SplData', 'Stakeholder', 'StructuredBody', 'SubstanceSpecification',
                    'Telecom', 'TerritorialAuthority', 'TextList', 'TextListItem', 'TextTable', 'TextTableCell', 'TextTableColumn',
                    'TextTableRow', 'WarningLetterDate', 'WarningLetterProductInfo'
                )
                ExportFlags = @('-n')
                ImportFlags = @('-n', '-E')
            }
        }
        'OrangeBook' {
            [PSCustomObject]@{
                Name = 'OrangeBook'
                Tables = @(
                    'OrangeBookPatentUseCode', 'OrangeBookApplicant', 'OrangeBookProduct', 'OrangeBookPatent',
                    'OrangeBookExclusivity', 'OrangeBookProductMarketingCategory', 'OrangeBookProductIngredientSubstance',
                    'OrangeBookApplicantOrganization'
                )
                ExportFlags = @('-n')
                ImportFlags = @('-n', '-q', '-E')
            }
        }
        'TempTables' {
            [PSCustomObject]@{
                Name = 'TempTables'
                Tables = @('tmp_SectionContent', 'tmp_LabelSectionMarkdown', 'tmp_InventorySummary')
                ExportFlags = @('-n')
                ImportFlags = @('-n')
            }
        }
        'AdverseEvents' {
            [PSCustomObject]@{
                Name = 'AdverseEvents'
                Tables = @(
                    'tmp_FlattenedStandardizedTable', 'tmp_FlattenedAdverseEventTable',
                    'tmp_FlattenedAdverseEventCoverageTable', 'tmp_FlattenedAdverseEventRiskTable',
                    'tmp_AeDashboardProductCatalog'
                )
                ExportFlags = @('-n', '-q')
                ImportFlags = @('-n', '-q', '-E')
            }
        }
    }

    return $configuration
}

function Test-MedRecProTransientSqlFailure {
    <#
    .SYNOPSIS
    Identifies sqlcmd output that indicates a transient serverless resume or network condition.
    #>
    param([string]$Output)

    if (-not $Output) { return $false }

    # 40613/40197/40501 are Azure availability/resume codes, 10928/10929 are resource limits;
    # the text patterns cover auto-pause warm-up, login timeouts, and transport drops.
    return $Output -match '(?i)\b(40613|40197|40501|10928|10929)\b|is not currently available|Login timeout expired|Connection Timeout Expired|network-related or instance-specific|semaphore timeout'
}

function Invoke-MedRecProSqlCmd {
    <#
    .SYNOPSIS
    Runs sqlcmd with the legacy workers' -U/-P authentication pattern and never logs its arguments.
    #>
    param(
        [Parameter(Mandatory = $true)]
        [string]$Server,

        [Parameter(Mandatory = $true)]
        [string]$Database,

        [string]$Query,

        [string]$InputFile,

        [string]$User,

        [Security.SecureString]$Password,

        [int]$LoginTimeoutSeconds = 30,

        [int]$QueryTimeoutSeconds = 30,

        [switch]$RetryServerless,

        [int[]]$RetryDelaysSeconds = @(0, 5, 15, 30),

        [switch]$StreamOutput
    )

    if (-not (Get-Command sqlcmd -ErrorAction SilentlyContinue)) {
        return [PSCustomObject]@{ Success = $false; ExitCode = -1; Output = 'sqlcmd was not found in PATH.' }
    }

    $attempts = if ($RetryServerless) { [Math]::Max(1, $RetryDelaysSeconds.Count) } else { 1 }
    $delays = $RetryDelaysSeconds
    $lastResult = $null

    for ($attempt = 0; $attempt -lt $attempts; $attempt++) {
        if ($attempt -gt 0) {
            Start-Sleep -Seconds $delays[$attempt]
        }

        $arguments = New-Object System.Collections.Generic.List[string]
        [void]$arguments.Add('-S')
        [void]$arguments.Add($Server)
        [void]$arguments.Add('-d')
        [void]$arguments.Add($Database)
        [void]$arguments.Add('-b')
        [void]$arguments.Add('-V')
        [void]$arguments.Add('11')
        [void]$arguments.Add('-l')
        [void]$arguments.Add($LoginTimeoutSeconds.ToString())
        [void]$arguments.Add('-t')
        [void]$arguments.Add($QueryTimeoutSeconds.ToString())
        [void]$arguments.Add('-h')
        [void]$arguments.Add('-1')
        [void]$arguments.Add('-W')
        # -I sets QUOTED_IDENTIFIER ON like SSMS and Azure Query Editor. sqlcmd defaults it
        # OFF, which fails ALTER/CREATE INDEX on the AE tmp_ tables' persisted computed
        # columns - the reason index rebuilds historically required the Azure portal.
        [void]$arguments.Add('-I')

        if ($InputFile) {
            [void]$arguments.Add('-i')
            [void]$arguments.Add($InputFile)
        }
        else {
            [void]$arguments.Add('-Q')
            [void]$arguments.Add($Query)
        }

        $plainText = $null
        try {
            if ($User) {
                if (-not $Password) {
                    return [PSCustomObject]@{ Success = $false; ExitCode = -1; Output = 'A secure Azure SQL password is required.' }
                }

                # The legacy workers authenticate with -U/-P arguments, matching bcp, and that
                # is the proven connection pattern for this environment. Arguments are never
                # logged, so the password stays out of logs, manifests, and run state.
                $plainText = ConvertTo-MedRecProPlainText -SecureString $Password
                [void]$arguments.Add('-U')
                [void]$arguments.Add($User)
                [void]$arguments.Add('-P')
                [void]$arguments.Add($plainText)
            }
            else {
                [void]$arguments.Add('-E')
            }

            if ($StreamOutput) {
                # Relay each sqlcmd line to the console as it arrives so long-running
                # disable/nuke/rebuild scripts show live progress, while the complete
                # output is still captured for the run log and postcondition checks.
                $streamBuilder = New-Object System.Text.StringBuilder
                & sqlcmd @arguments 2>&1 | ForEach-Object {
                    $line = $_.ToString()
                    Write-Host "    $line"
                    [void]$streamBuilder.AppendLine($line)
                }
                $output = $streamBuilder.ToString()
            }
            else {
                $output = & sqlcmd @arguments 2>&1 | Out-String
            }
            $lastResult = [PSCustomObject]@{
                Success = ($LASTEXITCODE -eq 0)
                ExitCode = $LASTEXITCODE
                Output = $output
            }
        }
        catch {
            $lastResult = [PSCustomObject]@{ Success = $false; ExitCode = -1; Output = $_.Exception.Message }
        }
        finally {
            $plainText = $null
        }

        # Retry is reserved for transient serverless resume/network conditions; a genuine
        # SQL failure (syntax, permission, constraint) must surface immediately.
        if ($lastResult.Success -or -not $RetryServerless -or -not (Test-MedRecProTransientSqlFailure -Output $lastResult.Output)) {
            return $lastResult
        }
    }

    return $lastResult
}

function Get-MedRecProSqlRowCount {
    <#
    .SYNOPSIS
    Gets one exact table count through sqlcmd and reports query failures explicitly.
    #>
    param(
        [Parameter(Mandatory = $true)]
        [string]$Server,

        [Parameter(Mandatory = $true)]
        [string]$Database,

        [Parameter(Mandatory = $true)]
        [string]$Table,

        [string]$User,

        [Security.SecureString]$Password,

        [switch]$RetryServerless
    )

    $safeTable = $Table.Replace(']', ']]')
    $result = Invoke-MedRecProSqlCmd -Server $Server -Database $Database -User $User -Password $Password -RetryServerless:$RetryServerless -Query "SET NOCOUNT ON; SELECT COUNT_BIG(*) FROM [dbo].[$safeTable];"
    if (-not $result.Success) {
        return [PSCustomObject]@{ Success = $false; Count = $null; Error = $result.Output }
    }

    $matches = [regex]::Matches($result.Output, '(?m)^\s*(\d+)\s*$')
    if ($matches.Count -ne 1) {
        return [PSCustomObject]@{ Success = $false; Count = $null; Error = "sqlcmd did not return one row count for $Table." }
    }

    return [PSCustomObject]@{ Success = $true; Count = [long]$matches[0].Groups[1].Value; Error = $null }
}

function Invoke-MedRecProBcp {
    <#
    .SYNOPSIS
    Runs one native-format BCP operation with the domain's established flags.
    #>
    param(
        [Parameter(Mandatory = $true)]
        [ValidateSet('Export', 'Import')]
        [string]$Operation,

        [Parameter(Mandatory = $true)]
        [string]$Server,

        [Parameter(Mandatory = $true)]
        [string]$Database,

        [Parameter(Mandatory = $true)]
        [string]$Table,

        [Parameter(Mandatory = $true)]
        [string]$DataFile,

        [Parameter(Mandatory = $true)]
        [string[]]$FormatFlags,

        [int]$BatchSize = 50000,

        [string]$User,

        [Security.SecureString]$Password
    )

    if (-not (Get-Command bcp -ErrorAction SilentlyContinue)) {
        return [PSCustomObject]@{ Success = $false; ExitCode = -1; RowsCopied = 0; Output = 'bcp was not found in PATH.' }
    }

    $errorFile = if ($Operation -eq 'Export') { "$DataFile.err" } else { "$DataFile.import.err" }
    $arguments = New-Object System.Collections.Generic.List[string]
    [void]$arguments.Add("$Database.dbo.$Table")

    # An if statement inside plain parentheses is parsed as a command named 'if' and fails at
    # runtime, so the bcp direction must be resolved into a variable before joining the list.
    $direction = if ($Operation -eq 'Export') { 'out' } else { 'in' }
    [void]$arguments.Add($direction)
    [void]$arguments.Add($DataFile)
    [void]$arguments.Add('-S')
    [void]$arguments.Add($Server)

    $plainText = $null
    try {
        if ($Operation -eq 'Export') {
            [void]$arguments.Add('-T')
        }
        else {
            if (-not $User -or -not $Password) {
                return [PSCustomObject]@{ Success = $false; ExitCode = -1; RowsCopied = 0; Output = 'Azure user and secure password are required for BCP import.' }
            }

            $plainText = ConvertTo-MedRecProPlainText -SecureString $Password
            [void]$arguments.Add('-U')
            [void]$arguments.Add($User)
            [void]$arguments.Add('-P')
            [void]$arguments.Add($plainText)
        }

        foreach ($flag in $FormatFlags) {
            [void]$arguments.Add($flag)
        }

        if ($Operation -eq 'Import') {
            [void]$arguments.Add('-b')
            [void]$arguments.Add($BatchSize.ToString())
            [void]$arguments.Add('-h')
            [void]$arguments.Add('TABLOCK')
        }

        [void]$arguments.Add('-e')
        [void]$arguments.Add($errorFile)
        $output = & bcp @arguments 2>&1 | Out-String
        $exitCode = $LASTEXITCODE
        $rowsCopied = 0
        if ($output -match '(\d+) rows copied') {
            $rowsCopied = [long]$Matches[1]
        }

        $hasRejectedRows = (Test-Path -LiteralPath $errorFile) -and ((Get-Item -LiteralPath $errorFile).Length -gt 0)
        return [PSCustomObject]@{
            Success = ($exitCode -eq 0 -and -not $hasRejectedRows)
            ExitCode = $exitCode
            RowsCopied = $rowsCopied
            Output = $output
        }
    }
    catch {
        return [PSCustomObject]@{ Success = $false; ExitCode = -1; RowsCopied = 0; Output = $_.Exception.Message }
    }
    finally {
        $plainText = $null
    }
}

function Invoke-MedRecProDomainWorker {
    <#
    .SYNOPSIS
    Executes a strict, noninteractive BCP export or import for one MedRecPro data domain.
    #>
    param(
        [Parameter(Mandatory = $true)]
        [ValidateSet('Core', 'OrangeBook', 'TempTables', 'AdverseEvents')]
        [string]$Domain,

        [Parameter(Mandatory = $true)]
        [ValidateSet('Export', 'Import', 'Both')]
        [string]$Operation,

        [string]$LocalServer = 'localhost',

        [string]$LocalDatabase = 'MedRecLocal',

        [string]$AzureServer,

        [string]$AzureDatabase,

        [string]$AzureUser,

        [Security.SecureString]$AzurePassword,

        [Parameter(Mandatory = $true)]
        [string]$DataPath,

        [int]$BatchSize = 50000,

        [int]$ParallelThrottle = 3,

        [string[]]$ExcludeTable,

        [object[]]$ExpectedInventory,

        [switch]$Strict,

        [switch]$NonInteractive,

        [switch]$OverwriteData,

        [switch]$SkipTargetTruncate
    )

    $startedAt = Get-Date
    $configuration = Get-MedRecProDomainConfiguration -Domain $Domain
    $tables = @($configuration.Tables | Where-Object { $ExcludeTable -notcontains $_ })
    $tableResults = New-Object System.Collections.Generic.List[object]
    $failedTables = New-Object System.Collections.Generic.List[string]
    $succeededTables = New-Object System.Collections.Generic.List[string]

    if (-not (Test-Path -LiteralPath $DataPath)) {
        New-Item -ItemType Directory -Path $DataPath -Force | Out-Null
    }

    if ($tables.Count -eq 0) {
        return [PSCustomObject]@{ Domain = $Domain; Operation = $Operation; ExpectedTables = @(); SucceededTables = @(); FailedTables = @('No selected tables'); RowTotals = [PSCustomObject]@{ Source = 0; Target = 0 }; FileInventory = @(); Duration = (Get-Date) - $startedAt; Success = $false; TableResults = @() }
    }

    if ($Operation -in @('Export', 'Both')) {
        $existingFiles = @(Get-ChildItem -LiteralPath $DataPath -Filter '*.dat' -File -ErrorAction SilentlyContinue)
        if ($existingFiles.Count -gt 0) {
            if (-not $OverwriteData) {
                $failedTables.Add('Existing export files')
            }
            else {
                foreach ($file in $existingFiles) {
                    Remove-Item -LiteralPath $file.FullName -Force
                    Remove-Item -LiteralPath "$($file.FullName).err" -Force -ErrorAction SilentlyContinue
                }
            }
        }

        $exportIndex = 0
        foreach ($table in $tables) {
            if ($failedTables.Count -gt 0 -and $Strict) { break }

            $exportIndex++
            Write-Host ("  [{0}/{1}] {2} export: {3}" -f $exportIndex, $tables.Count, $Domain, $table)
            $sourceCount = Get-MedRecProSqlRowCount -Server $LocalServer -Database $LocalDatabase -Table $table
            $dataFile = Join-Path $DataPath "$table.dat"
            if (-not $sourceCount.Success) {
                Write-Host "    FAILED: $table" -ForegroundColor Red
                $failedTables.Add($table)
                $tableResults.Add([PSCustomObject]@{ Table = $table; Operation = 'Export'; Success = $false; SourceRows = $null; TargetRows = $null; FilePath = $dataFile; FileSize = 0; Hash = $null; Error = $sourceCount.Error })
                continue
            }

            $bcp = Invoke-MedRecProBcp -Operation Export -Server $LocalServer -Database $LocalDatabase -Table $table -DataFile $dataFile -FormatFlags $configuration.ExportFlags
            $fileExists = Test-Path -LiteralPath $dataFile
            $fileSize = if ($fileExists) { (Get-Item -LiteralPath $dataFile).Length } else { 0 }
            $success = $bcp.Success -and $fileExists
            if ($success) {
                $hash = (Get-FileHash -LiteralPath $dataFile -Algorithm SHA256).Hash
                Write-Host ("    OK: {0:N0} rows, {1:N1} MB" -f $sourceCount.Count, ($fileSize / 1MB))
                $succeededTables.Add($table)
                $tableResults.Add([PSCustomObject]@{ Table = $table; Operation = 'Export'; Success = $true; SourceRows = $sourceCount.Count; TargetRows = $null; FilePath = $dataFile; FileSize = $fileSize; Hash = $hash; Error = $null })
            }
            else {
                Write-Host "    FAILED: $table" -ForegroundColor Red
                $failedTables.Add($table)
                $tableResults.Add([PSCustomObject]@{ Table = $table; Operation = 'Export'; Success = $false; SourceRows = $sourceCount.Count; TargetRows = $null; FilePath = $dataFile; FileSize = $fileSize; Hash = $null; Error = $bcp.Output })
            }
        }
    }

    if ($Operation -in @('Import', 'Both') -and $failedTables.Count -eq 0) {
        if (-not $AzureServer -or -not $AzureDatabase -or -not $AzureUser -or -not $AzurePassword) {
            $failedTables.Add('Azure credentials')
        }

        if (-not $SkipTargetTruncate -and $failedTables.Count -eq 0) {
            foreach ($table in @($tables | Sort-Object -Descending)) {
                $truncate = Invoke-MedRecProSqlCmd -Server $AzureServer -Database $AzureDatabase -User $AzureUser -Password $AzurePassword -Query "TRUNCATE TABLE [dbo].[$($table.Replace(']', ']]'))];"
                if (-not $truncate.Success) {
                    $failedTables.Add($table)
                    break
                }
            }
        }

        $importIndex = 0
        foreach ($table in $tables) {
            if ($failedTables.Count -gt 0 -and $Strict) { break }
            $importIndex++
            Write-Host ("  [{0}/{1}] {2} import: {3}" -f $importIndex, $tables.Count, $Domain, $table)
            $dataFile = Join-Path $DataPath "$table.dat"
            $expected = @($ExpectedInventory | Where-Object { $_.Table -eq $table } | Select-Object -First 1)
            if ($expected.Count -eq 1 -and $expected[0].FilePath) { $dataFile = [string]$expected[0].FilePath }
            if (-not (Test-Path -LiteralPath $dataFile)) {
                $failedTables.Add($table)
                $tableResults.Add([PSCustomObject]@{ Table = $table; Operation = 'Import'; Success = $false; SourceRows = $null; TargetRows = $null; FilePath = $dataFile; FileSize = 0; Hash = $null; Error = 'Required data file is missing.' })
                continue
            }

            $fileHash = (Get-FileHash -LiteralPath $dataFile -Algorithm SHA256).Hash
            if ($expected.Count -eq 1 -and $expected[0].Hash -and $expected[0].Hash -ne $fileHash) {
                $failedTables.Add($table)
                $tableResults.Add([PSCustomObject]@{ Table = $table; Operation = 'Import'; Success = $false; SourceRows = $expected[0].SourceRows; TargetRows = $null; FilePath = $dataFile; FileSize = (Get-Item -LiteralPath $dataFile).Length; Hash = $fileHash; Error = 'Data file hash does not match the manifest.' })
                continue
            }

            $sourceRows = if ($expected.Count -eq 1 -and $null -ne $expected[0].SourceRows) { [long]$expected[0].SourceRows } else { (Get-MedRecProSqlRowCount -Server $LocalServer -Database $LocalDatabase -Table $table).Count }
            $bcp = Invoke-MedRecProBcp -Operation Import -Server $AzureServer -Database $AzureDatabase -Table $table -DataFile $dataFile -FormatFlags $configuration.ImportFlags -BatchSize $BatchSize -User $AzureUser -Password $AzurePassword
            $targetCount = Get-MedRecProSqlRowCount -Server $AzureServer -Database $AzureDatabase -Table $table -User $AzureUser -Password $AzurePassword
            $success = $bcp.Success -and $targetCount.Success -and ($targetCount.Count -eq $sourceRows)
            if ($success) {
                Write-Host ("    OK: {0:N0} rows verified on target" -f $targetCount.Count)
                $succeededTables.Add($table)
            }
            else {
                Write-Host "    FAILED: $table" -ForegroundColor Red
                $failedTables.Add($table)
            }
            $tableResults.Add([PSCustomObject]@{ Table = $table; Operation = 'Import'; Success = $success; SourceRows = $sourceRows; TargetRows = $targetCount.Count; FilePath = $dataFile; FileSize = (Get-Item -LiteralPath $dataFile).Length; Hash = $fileHash; Error = if ($success) { $null } elseif (-not $targetCount.Success) { $targetCount.Error } elseif ($targetCount.Count -ne $sourceRows) { "Target row count $($targetCount.Count) does not match source row count $sourceRows." } else { $bcp.Output } })
        }
    }

    $fileInventory = @($tableResults | Where-Object { $_.Operation -eq 'Export' })

    # Accumulate totals with a plain loop. Under strict mode an empty Measure-Object result
    # (for example TargetRows during an export-only pass) exposes no Sum member in
    # Windows PowerShell 5.1, which terminates the worker.
    $sourceTotal = [long]0
    $targetTotal = [long]0
    foreach ($tableResult in $tableResults) {
        if ($null -ne $tableResult.SourceRows) { $sourceTotal += [long]$tableResult.SourceRows }
        if ($null -ne $tableResult.TargetRows) { $targetTotal += [long]$tableResult.TargetRows }
    }

    $uniqueFailedTables = @($failedTables | Sort-Object -Unique)
    $uniqueSucceededCount = @($succeededTables | Sort-Object -Unique).Count
    $allTablesSucceeded = ($failedTables.Count -eq 0 -and $uniqueSucceededCount -eq $tables.Count)
    $duration = (Get-Date) - $startedAt

    # List.ToArray replaces @(...) here: the @() subexpression over a List[object] that
    # holds PSCustomObjects throws 'Argument types do not match' on Windows PowerShell 5.1.
    $result = [ordered]@{}
    $result['Domain'] = $Domain
    $result['Operation'] = $Operation
    $result['ExpectedTables'] = $tables
    $result['SucceededTables'] = $succeededTables.ToArray()
    $result['FailedTables'] = $uniqueFailedTables
    $result['RowTotals'] = [PSCustomObject]@{ Source = [long]$sourceTotal; Target = [long]$targetTotal }
    $result['FileInventory'] = $fileInventory
    $result['Duration'] = $duration
    $result['Success'] = $allTablesSucceeded
    $result['TableResults'] = $tableResults.ToArray()
    return [PSCustomObject]$result
}

Export-ModuleMember -Function Get-MedRecProDomainConfiguration, Invoke-MedRecProSqlCmd, Get-MedRecProSqlRowCount, Invoke-MedRecProBcp, Invoke-MedRecProDomainWorker

#endregion implementation
