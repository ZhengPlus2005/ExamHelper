#ifndef AppVersion
 #define AppVersion "0.1.0"
#endif
#ifndef PublishDir
 #error PublishDir must be provided by Build-Installer.ps1
#endif
#ifndef InstallerOutput
 #error InstallerOutput must be provided by Build-Installer.ps1
#endif
[Setup]
AppId={{16DDFB45-796E-49D7-8F8A-295BDBF194C1}
AppName=ExamHelper
AppVersion={#AppVersion}
DefaultDirName={localappdata}\Programs\ExamHelper
DefaultGroupName=ExamHelper
PrivilegesRequired=lowest
ArchitecturesAllowed=x64os
ArchitecturesInstallIn64BitMode=x64os
MinVersion=10.0.22000
OutputDir={#InstallerOutput}
OutputBaseFilename=ExamHelper-Setup-{#AppVersion}-x64
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
DisableProgramGroupPage=yes
UninstallDisplayIcon={app}\ExamHelper.App.exe
CloseApplications=yes
RestartApplications=no
[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Excludes: "*.pdb"; Flags: ignoreversion recursesubdirs createallsubdirs
[Icons]
Name: "{userprograms}\ExamHelper"; Filename: "{app}\ExamHelper.App.exe"
[Run]
Filename: "{app}\ExamHelper.App.exe"; Description: "Launch ExamHelper"; Flags: nowait postinstall skipifsilent
[Code]
function InitializeSetup(): Boolean;
var
  Version: TWindowsVersion;
begin
  GetWindowsVersionEx(Version);
  Result := (Version.Major >= 10) and (Version.Build >= 22000) and (Version.ProductType = VER_NT_WORKSTATION);
  if not Result then
    MsgBox('ExamHelper requires Windows 11 x64 workstation.', mbError, MB_OK);
end;
