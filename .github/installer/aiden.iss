#define InstallerBitness "win-x64"

#ifndef AppVersion
#define AppVersion "0.0.0"
#endif

#ifndef SourceDir
#define SourceDir "..\\..\\artifacts\\stage\\package"
#endif

#ifndef OutputDir
#define OutputDir "..\\..\\artifacts\\installer"
#endif

#define InstallerFilename "Aiden-Setup-" + AppVersion + "-" + InstallerBitness

[Setup]
AppName=Aiden
AppVersion={#AppVersion}
DefaultDirName={localappdata}\Aiden
DefaultGroupName=Aiden
SetupIconFile={#SourceDir}\aiden.ico
OutputDir={#OutputDir}
OutputBaseFilename={#InstallerFilename}
DisableProgramGroupPage=no
Compression=lzma
SolidCompression=yes
ArchitecturesInstallIn64BitMode=x64
PrivilegesRequired=lowest
UninstallDisplayIcon={app}\Aiden.TrayMonitor.exe
CloseApplications=yes
ForceCloseApplications=yes
RestartApplications=no

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Tasks]
Name: desktopicon; Description: Create a desktop icon; GroupDescription: Additional icons; Flags: unchecked

[Icons]
Name: "{group}\Aiden Tray Monitor"; Filename: "{app}\Aiden.TrayMonitor.exe"; IconFilename: "{app}\aiden.ico"
Name: "{commondesktop}\Aiden Tray Monitor"; Filename: "{app}\Aiden.TrayMonitor.exe"; Tasks: desktopicon; IconFilename: "{app}\aiden.ico"

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "AidenRuntimeAgent"; ValueData: """{app}\Aiden.RuntimeAgent.exe"""; Flags: uninsdeletevalue

[Run]
Filename: "powershell.exe"; Parameters: "-NoProfile -ExecutionPolicy Bypass -File ""{app}\scripts\install-runtime-deps.ps1"" -InstallDir ""{app}"""; StatusMsg: "Downloading runtime dependencies (VictoriaMetrics and OpenTelemetry Collector)..."; Flags: waituntilterminated

[Run]
Filename: "{app}\Aiden.TrayMonitor.exe"; Description: Launch Aiden Tray Monitor; Flags: nowait postinstall skipifsilent


