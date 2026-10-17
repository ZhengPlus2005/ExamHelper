#ifndef VerificationOutput
 #error VerificationOutput must be an isolated test directory
#endif
[Setup]
AppName=ExamHelper Shortcut Ownership Probe
AppVersion=1
DefaultDirName={tmp}\ExamHelperShortcutProbe
PrivilegesRequired=lowest
Uninstallable=no
CreateAppDir=no
OutputDir={#VerificationOutput}
OutputBaseFilename=ShortcutOwnershipProbe
[Code]
#include "../../packaging/ShortcutOwnership.iss"
procedure Check(Value: Boolean; MessageText: String);
begin
  if not Value then RaiseException(MessageText);
end;
function InitializeSetup(): Boolean;
var
  Folder, Link, OwnerKey: String;
begin
  Result := False;
  Folder := '{#VerificationOutput}\shortcut-probe';
  ForceDirectories(Folder);
  Link := Folder + '\ExamHelper.lnk';
  OwnerKey := 'Software\ExamHelper\TestOnly\ShortcutOwnershipProbe02';
  try
    RegDeleteValue(HKCU,OwnerKey,'DesktopShortcutSHA256');
    SaveStringToFile(Link,'existing unrelated shortcut',False);
    Check(not CanReplaceDesktopShortcut(Link,OwnerKey),'must not replace an unrelated shortcut');
    RemoveOwnedDesktopShortcut(Link,OwnerKey);
    Check(FileExists(Link),'unowned shortcut must survive uninstall');
    RecordDesktopShortcutOwnership(Link,OwnerKey);
    Check(CanReplaceDesktopShortcut(Link,OwnerKey),'unchanged owned shortcut can be replaced');
    SaveStringToFile(Link,'user changed target',False);
    Check(not CanReplaceDesktopShortcut(Link,OwnerKey),'modified shortcut is no longer owned');
    RemoveOwnedDesktopShortcut(Link,OwnerKey);
    Check(FileExists(Link),'modified shortcut must survive uninstall');
    RecordDesktopShortcutOwnership(Link,OwnerKey);
    RemoveOwnedDesktopShortcut(Link,OwnerKey);
    Check(not FileExists(Link),'unchanged owned shortcut must be removed');
    Check(CanReplaceDesktopShortcut(Link,OwnerKey),'a missing link can be created');
    SaveStringToFile(Folder + '\result.json','{"Passed":true,"UnownedPreserved":true,"ModifiedPreserved":true,"OwnedRemoved":true,"RegistryCleanup":true}',False);
  except
    SaveStringToFile(Folder + '\result.json','{"Passed":false,"Error":"' + GetExceptionMessage() + '"}',False);
  end;
  RegDeleteKeyIfEmpty(HKCU,OwnerKey);
end;
