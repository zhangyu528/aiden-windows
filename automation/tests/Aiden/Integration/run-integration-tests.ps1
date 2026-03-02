param(
    [string]$Configuration = 'Debug',
    [switch]$NoRestore,
    [string]$ResultsDirectory
)

$ErrorActionPreference = 'Stop'
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..')

$testProject = Join-Path $repoRoot "tests/Aiden.IntegrationTests/Aiden.IntegrationTests.csproj"
$restoreFlag = if ($NoRestore) { "--no-restore" } else { "" }

Write-Host "Running .NET Integration Tests..." -ForegroundColor Yellow
dotnet test $testProject -c $Configuration $restoreFlag --collect:"XPlat Code Coverage" `
    --logger "trx;LogFileName=integration.trx" `
    --results-directory $ResultsDirectory `
    --nologo
