param(
    [string]$Configuration = 'Debug',
    [switch]$NoRestore,
    [string]$ResultsDirectory
)

$ErrorActionPreference = 'Stop'
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..')

$testProjects = @(
    (Join-Path $repoRoot "tests/Aiden.RuntimeAgent.UnitTests/Aiden.RuntimeAgent.UnitTests.csproj"),
    (Join-Path $repoRoot "tests/Aiden.TrayMonitor.UnitTests/Aiden.TrayMonitor.UnitTests.csproj")
)

$restoreFlag = if ($NoRestore) { "--no-restore" } else { "" }

foreach ($project in $testProjects) {
    $projectName = [System.IO.Path]::GetFileNameWithoutExtension($project)
    Write-Host "Running .NET Unit Tests for $projectName..." -ForegroundColor Yellow
    dotnet test $project -c $Configuration $restoreFlag --collect:"XPlat Code Coverage" `
        --logger "trx;LogFileName=$($projectName.ToLower()).trx" `
        --results-directory $ResultsDirectory `
        --nologo
}
