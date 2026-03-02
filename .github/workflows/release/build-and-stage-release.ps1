param(
    [Parameter(Mandatory = $true)]
    [string]$Version,
    [Parameter(Mandatory = $false)]
    [string]$Rid = 'win-x64'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if ([string]::IsNullOrWhiteSpace($Version)) {
    throw "Version is empty."
}

dotnet publish Aiden.TrayMonitor/Aiden.TrayMonitor.csproj `
  -c Release `
  -r $Rid `
  --self-contained true `
  /p:Version=$Version `
  /p:PublishSingleFile=true `
  /p:IncludeNativeLibrariesForSelfExtract=true `
  /p:DebugType=None `
  /p:DebugSymbols=false `
  -o artifacts/stage/tray

dotnet publish Aiden.RuntimeAgent/Aiden.RuntimeAgent.csproj `
  -c Release `
  -r $Rid `
  --self-contained true `
  /p:Version=$Version `
  /p:PublishSingleFile=true `
  /p:IncludeNativeLibrariesForSelfExtract=true `
  /p:DebugType=None `
  /p:DebugSymbols=false `
  -o artifacts/stage/agent

& .github/scripts/prepare-package.ps1 -Version $Version
