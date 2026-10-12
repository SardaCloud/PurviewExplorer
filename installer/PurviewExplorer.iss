; Copyright (C) 2026 Francesco Valerio Buccoli. Sarda Cloud Community.
; Licensed under the MIT License. See LICENSE in the repository root.
;
; Inno Setup script for Purview Explorer. Build it with build.ps1 -Setup, or:
;   ISCC.exe /DAppVersion=0.2.0 /DBinDir=<folder with PurviewExplorer.exe> installer\PurviewExplorer.iss
;
; Setup checks the requirements (Windows, .NET Framework 4.8, Windows PowerShell 5.1 and the
; ExchangeOnlineManagement module), can install the module from the PowerShell Gallery, and after
; installing runs "PurviewExplorer.exe /CheckPrerequisites" to confirm the module loads in the application.

#define AppName "Purview Explorer"
#define AppExe "PurviewExplorer.exe"
#define AppPublisher "Sarda Cloud Community"
#define AppUrl "https://github.com/SardaCloud/PurviewExplorer"

#ifndef AppVersion
  #define VersionFile FileOpen(AddBackslash(SourcePath) + "..\VERSION")
  #define AppVersion Trim(FileRead(VersionFile))
  #expr FileClose(VersionFile)
#endif
#ifndef BinDir
  #define BinDir AddBackslash(SourcePath) + "..\src\PurviewExplorer\bin\Release\net48"
#endif

[Setup]
AppId={{C8A6D82E-0CDD-424C-8ADC-1A8BD8EC3918}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppUrl}
AppSupportURL={#AppUrl}/issues
AppUpdatesURL={#AppUrl}/releases
AppCopyright=Copyright (C) 2026 Francesco Valerio Buccoli
VersionInfoVersion={#AppVersion}
VersionInfoCompany={#AppPublisher}
VersionInfoDescription={#AppName} Setup
VersionInfoProductName={#AppName}
VersionInfoProductVersion={#AppVersion}
VersionInfoCopyright=Copyright (C) 2026 Francesco Valerio Buccoli

; Installs for the current user without elevation by default; "Install for all users" is offered.
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog commandline
DefaultDirName={autopf}\Sarda Cloud\{#AppName}
DisableProgramGroupPage=yes
ArchitecturesInstallIn64BitMode=x64compatible
; Windows 10 version 1607 / Windows Server 2016, the oldest systems .NET Framework 4.8 supports.
MinVersion=10.0.14393

LicenseFile=..\LICENSE
DisableWelcomePage=no
WizardStyle=modern
WizardImageFile=images\wizard-202x386.png,images\wizard-269x515.png,images\wizard-336x643.png,images\wizard-403x772.png,images\wizard-430x824.png,images\wizard-498x953.png,images\wizard-534x1022.png
WizardSmallImageFile=images\wizard-small-58.png,images\wizard-small-77.png,images\wizard-small-97.png,images\wizard-small-116.png,images\wizard-small-124.png,images\wizard-small-143.png,images\wizard-small-159.png
SetupIconFile=..\src\PurviewExplorer\Resources\PurviewExplorer.ico
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName}

; The application creates these mutexes, so Setup and Uninstall can ask to close it first.
AppMutex=SardaCloud.PurviewExplorer,Global\SardaCloud.PurviewExplorer
SetupLogging=yes
ShowLanguageDialog=no

OutputDir=..\artifacts
OutputBaseFilename=PurviewExplorer-{#AppVersion}-setup
Compression=lzma2/max
SolidCompression=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Messages]
WelcomeLabel2=This will install [name/ver] on your computer.%n%nPurview Explorer shows your Microsoft Purview configuration, such as sensitivity labels, DLP and retention policies, in a classic Windows application. It's read-only: it never changes anything in your tenant.%n%nSetup checks that this computer has everything Purview Explorer needs, and can install the ExchangeOnlineManagement PowerShell module for you.
BeveledLabel=Sarda Cloud Community

[Tasks]
Name: "installmodule"; Description: "Install the ExchangeOnlineManagement module from the PowerShell Gallery (needs internet access)"; GroupDescription: "PowerShell module:"; Check: ModuleNeedsInstall
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#BinDir}\{#AppExe}"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#BinDir}\{#AppExe}.config"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\LICENSE"; DestDir: "{app}"; DestName: "LICENSE.txt"; Flags: ignoreversion
Source: "..\THIRD-PARTY-NOTICES.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\README.md"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"; Comment: "View Microsoft Purview configuration"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Comment: "View Microsoft Purview configuration"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent

[Code]
const
  ModuleName = 'ExchangeOnlineManagement';
  MinModuleVersion = '3.2.0';
  NetFxUrl = 'https://dotnet.microsoft.com/download/dotnet-framework/net48';
  ManualInstallCommand = 'Install-Module ExchangeOnlineManagement -Scope CurrentUser';
  { TColor is $00BBGGRR }
  ColorOk = $00107C10;
  ColorMissing = $001C2BC4;
  ColorAction = $0000538A;

type
  TRequirementRow = record
    NameLabel: TNewStaticText;
    StatusLabel: TNewStaticText;
    DetailLabel: TNewStaticText;
  end;

var
  RequirementsPage: TWizardPage;
  VerifyPage: TWizardPage;
  VerifyCaption: TNewStaticText;
  VerifyMemo: TNewMemo;
  Rows: array[0..3] of TRequirementRow;
  SummaryLabel: TNewLinkLabel;
  CheckAgainButton: TNewButton;

  WindowsOk, NetFxOk, PowerShellOk: Boolean;
  WindowsText, NetFxText, PowerShellText: String;
  ModuleVersion, ModuleLocation: String;

  ModuleInstallRan, ModuleInstallFailed: Boolean;
  ModuleInstallLog: String;
  VerificationRan, VerificationPassed: Boolean;
  VerificationReport: String;

{ ---------------------------------------------------------------- versions }

function NextVersionPart(var S: String): Integer;
var
  P: Integer;
begin
  P := Pos('.', S);
  if P = 0 then
  begin
    Result := StrToIntDef(S, 0);
    S := '';
  end
  else
  begin
    Result := StrToIntDef(Copy(S, 1, P - 1), 0);
    S := Copy(S, P + 1, Length(S));
  end;
end;

{ Compares dotted versions numerically: -1, 0 or 1. }
function CompareVersions(A, B: String): Integer;
var
  I, X, Y: Integer;
begin
  Result := 0;
  for I := 1 to 4 do
  begin
    X := NextVersionPart(A);
    Y := NextVersionPart(B);
    if X <> Y then
    begin
      if X < Y then Result := -1 else Result := 1;
      Exit;
    end;
  end;
end;

function IsVersionString(const S: String): Boolean;
var
  I: Integer;
begin
  Result := S <> '';
  for I := 1 to Length(S) do
    if not (((S[I] >= '0') and (S[I] <= '9')) or (S[I] = '.')) then
    begin
      Result := False;
      Exit;
    end;
end;

{ ---------------------------------------------------------------- detection }

procedure SearchModuleIn(const Base: String);
var
  FindRec: TFindRec;
  Dir: String;
begin
  Dir := AddBackslash(Base) + ModuleName;
  if FindFirst(Dir + '\*', FindRec) then
  try
    repeat
      if (FindRec.Attributes and FILE_ATTRIBUTE_DIRECTORY <> 0) and IsVersionString(FindRec.Name) and
         FileExists(Dir + '\' + FindRec.Name + '\' + ModuleName + '.psd1') then
        if (ModuleVersion = '') or (CompareVersions(FindRec.Name, ModuleVersion) > 0) then
        begin
          ModuleVersion := FindRec.Name;
          ModuleLocation := Dir + '\' + FindRec.Name;
        end;
    until not FindNext(FindRec);
  finally
    FindClose(FindRec);
  end;
end;

{ The same folders Purview Explorer searches: Windows PowerShell and PowerShell 7, per user and for all users. }
procedure DetectModule;
var
  Paths, P: String;
  I: Integer;
begin
  ModuleVersion := '';
  ModuleLocation := '';
  SearchModuleIn(ExpandConstant('{userdocs}\WindowsPowerShell\Modules'));
  SearchModuleIn(ExpandConstant('{userdocs}\PowerShell\Modules'));
  SearchModuleIn(ExpandConstant('{commonpf}\WindowsPowerShell\Modules'));
  SearchModuleIn(ExpandConstant('{commonpf}\PowerShell\Modules'));
  Paths := GetEnv('PSModulePath');
  while Paths <> '' do
  begin
    I := Pos(';', Paths);
    if I = 0 then
    begin
      P := Paths;
      Paths := '';
    end
    else
    begin
      P := Copy(Paths, 1, I - 1);
      Paths := Copy(Paths, I + 1, Length(Paths));
    end;
    if Trim(P) <> '' then
      SearchModuleIn(Trim(P));
  end;
end;

function ModuleOk: Boolean;
begin
  Result := (ModuleVersion <> '') and (CompareVersions(ModuleVersion, MinModuleVersion) >= 0);
end;

{ Check function of the "installmodule" task. }
function ModuleNeedsInstall: Boolean;
begin
  Result := not ModuleOk;
end;

function HardRequirementsMet: Boolean;
begin
  Result := WindowsOk and NetFxOk and PowerShellOk;
end;

procedure DetectSystem;
var
  V: TWindowsVersion;
  Name, Display, S: String;
begin
  GetWindowsVersionEx(V);
  WindowsOk := (V.Major > 10) or ((V.Major = 10) and (V.Build >= 14393));
  if not RegQueryStringValue(HKLM, 'SOFTWARE\Microsoft\Windows NT\CurrentVersion', 'ProductName', Name) then
    Name := 'Windows';
  { ProductName still says "Windows 10" on Windows 11; the build number tells them apart. }
  if (V.Build >= 22000) and (Pos('Windows 10', Name) = 1) then
    Name := 'Windows 11' + Copy(Name, 11, Length(Name));
  if RegQueryStringValue(HKLM, 'SOFTWARE\Microsoft\Windows NT\CurrentVersion', 'DisplayVersion', Display) then
    Name := Name + ', version ' + Display;
  WindowsText := Format('%s (build %d)', [Name, V.Build]);

  NetFxOk := IsDotNetInstalled(net48, 0);
  if RegQueryStringValue(HKLM, 'SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full', 'Version', S) then
    NetFxText := 'Version ' + S
  else
    NetFxText := 'Not installed';
  if not NetFxOk then
    NetFxText := NetFxText + '. Install .NET Framework 4.8 or later, then run Setup again.';

  PowerShellOk := RegQueryStringValue(HKLM, 'SOFTWARE\Microsoft\PowerShell\3\PowerShellEngine', 'PowerShellVersion', S) and
    (CompareVersions(S, '5.1') >= 0) and FileExists(ExpandConstant('{sys}\WindowsPowerShell\v1.0\powershell.exe'));
  if PowerShellOk then
    PowerShellText := 'Version ' + S
  else
    PowerShellText := 'Not found. Windows PowerShell 5.1 is part of Windows; make sure it hasn''t been removed.';

  DetectModule;
end;

function MissingRequirementsText: String;
begin
  Result := '';
  if not WindowsOk then Result := Result + '- Windows 10 version 1607, Windows Server 2016 or later' + #13#10;
  if not NetFxOk then Result := Result + '- .NET Framework 4.8 or later (' + NetFxUrl + ')' + #13#10;
  if not PowerShellOk then Result := Result + '- Windows PowerShell 5.1' + #13#10;
  Result := '{#AppName} needs:' + #13#10 + Result;
end;

{ ---------------------------------------------------------------- requirements page }

procedure SetStatus(Row: Integer; const Status: String; Color: TColor);
begin
  Rows[Row].StatusLabel.Caption := Status;
  Rows[Row].StatusLabel.Font.Color := Color;
end;

procedure LayoutRequirements;
var
  I, Y: Integer;
  ModuleDetail: String;
begin
  if WindowsOk then SetStatus(0, 'OK', ColorOk) else SetStatus(0, 'Too old', ColorMissing);
  Rows[0].DetailLabel.Caption := WindowsText;
  if NetFxOk then SetStatus(1, 'OK', ColorOk) else SetStatus(1, 'Missing', ColorMissing);
  Rows[1].DetailLabel.Caption := NetFxText;
  if PowerShellOk then SetStatus(2, 'OK', ColorOk) else SetStatus(2, 'Missing', ColorMissing);
  Rows[2].DetailLabel.Caption := PowerShellText;

  if ModuleOk then
  begin
    SetStatus(3, 'OK', ColorOk);
    ModuleDetail := 'Version ' + ModuleVersion + ' in ' + ModuleLocation;
  end
  else
  begin
    SetStatus(3, 'Can install', ColorAction);
    if ModuleVersion = '' then
      ModuleDetail := 'Not installed. '
    else
      ModuleDetail := 'Version ' + ModuleVersion + ' is too old. ';
    ModuleDetail := ModuleDetail + 'Setup can install the latest version from the PowerShell Gallery: keep "Install the ExchangeOnlineManagement module" selected on the next pages.';
  end;
  Rows[3].DetailLabel.Caption := ModuleDetail;

  Y := 0;
  for I := 0 to 3 do
  begin
    Rows[I].NameLabel.Top := Y;
    Rows[I].StatusLabel.Top := Y;
    Rows[I].NameLabel.AdjustHeight;
    Rows[I].DetailLabel.Top := Y + Rows[I].NameLabel.Height + ScaleY(2);
    Rows[I].DetailLabel.AdjustHeight;
    Y := Rows[I].DetailLabel.Top + Rows[I].DetailLabel.Height + ScaleY(12);
  end;

  if not HardRequirementsMet then
    SummaryLabel.Caption := 'Setup can''t continue until the requirements marked Missing are installed. ' +
      'Get <a href="' + NetFxUrl + '">.NET Framework 4.8</a> if it''s missing, then click Check again or run Setup again.'
  else if not ModuleOk then
    SummaryLabel.Caption := 'Everything else is ready. After installing, Setup checks that Purview Explorer can load the module.'
  else
    SummaryLabel.Caption := 'This computer is ready. After installing, Setup checks that Purview Explorer can load the module.';
  SummaryLabel.Top := Y;
  SummaryLabel.AdjustHeight;

  if (WizardForm.CurPageID = RequirementsPage.ID) then
    WizardForm.NextButton.Enabled := HardRequirementsMet;
end;

procedure CheckAgainClick(Sender: TObject);
begin
  DetectSystem;
  LayoutRequirements;
end;

procedure LinkClick(Sender: TObject; const Link: string; LinkType: TSysLinkType);
var
  ErrorCode: Integer;
begin
  ShellExecAsOriginalUser('open', Link, '', '', SW_SHOWNORMAL, ewNoWait, ErrorCode);
end;

function NewText(Left, Width: Integer; const Caption: String; Bold: Boolean): TNewStaticText;
begin
  Result := TNewStaticText.Create(RequirementsPage);
  Result.AutoSize := False;
  Result.WordWrap := True;
  Result.Left := Left;
  Result.Width := Width;
  Result.Caption := Caption;
  if Bold then
    Result.Font.Style := [fsBold];
  Result.Parent := RequirementsPage.Surface;
  Result.AdjustHeight;
end;

procedure CreateRequirementsPage;
var
  I, StatusWidth: Integer;
  Names: array[0..3] of String;
begin
  RequirementsPage := CreateCustomPage(wpLicense, 'System requirements',
    'Setup checked whether this computer has everything {#AppName} needs.');
  Names[0] := 'Windows 10 version 1607, Windows Server 2016 or later';
  Names[1] := '.NET Framework 4.8 or later';
  Names[2] := 'Windows PowerShell 5.1';
  Names[3] := 'ExchangeOnlineManagement PowerShell module ' + MinModuleVersion + ' or later';
  StatusWidth := ScaleX(90);
  for I := 0 to 3 do
  begin
    Rows[I].NameLabel := NewText(0, RequirementsPage.SurfaceWidth - StatusWidth - ScaleX(8), Names[I], True);
    Rows[I].StatusLabel := NewText(RequirementsPage.SurfaceWidth - StatusWidth, StatusWidth, '', True);
    Rows[I].StatusLabel.Alignment := taRightJustify;
    Rows[I].StatusLabel.StyleElements := Rows[I].StatusLabel.StyleElements - [seFont];
    Rows[I].DetailLabel := NewText(ScaleX(12), RequirementsPage.SurfaceWidth - ScaleX(12), '', False);
  end;

  SummaryLabel := TNewLinkLabel.Create(RequirementsPage);
  SummaryLabel.AutoSize := False;
  SummaryLabel.Left := 0;
  SummaryLabel.Width := RequirementsPage.SurfaceWidth;
  SummaryLabel.OnLinkClick := @LinkClick;
  SummaryLabel.Parent := RequirementsPage.Surface;

  CheckAgainButton := TNewButton.Create(RequirementsPage);
  CheckAgainButton.Caption := '&Check again';
  CheckAgainButton.Width := WizardForm.CalculateButtonWidth([CheckAgainButton.Caption]);
  CheckAgainButton.Height := WizardForm.NextButton.Height;
  CheckAgainButton.Left := 0;
  CheckAgainButton.Top := RequirementsPage.SurfaceHeight - CheckAgainButton.Height;
  CheckAgainButton.OnClick := @CheckAgainClick;
  CheckAgainButton.Parent := RequirementsPage.Surface;

  LayoutRequirements;
end;

{ ---------------------------------------------------------------- installing the module and verifying }

{ Runs in Windows PowerShell 5.1. Typed commands aren't subject to the execution policy, unlike a script file.
  powershell.exe inherits Setup's PSModulePath; when Setup was started from PowerShell 7 that lists PowerShell 7's
  folders (whose PackageManagement can't load in Windows PowerShell) and may lack the per-user folder, so the
  command first rebuilds the default Windows PowerShell module path: Documents, then the user and machine values. }
function ModuleInstallCommand(const Scope: String): String;
begin
  Result :=
    '$env:PSModulePath = ((@([Environment]::GetFolderPath(''MyDocuments'') + ''\WindowsPowerShell\Modules'') + ' +
      '([Environment]::GetEnvironmentVariable(''PSModulePath'', ''User'') -split '';'') + ' +
      '([Environment]::GetEnvironmentVariable(''PSModulePath'', ''Machine'') -split '';'')) | Where-Object { $_ }) -join '';''; ' +
    '$ErrorActionPreference = ''Stop''; $ProgressPreference = ''SilentlyContinue''; ' +
    '[Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12; ' +
    'try { ' +
      '$nuget = Get-PackageProvider -ListAvailable -Name NuGet -ErrorAction SilentlyContinue | Where-Object { $_.Version -ge [version] ''2.8.5.201'' }; ' +
      'if (-not $nuget) { Write-Output ''Installing the NuGet package provider...''; Install-PackageProvider -Name NuGet -MinimumVersion 2.8.5.201 -Scope ' + Scope + ' -Force | Out-Null }; ' +
      'Write-Output ''Installing ' + ModuleName + ' from the PowerShell Gallery for ' + Scope + '...''; ' +
      'Install-Module -Name ' + ModuleName + ' -MinimumVersion ' + MinModuleVersion + ' -Repository PSGallery -Scope ' + Scope + ' -Force -AllowClobber; ' +
      '$m = Get-Module -ListAvailable -Name ' + ModuleName + ' | Sort-Object Version -Descending | Select-Object -First 1; ' +
      'Write-Output (''Installed ' + ModuleName + ' {0} in {1}'' -f $m.Version, $m.ModuleBase); exit 0 ' +
    '} catch { Write-Output (''ERROR: '' + $_.Exception.Message); exit 1 }';
end;

procedure ModuleInstallOutput(const S: String; const Error, FirstLine: Boolean);
begin
  Log('PowerShell: ' + S);
  if Trim(S) <> '' then
    ModuleInstallLog := ModuleInstallLog + S + #13#10;
end;

procedure InstallModule;
var
  Scope, Params: String;
  Code: Integer;
begin
  if IsAdminInstallMode then Scope := 'AllUsers' else Scope := 'CurrentUser';
  WizardForm.StatusLabel.Caption := 'Installing the ExchangeOnlineManagement PowerShell module...';
  WizardForm.FilenameLabel.Caption := 'Downloading it from the PowerShell Gallery. This can take a minute.';
  WizardForm.ProgressGauge.Style := npbstMarquee;
  ModuleInstallRan := True;
  ModuleInstallLog := '';
  Params := '-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command "' + ModuleInstallCommand(Scope) + '"';
  Log('Installing the module: powershell.exe ' + Params);
  if ExecAndLogOutput(ExpandConstant('{sys}\WindowsPowerShell\v1.0\powershell.exe'), Params, '', SW_HIDE,
       ewWaitUntilTerminated, Code, @ModuleInstallOutput) then
    ModuleInstallFailed := Code <> 0
  else
  begin
    ModuleInstallFailed := True;
    ModuleInstallLog := ModuleInstallLog + 'Couldn''t start Windows PowerShell: ' + SysErrorMessage(Code) + #13#10;
  end;
  Log(Format('Module installation exit code %d', [Code]));
  WizardForm.ProgressGauge.Style := npbstNormal;
  DetectModule;
end;

procedure VerifyInstallation;
var
  ReportFile: String;
  Lines: TArrayOfString;
  I, Code: Integer;
begin
  WizardForm.StatusLabel.Caption := 'Checking that {#AppName} can load the PowerShell module...';
  WizardForm.FilenameLabel.Caption := '';
  WizardForm.ProgressGauge.Style := npbstMarquee;
  ReportFile := ExpandConstant('{tmp}\prerequisites.txt');
  VerificationRan := Exec(ExpandConstant('{app}\{#AppExe}'), '"/CheckPrerequisites:' + ReportFile + '"', '',
    SW_HIDE, ewWaitUntilTerminated, Code);
  VerificationPassed := VerificationRan and (Code = 0);
  VerificationReport := '';
  if LoadStringsFromFile(ReportFile, Lines) then
    for I := 0 to GetArrayLength(Lines) - 1 do
      VerificationReport := VerificationReport + Lines[I] + #13#10
  else if VerificationRan then
    VerificationReport := Format('{#AppExe} /CheckPrerequisites exited with code %d and wrote no report.', [Code])
  else
    VerificationReport := 'Couldn''t start {#AppExe}: ' + SysErrorMessage(Code);
  WizardForm.ProgressGauge.Style := npbstNormal;

  if VerificationPassed then
    Log('Verification passed.')
  else
    Log('Verification failed.');
  Log('Verification report:' + #13#10 + VerificationReport);
end;

procedure ShowVerification;
var
  Text: String;
begin
  if VerificationPassed then
    VerifyCaption.Caption := '{#AppName} is ready to use: it found the ExchangeOnlineManagement module and loaded it.'
  else
    VerifyCaption.Caption := '{#AppName} is installed, but it can''t load the ExchangeOnlineManagement module yet. ' +
      'Fix the problem below (to install the module yourself, run: ' + ManualInstallCommand + '), then use Help > Check Prerequisites in {#AppName}.';
  VerifyCaption.AdjustHeight;
  VerifyMemo.Top := VerifyCaption.Top + VerifyCaption.Height + ScaleY(8);
  VerifyMemo.Height := VerifyPage.SurfaceHeight - VerifyMemo.Top;

  Text := '';
  if ModuleInstallRan then
  begin
    if ModuleInstallFailed then
      Text := 'Installing the module from the PowerShell Gallery failed:' + #13#10
    else
      Text := 'Installing the module from the PowerShell Gallery:' + #13#10;
    Text := Text + ModuleInstallLog + #13#10;
  end;
  VerifyMemo.Text := Text + VerificationReport;
end;

procedure CreateVerifyPage;
begin
  VerifyPage := CreateCustomPage(wpInstalling, 'Checking the installation',
    'Setup checked that {#AppName} can load the PowerShell module it uses.');
  VerifyCaption := TNewStaticText.Create(VerifyPage);
  VerifyCaption.AutoSize := False;
  VerifyCaption.WordWrap := True;
  VerifyCaption.Width := VerifyPage.SurfaceWidth;
  VerifyCaption.Parent := VerifyPage.Surface;
  VerifyMemo := TNewMemo.Create(VerifyPage);
  VerifyMemo.Width := VerifyPage.SurfaceWidth;
  VerifyMemo.ReadOnly := True;
  VerifyMemo.ScrollBars := ssBoth;
  VerifyMemo.WordWrap := False;
  VerifyMemo.Parent := VerifyPage.Surface;
end;

{ ---------------------------------------------------------------- event functions }

function InitializeSetup: Boolean;
begin
  DetectSystem;
  Log('Windows: ' + WindowsText);
  Log('.NET Framework: ' + NetFxText);
  Log('Windows PowerShell: ' + PowerShellText);
  if ModuleVersion <> '' then
    Log('ExchangeOnlineManagement: ' + ModuleVersion + ' in ' + ModuleLocation)
  else
    Log('ExchangeOnlineManagement: not found');

  Result := True;
  if not HardRequirementsMet and WizardSilent then
  begin
    Log(MissingRequirementsText);
    SuppressibleMsgBox(MissingRequirementsText, mbCriticalError, MB_OK, IDOK);
    Result := False;
  end;
end;

procedure InitializeWizard;
begin
  CreateRequirementsPage;
  CreateVerifyPage;
end;

procedure CurPageChanged(CurPageID: Integer);
begin
  if CurPageID = RequirementsPage.ID then
    WizardForm.NextButton.Enabled := HardRequirementsMet
  else if CurPageID = VerifyPage.ID then
    ShowVerification
  else if (CurPageID = wpFinished) and not VerificationPassed then
  begin
    WizardForm.FinishedLabel.Caption := WizardForm.FinishedLabel.Caption + #13#10#13#10 +
      'It can''t load the ExchangeOnlineManagement module yet; see the previous page for what to do.';
    WizardForm.FinishedLabel.AdjustHeight;
  end;
end;

function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;
  if (CurPageID = RequirementsPage.ID) and not HardRequirementsMet then
  begin
    MsgBox(MissingRequirementsText, mbError, MB_OK);
    Result := False;
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    if WizardIsTaskSelected('installmodule') then
      InstallModule;
    VerifyInstallation;
  end;
end;
