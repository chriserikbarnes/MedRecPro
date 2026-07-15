[CmdletBinding()]
param(
    [ValidateSet("Fast", "DebugContract", "ReleaseContract", "Full", "All")]
    [string]$Gate = "Fast",

    [switch]$SkipRestore
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$solutionPath = Join-Path $repoRoot "MedRecPro.sln"
$testProjectPath = Join-Path $repoRoot "MedRecProTest\MedRecProTest.csproj"
$releaseOutputPath = Join-Path $repoRoot "MedRecPro\.codex-build\test-contract-release"
$buildProperties = @("-p:UseAppHost=false", "-p:UseSharedCompilation=false")
$fastFilter = "FullyQualifiedName~TestProjectDependencyGuardTests|FullyQualifiedName~ReflectionUsageArchitectureTests|FullyQualifiedName~MedRecProPublicSurfaceInventoryTests|FullyQualifiedName~LabelControllerRouteCompatibilityTests"
$contractFilter = "(TestCategory=Contract&FullyQualifiedName!~OpenApiDocumentFilterTests)|FullyQualifiedName~StartupSmokeTests"

function Invoke-DotNet
{
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$CommandArguments
    )

    Write-Host "dotnet $($CommandArguments -join ' ')"
    & dotnet @CommandArguments

    if ($LASTEXITCODE -ne 0)
    {
        throw "dotnet command failed with exit code $LASTEXITCODE."
    }
}

function Assert-NoMatches
{
    param(
        [Parameter(Mandatory = $true)]
        [string]$Pattern,

        [Parameter(Mandatory = $true)]
        [string]$Path,

        [Parameter(Mandatory = $true)]
        [string]$Description
    )

    & rg -n $Pattern $Path -g "*.cs" -g "*.csproj"

    if ($LASTEXITCODE -eq 0)
    {
        throw "$Description."
    }

    if ($LASTEXITCODE -ne 1)
    {
        throw "The $Description scan could not complete (rg exit code $LASTEXITCODE)."
    }
}

function Invoke-FastGate
{
    Invoke-DotNet -CommandArguments (@(
            "test", $testProjectPath, "--no-restore", "--filter", $fastFilter
        ) + $buildProperties)
}

function Invoke-DebugContractGate
{
    Invoke-DotNet -CommandArguments (@(
            "test", $testProjectPath, "--no-restore", "--filter", $contractFilter
        ) + $buildProperties)
}

function Invoke-ReleaseContractGate
{
    New-Item -ItemType Directory -Force -Path $releaseOutputPath | Out-Null
    $isolatedOutputProperty = "-p:BaseOutputPath=$releaseOutputPath$([IO.Path]::DirectorySeparatorChar)"

    Invoke-DotNet -CommandArguments (@(
            "test", $testProjectPath, "-c", "Release", "--no-restore", "--filter", $contractFilter,
            $isolatedOutputProperty
        ) + $buildProperties)
}

function Invoke-FullGate
{
    Invoke-DotNet -CommandArguments (@("build", $solutionPath, "--no-restore") + $buildProperties)
    Invoke-DotNet -CommandArguments (@("test", $testProjectPath, "--no-restore", "--no-build") + $buildProperties)

    Assert-NoMatches -Pattern "AddUserSecrets|UserSecretsId|Microsoft.Extensions.Configuration.UserSecrets" `
        -Path (Join-Path $repoRoot "MedRecProTest") `
        -Description "MedRecProTest must not reference developer-secret configuration"
    Assert-NoMatches -Pattern "Thread.Sleep|Task.Delay|Stopwatch|GetField\(|SetValue\(" `
        -Path (Join-Path $repoRoot "MedRecProTest") `
        -Description "MedRecProTest must not use nondeterministic delay or private-state reflection seams"

    & git -C $repoRoot diff --check

    if ($LASTEXITCODE -ne 0)
    {
        throw "git diff --check failed with exit code $LASTEXITCODE."
    }
}

Push-Location $repoRoot

try
{
    if (-not $SkipRestore)
    {
        Invoke-DotNet -CommandArguments @("restore", $solutionPath)
    }

    switch ($Gate)
    {
        "Fast"
        {
            Invoke-FastGate
            break
        }
        "DebugContract"
        {
            Invoke-DebugContractGate
            break
        }
        "ReleaseContract"
        {
            Invoke-ReleaseContractGate
            break
        }
        "Full"
        {
            Invoke-FullGate
            break
        }
        "All"
        {
            Invoke-FastGate
            Invoke-DebugContractGate
            Invoke-ReleaseContractGate
            Invoke-FullGate
            break
        }
    }

    Write-Host "MedRecPro $Gate verification gate passed."
}
finally
{
    Pop-Location
}
