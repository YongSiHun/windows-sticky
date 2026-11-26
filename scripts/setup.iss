#ifndef Platform
  #error Platform required
#endif
#ifndef Repository
  #error Repository required
#endif
#ifndef Runtime
  #error Runtime required
#endif
#ifndef Output
  #error Output required
#endif
#ifndef AppVersion
  #error AppVersion required
#endif

[Setup]
AppId={{A3142C21-8A03-41D8-9EB3-17933227A441}
AppName=Windows Stricky
AppVersion={#AppVersion}
DefaultDirName={localappdata}\Programs\StickyNote
DefaultGroupName=Windows Stricky
PrivilegesRequired=lowest
MinVersion=6.1sp1
#if Platform == "x64"
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
#endif
OutputDir={#Output}
OutputBaseFilename=windows_stricky_setup_{#Platform}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ShowLanguageDialog=no
UsePreviousLanguage=no
LanguageDetectionMethod=uilanguage
SetupIconFile={#Repository}\src\StickyNote\Assets\App.ico
DisableProgramGroupPage=yes
CloseApplications=yes
CloseApplicationsFilter=StickyNote.exe
RestartApplications=no
UninstallDisplayIcon={app}\StickyNote.exe

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "korean"; MessagesFile: "compiler:Default.isl,{#Repository}\scripts\Korean.isl"

[CustomMessages]
english.LaunchSticky=Run Windows Stricky
korean.LaunchSticky=Windows Stricky 실행
english.StartupSticky=Start with Windows
korean.StartupSticky=Windows 시작 시 자동 실행
english.UninstallSticky=Uninstall Windows Stricky
korean.UninstallSticky=Windows Stricky 제거
english.RuntimeRestart=Restart Windows, then run this setup again.
korean.RuntimeRestart=Windows를 다시 시작한 뒤 이 설치 파일을 다시 실행해 주세요.
english.RuntimeFailed=.NET Framework installation failed (code %d). Windows 7 requires SP1 and servicing/SHA-2 updates.
korean.RuntimeFailed=.NET Framework 설치에 실패했습니다 (오류 %d). Windows 7은 SP1 및 서비스/SHA-2 업데이트가 필요합니다.
english.RuntimeMissing=.NET Framework 4.8 was not found. Restart Windows and retry setup.
korean.RuntimeMissing=.NET Framework 4.8을 확인할 수 없습니다. Windows를 다시 시작한 뒤 설치를 다시 실행해 주세요.
english.RuntimeLaunchFailed=Unable to start the bundled .NET Framework 4.8 installer.
korean.RuntimeLaunchFailed=포함된 .NET Framework 4.8 설치 파일을 실행하지 못했습니다.

[Files]
Source: "{#Repository}\src\StickyNote\bin\{#Platform}\Release\StickyNote.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#Repository}\src\StickyNote\bin\{#Platform}\Release\StickyNote.exe.config"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#Runtime}"; DestName: "net48.exe"; Flags: dontcopy

[Icons]
Name: "{userprograms}\Windows Stricky"; Filename: "{app}\StickyNote.exe"; WorkingDir: "{app}"
Name: "{userdesktop}\Windows Stricky"; Filename: "{app}\StickyNote.exe"; WorkingDir: "{app}"
Name: "{group}\{cm:UninstallSticky}"; Filename: "{uninstallexe}"

[InstallDelete]
Type: files; Name: "{userprograms}\Sticky Note\Sticky Note.lnk"
Type: files; Name: "{userprograms}\Sticky Note\Sticky Note 제거.lnk"
Type: files; Name: "{userprograms}\Sticky Note\Uninstall Sticky Note.lnk"

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: none; ValueName: "StickyNote"; Flags: uninsdeletevalue
Root: HKCU32; Subkey: "Software\StickyNote"; ValueType: none; ValueName: "InstallStartupEnabled"; Flags: uninsdeletevalue

[Run]
Filename: "{app}\StickyNote.exe"; Description: "{cm:LaunchSticky}"; Flags: nowait postinstall skipifsilent; Check: HasFramework

[Code]
var
  StartupCheck: TNewCheckBox;
  StartupPositioned: Boolean;

procedure InitializeWizard;
begin
  StartupCheck := TNewCheckBox.Create(WizardForm);
  StartupCheck.Parent := WizardForm.FinishedPage;
  StartupCheck.Caption := CustomMessage('StartupSticky');
  StartupCheck.Checked := True;
  StartupCheck.Visible := False;
end;

procedure CurPageChanged(CurPageID: Integer);
begin
  if CurPageID <> wpFinished then Exit;
  if not StartupPositioned then
  begin
    StartupCheck.SetBounds(WizardForm.RunList.Left, WizardForm.RunList.Top,
      WizardForm.RunList.Width, ScaleY(24));
    WizardForm.RunList.Top := WizardForm.RunList.Top + ScaleY(28);
    WizardForm.RunList.Height := WizardForm.RunList.Height - ScaleY(28);
    StartupPositioned := True;
  end;
  StartupCheck.Visible := True;
end;

// SETUP: pass the finished-page choice to the app without editing JSON in Pascal.
procedure SaveStartupChoice;
var
  Enabled: Cardinal;
begin
  Enabled := 0;
  if StartupCheck.Checked then Enabled := 1;
  if not RegWriteDWordValue(HKCU32, 'Software\StickyNote', 'InstallStartupEnabled', Enabled) then
    RaiseException('Unable to save startup preference.');
  if StartupCheck.Checked then
  begin
    if not RegWriteStringValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run',
      'StickyNote', '"' + ExpandConstant('{app}\StickyNote.exe') + '"') then
      RaiseException('Unable to register startup.');
  end
  else
    RegDeleteValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'StickyNote');
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  // Silent installs and interrupted finished pages retain the checked default.
  if CurStep = ssPostInstall then SaveStartupChoice;
end;

function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;
  if CurPageID = wpFinished then SaveStartupChoice;
end;

// NET: 4.8 or newer; 32-bit registry view works on x86/x64.
function HasFramework: Boolean;
var
  Release: Cardinal;
begin
  Result := False;
  if not RegQueryDWordValue(HKLM32, 'SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full', 'Release', Release) then Exit;
  Result := Release >= 528040;
end;

// RC: separate success, restart and failure outcomes.
function RuntimeResult(Code: Integer; var NeedsRestart: Boolean): String;
begin
  Result := '';
  if (Code = 3010) or (Code = 1641) then
  begin
    NeedsRestart := True;
    Result := CustomMessage('RuntimeRestart');
    Exit;
  end;
  if Code <> 0 then
  begin
    Result := Format(CustomMessage('RuntimeFailed'), [Code]);
    Exit;
  end;
  if not HasFramework then Result := CustomMessage('RuntimeMissing');
end;

// NET: embedded offline prerequisite; elevation only when needed.
function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  Code: Integer;
begin
  Result := '';
  if HasFramework then Exit;
  ExtractTemporaryFile('net48.exe');
  if not ShellExec('runas', ExpandConstant('{tmp}\net48.exe'), '/q /norestart', '', SW_HIDE, ewWaitUntilTerminated, Code) then
  begin
    Result := CustomMessage('RuntimeLaunchFailed');
    Exit;
  end;
  Result := RuntimeResult(Code, NeedsRestart);
end;
