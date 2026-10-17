// A matching content hash proves this exact link was last written by this installer.
// Existing or subsequently edited links belong to the user and must be preserved.
function CanReplaceDesktopShortcut(FileName, OwnerKey: String): Boolean;
var
  ExpectedHash: String;
begin
  Result := not FileExists(FileName);
  if not Result then
    if RegQueryStringValue(HKCU,OwnerKey,'DesktopShortcutSHA256',ExpectedHash) then
      Result := CompareText(ExpectedHash,GetSHA256OfFile(FileName)) = 0;
end;

procedure RecordDesktopShortcutOwnership(FileName, OwnerKey: String);
begin
  if not RegWriteStringValue(HKCU,OwnerKey,'DesktopShortcutSHA256',GetSHA256OfFile(FileName)) then
    RaiseException('无法记录桌面快捷方式归属。');
end;

procedure RemoveOwnedDesktopShortcut(FileName, OwnerKey: String);
begin
  if FileExists(FileName) and CanReplaceDesktopShortcut(FileName,OwnerKey) then
    DeleteFile(FileName);
  RegDeleteValue(HKCU,OwnerKey,'DesktopShortcutSHA256');
  RegDeleteKeyIfEmpty(HKCU,OwnerKey);
end;
