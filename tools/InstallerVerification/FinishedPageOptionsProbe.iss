#ifndef VerificationOutput
 #error VerificationOutput must be an isolated test directory
#endif
#define FinishedDesktopPath VerificationOutput + "\finish-probe\ExamHelper.lnk"
#define FinishedOwnerKey "Software\ExamHelper\TestOnly\FinishedOptionsProbe04"
#define FinishedAppDirectory VerificationOutput + "\finish-probe"
#define FinishedOptionsProbe
[Setup]
AppName=ExamHelper Finish Options Probe
AppVersion=1
DefaultDirName={#VerificationOutput}\finish-probe
PrivilegesRequired=lowest
Uninstallable=no
CreateAppDir=no
OutputDir={#VerificationOutput}
OutputBaseFilename=FinishedPageOptionsProbe
[Run]
Filename: "{app}\owned-not-run.exe"; Description: "启动应用"; Flags: nowait postinstall skipifsilent
[Code]
#include "../../packaging/ShortcutOwnership.iss"
#include "../../packaging/FinishedPageOptions.iss"
procedure Check(Value: Boolean; MessageText: String);
begin
 if not Value then RaiseException(MessageText);
end;
procedure InitializeWizard;
var
 Folder, Link, OwnerKey: String;
 I, J: Integer;
 Outcome: Boolean;
begin
 Folder := '{#VerificationOutput}\finish-probe'; ForceDirectories(Folder);
 Link := '{#FinishedDesktopPath}'; OwnerKey := '{#FinishedOwnerKey}';
 try
  InitializeFinishedOptions();
  if WizardForm.RunList.Items.Count = 0 then WizardForm.RunList.AddCheckBox('launch','',0,True,True,False,True,nil);
  ShowFinishedOptions(wpFinished);
  Check(LaunchApplicationBox.Checked and DesktopShortcutBox.Checked,'both options must default checked');
  Check(LaunchApplicationBox.Left=DesktopShortcutBox.Left,'left alignment');
  Check(LaunchApplicationBox.Width=DesktopShortcutBox.Width,'width alignment');
  Check(LaunchApplicationBox.Height=DesktopShortcutBox.Height,'height alignment');
  Check(LaunchApplicationBox.Font.Height=DesktopShortcutBox.Font.Height,'same font');
  Check(DesktopShortcutBox.Top >= LaunchApplicationBox.Top+LaunchApplicationBox.Height,'rows overlap');
  Check(not WizardForm.RunList.Visible,'native checklist must be hidden');
  for I := 0 to 1 do
  begin
   if I=0 then J:=120 else J:=192;
   LaunchApplicationBox.Font.PixelsPerInch:=J;DesktopShortcutBox.Font.PixelsPerInch:=J;
   LayoutFinishedOptions(20,40,600*J div 96,24*J div 96,8*J div 96);
   Check(LaunchApplicationBox.Left=DesktopShortcutBox.Left,'scaled left alignment');
   Check(LaunchApplicationBox.Height=DesktopShortcutBox.Height,'scaled height alignment');
   Check(LaunchApplicationBox.Font.Height=DesktopShortcutBox.Font.Height,'scaled font alignment');
   Check(DesktopShortcutBox.Top>=LaunchApplicationBox.Top+LaunchApplicationBox.Height,'scaled rows overlap');
  end;
  LaunchApplicationBox.Checked := False; DesktopShortcutBox.Checked := False;
  ShowFinishedOptions(wpWelcome); ShowFinishedOptions(wpFinished);
  Check(not LaunchApplicationBox.Checked and not DesktopShortcutBox.Checked,'redisplay must preserve cancelled choices');
  for I := 0 to 1 do
   for J := 0 to 1 do
   begin
    if FileExists(Link) then DeleteFile(Link);
    RegDeleteValue(HKCU,OwnerKey,'DesktopShortcutSHA256');
    LaunchApplicationBox.Checked := I=1; DesktopShortcutBox.Checked := J=1;
    Outcome := CommitFinishedOptions();Check(Outcome,'commit must succeed: '+FinishedOptionsError);
    Check(WizardForm.RunList.Checked[0]=(I=1),'native launch choice');
    Check(FileExists(Link)=(J=1),'desktop choice');
   end;
  RemoveOwnedDesktopShortcut(Link,OwnerKey);
  SaveStringToFile(Link,'unrelated owned fixture',False);
  LaunchApplicationBox.Checked := True; DesktopShortcutBox.Checked := True; WizardForm.RunList.Checked[0] := False;
  Check(not CommitFinishedOptions(),'unowned shortcut must fail safely');
  Check(not WizardForm.RunList.Checked[0],'failed shortcut must not schedule launch');
  Check(DesktopShortcutBox.Checked,'failure preserves choice for cancellation or retry');
  DesktopShortcutBox.Checked := False;Check(CommitFinishedOptions(),'unchecked retry must finish');
  SaveStringToFile(Folder+'\result.json','{"Passed":true,"Aligned":true,"BothDefaultChecked":true,"FourCombinations":true,"RedisplayPreserved":true,"FailureBeforeLaunch":true,"NativeControls":true,"Simulated125200Bounds":true}',False);
 except
  SaveStringToFile(Folder+'\result.json','{"Passed":false,"Error":"'+GetExceptionMessage()+'"}',False);
 end;
 RegDeleteValue(HKCU,OwnerKey,'DesktopShortcutSHA256');RegDeleteKeyIfEmpty(HKCU,OwnerKey);
 Abort;
end;
