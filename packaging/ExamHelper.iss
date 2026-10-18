#ifndef AppVersion
 #define AppVersion "0.4.0"
#endif
#ifndef PublishDir
 #error PublishDir must be provided by Build-Installer.ps1
#endif
#ifndef InstallerOutput
 #error InstallerOutput must be provided by Build-Installer.ps1
#endif
[Setup]
AppId={{A0E88B39-9B45-490D-A4A0-C5C731A40400}
AppName=ExamHelper
AppVersion={#AppVersion}
DefaultDirName={localappdata}\Programs\ExamHelper-0.4
DefaultGroupName=ExamHelper 0.4
PrivilegesRequired=lowest
ArchitecturesAllowed=x64os
ArchitecturesInstallIn64BitMode=x64os
MinVersion=10.0.22000
OutputDir={#InstallerOutput}
OutputBaseFilename=ExamHelper-Setup-{#AppVersion}-x64
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
SetupIconFile=../src/ExamHelper.App/Assets/app-icon.ico
DisableProgramGroupPage=yes
UninstallDisplayIcon={app}\ExamHelper.App.exe
CloseApplications=yes
RestartApplications=no
[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Excludes: "*.pdb"; Flags: ignoreversion recursesubdirs createallsubdirs
[Icons]
Name: "{userprograms}\ExamHelper 0.4"; Filename: "{app}\ExamHelper.App.exe"; AppUserModelID: "ExamHelper.Desktop.0.4"
[Run]
Filename: "{app}\ExamHelper.App.exe"; Description: "启动应用"; Flags: nowait postinstall skipifsilent
[Code]
#include "ShortcutOwnership.iss"
#include "FinishedPageOptions.iss"
function InitializeSetup(): Boolean;
var
  Version: TWindowsVersion;
begin
  GetWindowsVersionEx(Version);
  Result := (Version.Major >= 10) and (Version.Build >= 22000) and (Version.ProductType = VER_NT_WORKSTATION);
  if not Result then
    MsgBox('ExamHelper requires Windows 11 x64 workstation.', mbError, MB_OK);
end;

procedure InitializeWizard;
begin
 InitializeFinishedOptions();
end;
procedure CurPageChanged(CurPageID: Integer);
begin
 ShowFinishedOptions(CurPageID);
end;
function NextButtonClick(CurPageID: Integer): Boolean;
begin
 Result := True;
 if CurPageID=wpFinished then Result := CommitFinishedOptions();
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usUninstall then
    RemoveOwnedDesktopShortcut(ExpandConstant('{userdesktop}\ExamHelper.lnk'),'Software\ExamHelper\Installer\0.4');
end;
