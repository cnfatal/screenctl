[Setup]
AppId=cnfatal.Screenctl
AppName=Screenctl
AppVersion={#AppVersion}
AppPublisher=cnfatal
AppPublisherURL=https://github.com/cnfatal/screenctl
DefaultDirName={autopf}\Screenctl
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.19041
SetupIconFile=..\Assets\Screenctl.ico
UninstallDisplayIcon={app}\Screenctl.exe
OutputDir=..\artifacts
OutputBaseFilename=Screenctl-{#AppVersion}-win-x64-Setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern

[Files]
Source: "..\artifacts\Screenctl\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\Screenctl"; Filename: "{app}\Screenctl.exe"

[Run]
Filename: "{app}\Screenctl.exe"; Flags: postinstall nowait skipifsilent
