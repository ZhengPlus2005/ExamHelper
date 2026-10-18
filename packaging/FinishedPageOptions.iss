#ifndef FinishedDesktopPath
 #define FinishedDesktopPath "{userdesktop}\ExamHelper.lnk"
#endif
#ifndef FinishedAppDirectory
 #define FinishedAppDirectory "{app}"
#endif
#ifndef FinishedOwnerKey
 #define FinishedOwnerKey "Software\ExamHelper\Installer\0.4"
#endif
var
 LaunchApplicationBox, DesktopShortcutBox: TNewCheckBox;
 FinishedOptionsError: String;
procedure InitializeFinishedOptions();
begin
 LaunchApplicationBox := TNewCheckBox.Create(WizardForm);
 LaunchApplicationBox.Parent := WizardForm.FinishedPage;
 LaunchApplicationBox.Caption := '启动应用';
 LaunchApplicationBox.Font.Assign(WizardForm.Font);
 LaunchApplicationBox.Checked := True;
 LaunchApplicationBox.Visible := False;
 DesktopShortcutBox := TNewCheckBox.Create(WizardForm);
 DesktopShortcutBox.Parent := WizardForm.FinishedPage;
 DesktopShortcutBox.Caption := '添加快捷方式到桌面';
 DesktopShortcutBox.Font.Assign(WizardForm.Font);
 DesktopShortcutBox.Checked := True;
 DesktopShortcutBox.Visible := False;
end;
procedure LayoutFinishedOptions(Left, Top, Width, RowHeight, Gap: Integer);
begin
 LaunchApplicationBox.SetBounds(Left,Top,Width,RowHeight);
 DesktopShortcutBox.SetBounds(Left,Top+RowHeight+Gap,Width,RowHeight);
end;
procedure ShowFinishedOptions(PageId: Integer);
begin
 WizardForm.RunList.Visible := False;
 LayoutFinishedOptions(WizardForm.RunList.Left,WizardForm.RunList.Top,WizardForm.RunList.Width,ScaleY(24),ScaleY(8));
 LaunchApplicationBox.Visible := (PageId=wpFinished) and not WizardSilent;
 DesktopShortcutBox.Visible := LaunchApplicationBox.Visible;
end;
function CommitFinishedOptions(): Boolean;
var
 Link, OwnerKey, AppDirectory: String;
begin
 Result := True;
 if WizardSilent then exit;
 if WizardForm.RunList.Items.Count>0 then WizardForm.RunList.Checked[0] := False;
 try
  if DesktopShortcutBox.Checked then
  begin
   Link := ExpandConstant('{#FinishedDesktopPath}'); OwnerKey := '{#FinishedOwnerKey}';
   if not CanReplaceDesktopShortcut(Link,OwnerKey) then
    RaiseException('桌面已有其他或已被修改的同名快捷方式，已保留原文件。');
   AppDirectory := ExpandConstant('{#FinishedAppDirectory}');
   CreateShellLink(Link,'ExamHelper',AppDirectory+'\ExamHelper.App.exe','',AppDirectory,AppDirectory+'\ExamHelper.App.exe',0,SW_SHOWNORMAL);
   RecordDesktopShortcutOwnership(Link,OwnerKey);
  end;
  if WizardForm.RunList.Items.Count>0 then WizardForm.RunList.Checked[0] := LaunchApplicationBox.Checked;
 except
  FinishedOptionsError := GetExceptionMessage();
  #ifndef FinishedOptionsProbe
  MsgBox('未能创建桌面快捷方式：'+FinishedOptionsError+#13#10+'可以取消勾选后再次点击完成。',mbError,MB_OK);
  #endif
  Result := False;
 end;
end;
