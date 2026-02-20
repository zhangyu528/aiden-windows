# Scripts

## download-vm.ps1

Download and install `victoria-metrics.exe` to local runtime path (not tracked by git).

Default install location:

- `Aiden.TrayMonitor/runtime/vm/<version>/victoria-metrics.exe`

Usage:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\download-vm.ps1 `
  -Version "v1.113.0" `
  -DownloadUrl "https://github.com/VictoriaMetrics/VictoriaMetrics/releases/download/v1.113.0/victoria-metrics-windows-amd64-v1.113.0.zip" `
  -Sha256 "ed8f660442a45b260a2c0a0976440ecec863bb75ccb7cec6aad9580364a92de6"
```

Notes:

- The script verifies SHA256 before extraction.
- Runtime binaries are excluded by `.gitignore`.

## download-collector.ps1

Download and install OTel Collector binary to project runtime path (not tracked by git).

Default install location:

- `Aiden.TrayMonitor/runtime/collector/<version>/otelcol*.exe`

Usage:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\download-collector.ps1 `
  -Version "v0.146.1" `
  -DownloadUrl "https://github.com/open-telemetry/opentelemetry-collector-releases/releases/download/v0.146.1/otelcol_0.146.1_windows_amd64.tar.gz" `
  -Sha256 "0eaa1ff9d0f5d8009921667368981617641cebb1766fc7b38be95d5dc21a126a"
```

## upgrade-stop-install-start.ps1

Stop current runtime processes, install new package, refresh HKCU auto-start key, and start RuntimeAgent + Tray.

Usage:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\upgrade-stop-install-start.ps1 `
  -NewPackagePath "C:\temp\aiden-new" `
  -InstallPath "C:\Users\<you>\AppData\Local\Aiden"
```

## uninstall-clean-agent.ps1

Stop runtime processes and clean user-level startup entry (`HKCU\...\Run\AidenRuntimeAgent`).

Usage:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\uninstall-clean-agent.ps1 `
  -InstallPath "C:\Users\<you>\AppData\Local\Aiden"
```
