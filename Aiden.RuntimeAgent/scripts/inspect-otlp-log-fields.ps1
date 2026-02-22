param(
    [string]$LogPath = "",
    [int]$Tail = 200
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Resolve-DefaultLogPath {
    $scriptDir = Split-Path -Parent $PSCommandPath
    $agentRoot = Split-Path -Parent $scriptDir
    $runtimeCollector = Join-Path $agentRoot "runtime\\collector"
    if (-not (Test-Path $runtimeCollector)) {
        return $null
    }

    $candidates = Get-ChildItem -Path $runtimeCollector -Recurse -File -Filter "otlp-logs.jsonl" -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime -Descending
    if (-not $candidates) {
        return $null
    }

    return $candidates[0].FullName
}

function Add-FieldCount {
    param(
        [hashtable]$Counter,
        [string]$Path
    )

    if ([string]::IsNullOrWhiteSpace($Path)) {
        return
    }

    if ($Counter.ContainsKey($Path)) {
        $Counter[$Path] += 1
    }
    else {
        $Counter[$Path] = 1
    }
}

function Visit-Json {
    param(
        [object]$Node,
        [string]$Path,
        [hashtable]$FieldCounter,
        [hashtable]$TokenSamples
    )

    if ($null -eq $Node) {
        return
    }

    if ($Node -is [System.Collections.IDictionary]) {
        foreach ($keyObj in $Node.Keys) {
            $key = [string]$keyObj
            $childPath = if ([string]::IsNullOrWhiteSpace($Path)) { $key } else { "$Path.$key" }
            Add-FieldCount -Counter $FieldCounter -Path $childPath

            $lower = $key.ToLowerInvariant()
            if ($lower -match "token|usage|prompt|completion") {
                $sampleValue = $Node[$keyObj]
                if (-not $TokenSamples.ContainsKey($childPath)) {
                    $TokenSamples[$childPath] = $sampleValue
                }
            }

            Visit-Json -Node $Node[$keyObj] -Path $childPath -FieldCounter $FieldCounter -TokenSamples $TokenSamples
        }
        return
    }

    if ($Node -is [psobject] -and $Node.PSObject.Properties.Count -gt 0) {
        foreach ($property in $Node.PSObject.Properties) {
            $key = [string]$property.Name
            $childPath = if ([string]::IsNullOrWhiteSpace($Path)) { $key } else { "$Path.$key" }
            Add-FieldCount -Counter $FieldCounter -Path $childPath

            $lower = $key.ToLowerInvariant()
            if ($lower -match "token|usage|prompt|completion") {
                $sampleValue = $property.Value
                if (-not $TokenSamples.ContainsKey($childPath)) {
                    $TokenSamples[$childPath] = $sampleValue
                }
            }

            Visit-Json -Node $property.Value -Path $childPath -FieldCounter $FieldCounter -TokenSamples $TokenSamples
        }
        return
    }

    if ($Node -is [System.Collections.IEnumerable] -and -not ($Node -is [string])) {
        $index = 0
        foreach ($item in $Node) {
            $childPath = "$Path[$index]"
            Visit-Json -Node $item -Path $childPath -FieldCounter $FieldCounter -TokenSamples $TokenSamples
            $index += 1
        }
    }
}

if ([string]::IsNullOrWhiteSpace($LogPath)) {
    $LogPath = Resolve-DefaultLogPath
}

if ([string]::IsNullOrWhiteSpace($LogPath) -or -not (Test-Path $LogPath)) {
    Write-Host "No otlp log file found. Pass -LogPath explicitly." -ForegroundColor Yellow
    exit 1
}

$lines = Get-Content -Path $LogPath -ErrorAction Stop
if (-not $lines -or $lines.Count -eq 0) {
    Write-Host "Log file is empty: $LogPath" -ForegroundColor Yellow
    exit 0
}

$take = [Math]::Min([Math]::Max($Tail, 1), $lines.Count)
$selected = $lines | Select-Object -Last $take

$fieldCounter = @{}
$tokenSamples = @{}
$parsedCount = 0

foreach ($line in $selected) {
    if ([string]::IsNullOrWhiteSpace($line)) {
        continue
    }

    try {
        $json = $line | ConvertFrom-Json
        $parsedCount += 1
        try {
            Visit-Json -Node $json -Path "" -FieldCounter $fieldCounter -TokenSamples $tokenSamples
        }
        catch {
            # Keep parsed count even if a nested node shape is unexpected.
        }
    }
    catch {
        # Skip malformed lines and keep scanning.
    }

    # Fallback extraction for OTLP JSON where nested structure is array-heavy.
    foreach ($m in [regex]::Matches($line, '"key"\s*:\s*"([^"]+)"\s*,\s*"value"\s*:\s*\{([^}]*)\}')) {
        $attrKey = $m.Groups[1].Value
        Add-FieldCount -Counter $fieldCounter -Path ("attributes." + $attrKey)

        $lower = $attrKey.ToLowerInvariant()
        if ($lower -match "token|usage|prompt|completion" -and -not $tokenSamples.ContainsKey("attributes.$attrKey")) {
            $valuePart = $m.Groups[2].Value
            $sample = $valuePart
            $sv = [regex]::Match($valuePart, '"stringValue"\s*:\s*"([^"]*)"')
            if ($sv.Success) {
                $sample = $sv.Groups[1].Value
            }
            else {
                $iv = [regex]::Match($valuePart, '"intValue"\s*:\s*"?(?<v>-?\d+)')
                if ($iv.Success) {
                    $sample = $iv.Groups["v"].Value
                }
            }

            $tokenSamples["attributes.$attrKey"] = $sample
        }
    }
}

Write-Host "File: $LogPath"
Write-Host "Scanned lines: $take"
Write-Host "Parsed JSON lines: $parsedCount"
Write-Host ""

Write-Host "Top fields by frequency:" -ForegroundColor Cyan
$fieldCounter.GetEnumerator() |
    Sort-Object Value -Descending |
    Select-Object -First 80 |
    ForEach-Object { "{0,6}  {1}" -f $_.Value, $_.Key } |
    Write-Host

Write-Host ""
Write-Host "Token-related field samples:" -ForegroundColor Cyan
if ($tokenSamples.Count -eq 0) {
    Write-Host "  (none found)"
}
else {
    $tokenSamples.GetEnumerator() |
        Sort-Object Name |
        ForEach-Object {
            $value = $_.Value
            $display = if ($null -eq $value) {
                "null"
            }
            elseif ($value -is [string]) {
                $value
            }
            elseif ($value -is [System.ValueType]) {
                [string]$value
            }
            else {
                try {
                    ($value | ConvertTo-Json -Depth 5 -Compress)
                }
                catch {
                    [string]$value
                }
            }

            if ($display.Length -gt 160) {
                $display = $display.Substring(0, 160) + "..."
            }

            "  {0} = {1}" -f $_.Name, $display
        } |
        Write-Host
}
