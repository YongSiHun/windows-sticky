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

[Setup]
AppId={{A3142C21-8A03-41D8-9EB3-17933227A441}
AppName=Sticky Note
AppVersion=1.0.0
DefaultDirName={localappdata}\Programs\StickyNote
DefaultGroupName=Sticky Note
PrivilegesRequired=lowest
MinVersion=6.1sp1
#if Platform == "x64"
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
#endif
OutputDir={#Output}
OutputBaseFilename=sticky_note_setup_{#Platform}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
DisableProgramGroupPage=yes
CloseApplications=no
RestartApplications=no
UninstallDisplayIcon={app}\StickyNote.exe

[Files]
Source: "{#Repository}\src\StickyNote\bin\{#Platform}\Release\StickyNote.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#Repository}\src\StickyNote\bin\{#Platform}\Release\StickyNote.exe.config"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#Runtime}"; DestName: "net48.exe"; Flags: dontcopy

[Icons]
Name: "{group}\Sticky Note"; Filename: "{app}\StickyNote.exe"
Name: "{group}\Uninstall Sticky Note"; Filename: "{uninstallexe}"

[Run]
Filename: "{app}\StickyNote.exe"; Description: "Start Sticky Note"; Flags: nowait postinstall skipifsilent; Check: HasFramework

[Code]
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
    Result := 'Restart Windows, then run this setup again.';
    Exit;
  end;
  if Code <> 0 then
  begin
    Result := Format('.NET Framework installation failed (%d). Windows 7 requires SP1 and current servicing/SHA-2 updates.', [Code]);
    Exit;
  end;
  if not HasFramework then Result := '.NET Framework 4.8 is unavailable. Restart Windows and retry setup.';
end;

// NET: embedded offline prerequisite; elevation only when needed.
function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  Code: Integer;
begin
  Result := '';
  if HasFramework then Exit;
  ExtractTemporaryFile('net48.exe');
  if not ShellExec('runas', ExpandConstant('{tmp}\net48.exe'), '/passive /norestart', '', SW_SHOW, ewWaitUntilTerminated, Code) then
  begin
    Result := 'Unable to start the bundled .NET Framework 4.8 installer.';
    Exit;
  end;
  Result := RuntimeResult(Code, NeedsRestart);
end;
