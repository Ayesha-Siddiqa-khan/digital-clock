; Inno Setup 6 script. Build with:  iscc installer\TimeBridge.iss   (after publish.ps1)
[Setup]
AppId={{6B1F3C2A-4D5E-4F60-9A71-2C8E5B7D9E10}
AppName=TimeBridge
AppVersion=1.0.0
AppPublisher=TimeBridge
DefaultDirName={autopf}\TimeBridge
DefaultGroupName=TimeBridge
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
OutputDir=..\dist
OutputBaseFilename=TimeBridge-Setup-1.0.0
SetupIconFile=..\src\TimeBridge.App\Assets\app.ico
UninstallDisplayIcon={app}\TimeBridge.exe
Compression=lzma2
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
CloseApplications=yes

[Files]
Source: "..\dist\publish\TimeBridge.exe"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\TimeBridge"; Filename: "{app}\TimeBridge.exe"
Name: "{autodesktop}\TimeBridge"; Filename: "{app}\TimeBridge.exe"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; Flags: unchecked

[Run]
Filename: "{app}\TimeBridge.exe"; Description: "Launch TimeBridge"; Flags: nowait postinstall skipifsilent
