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
OutputDir={#OutputDir}
OutputBaseFilename={#InstallerFilename}
DisableProgramGroupPage=no
Compression=lzma
SolidCompression=yes
ArchitecturesInstallIn64BitMode=x64
PrivilegesRequired=lowest
UninstallDisplayIcon={app}\Aiden.TrayMonitor.exe

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Tasks]
Name: desktopicon; Description: Create a desktop icon; GroupDescription: Additional icons; Flags: unchecked

[Icons]
Name: "{group}\Aiden Tray Monitor"; Filename: "{app}\Aiden.TrayMonitor.exe"
Name: "{commondesktop}\Aiden Tray Monitor"; Filename: "{app}\Aiden.TrayMonitor.exe"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "AidenRuntimeAgent"; ValueData: """{app}\Aiden.RuntimeAgent.exe"""; Flags: uninsdeletevalue; Check: not IsUninstallMode

[Run]
Filename: "powershell.exe"; Parameters: "-NoProfile -ExecutionPolicy Bypass -File ""{app}\scripts\install-runtime-deps.ps1"" -InstallDir ""{app}"""; Flags: waituntilterminated runhidden; Check: FileExists("{app}\scripts\install-runtime-deps.ps1")

[Run]
Filename: "{app}\Aiden.TrayMonitor.exe"; Description: Launch Aiden Tray Monitor; Flags: nowait postinstall skipifsilent
[Code]
function InitializeSetup(): Boolean;
begin
  Result := True;
  if not DirExists(ExpandConstant('{#SourceDir}')) then
  begin
    MsgBox(Format('Staging directory %s was not found. Run the packaging script before building the installer.', [ExpandConstant('{#SourceDir}')]), mbError, MB_OK);
    Result := False;
  end;
end;
