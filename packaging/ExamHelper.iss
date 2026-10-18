#ifndef AppVersion
 #define AppVersion "0.3.0"
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
Name: "{userprograms}\ExamHelper"; Filename: "{app}\ExamHelper.App.exe"
[Run]
Filename: "{app}\ExamHelper.App.exe"; Description: "Launch ExamHelper"; Flags: nowait postinstall skipifsilent
[Code]
#include "ShortcutOwnership.iss"
function InitializeSetup(): Boolean;
var
  Version: TWindowsVersion;
begin
  GetWindowsVersionEx(Version);
  Result := (Version.Major >= 10) and (Version.Build >= 22000) and (Version.ProductType = VER_NT_WORKSTATION);
  if not Result then
    MsgBox('ExamHelper requires Windows 11 x64 workstation.', mbError, MB_OK);
end;

var
  DesktopShortcutBox: TNewCheckBox;

procedure InitializeWizard;
begin
  DesktopShortcutBox := TNewCheckBox.Create(WizardForm);
  DesktopShortcutBox.Parent := WizardForm.FinishedPage;
  DesktopShortcutBox.Left := WizardForm.RunList.Left;
  DesktopShortcutBox.Width := WizardForm.RunList.Width;
  DesktopShortcutBox.Height := ScaleY(24);
  DesktopShortcutBox.Caption := '添加快捷方式到桌面';
  DesktopShortcutBox.Checked := False;
  DesktopShortcutBox.Visible := False;
end;

procedure CurPageChanged(CurPageID: Integer);
begin
  DesktopShortcutBox.Visible := (CurPageID = wpFinished) and not WizardSilent;
  if CurPageID = wpFinished then
  begin
    DesktopShortcutBox.Top := WizardForm.RunList.Top + ScaleY(34);
  end;
end;

function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;
  if (CurPageID = wpFinished) and not WizardSilent and DesktopShortcutBox.Checked then
  begin
    try
      if not CanReplaceDesktopShortcut(ExpandConstant('{userdesktop}\ExamHelper.lnk'),'Software\ExamHelper\Installer') then
        RaiseException('桌面已有其他或已被修改的同名快捷方式，已保留原文件。');
      CreateShellLink(
        ExpandConstant('{userdesktop}\ExamHelper.lnk'),
        'ExamHelper',
        ExpandConstant('{app}\ExamHelper.App.exe'),
        '',
        ExpandConstant('{app}'),
        ExpandConstant('{app}\ExamHelper.App.exe'),
        0, SW_SHOWNORMAL);
      RecordDesktopShortcutOwnership(ExpandConstant('{userdesktop}\ExamHelper.lnk'),'Software\ExamHelper\Installer');
    except
      MsgBox('未能创建桌面快捷方式：' + GetExceptionMessage() + #13#10 +
        '可以取消勾选后再次点击完成。', mbError, MB_OK);
      Result := False;
    end;
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usUninstall then
    RemoveOwnedDesktopShortcut(ExpandConstant('{userdesktop}\ExamHelper.lnk'),'Software\ExamHelper\Installer');
end;
