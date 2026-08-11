#define MyAppName "Optimal"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "Optimal"
#define MyAppExeName "Optimal.exe"
#ifndef PackageDir
  #define PackageDir "..\artifacts\package"
#endif

[Setup]
AppId={{DF7D5409-0A6A-4FA0-A08C-78A62D5955E8}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
VersionInfoVersion=1.0.0.0
VersionInfoCompany={#MyAppPublisher}
VersionInfoDescription=Optimal Windows tuning utility installer
VersionInfoProductName={#MyAppName}
VersionInfoProductVersion={#MyAppVersion}
DefaultDirName={autopf}\Optimal
DefaultGroupName=Optimal
DisableProgramGroupPage=yes
LicenseFile=..\TERMS.txt
OutputDir=..\artifacts
OutputBaseFilename=Optimal-Setup
SetupIconFile=..\Optimal Symbol.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma2/ultra64
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin
PrivilegesRequiredOverridesAllowed=dialog
WizardStyle=modern
WizardSizePercent=120
DisableWelcomePage=no
ShowLanguageDialog=no
SetupLogging=yes
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Shortcuts"; Flags: unchecked

[Files]
Source: "{#PackageDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\Optimal"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"
Name: "{autodesktop}\Optimal"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Open Optimal"; Flags: nowait postinstall skipifsilent

[Code]
const
  CanvasColor = $00120B08;
  SurfaceColor = $00211510;
  RaisedColor = $002B1D17;
  BorderColor = $00422E25;
  TextColor = $00FCF7F6;
  MutedColor = $00AE9B92;
  AccentColor = $00FF8263;
  SuccessColor = $00A6D665;

procedure StyleLabel(LabelControl: TNewStaticText; Color: TColor);
begin
  LabelControl.Font.Name := 'Segoe UI';
  LabelControl.Font.Color := Color;
end;

procedure InitializeWizard;
begin
  WizardForm.Caption := 'Install Optimal';
  WizardForm.Color := CanvasColor;
  WizardForm.MainPanel.Color := CanvasColor;
  WizardForm.InnerPage.Color := CanvasColor;
  WizardForm.Bevel.Visible := False;
  WizardForm.Bevel1.Visible := False;

  StyleLabel(WizardForm.PageNameLabel, TextColor);
  WizardForm.PageNameLabel.Font.Size := 17;
  WizardForm.PageNameLabel.Font.Style := [fsBold];
  StyleLabel(WizardForm.PageDescriptionLabel, MutedColor);

  StyleLabel(WizardForm.WelcomeLabel1, TextColor);
  WizardForm.WelcomeLabel1.Font.Size := 22;
  WizardForm.WelcomeLabel1.Font.Style := [fsBold];
  StyleLabel(WizardForm.WelcomeLabel2, MutedColor);

  WizardForm.LicenseMemo.Color := RaisedColor;
  WizardForm.LicenseMemo.Font.Name := 'Segoe UI';
  WizardForm.LicenseMemo.Font.Color := TextColor;
  WizardForm.LicenseMemo.BorderStyle := bsSingle;
  WizardForm.LicenseAcceptedRadio.Font.Name := 'Segoe UI';
  WizardForm.LicenseAcceptedRadio.Font.Color := TextColor;
  WizardForm.LicenseNotAcceptedRadio.Font.Name := 'Segoe UI';
  WizardForm.LicenseNotAcceptedRadio.Font.Color := MutedColor;

  WizardForm.DirEdit.Color := RaisedColor;
  WizardForm.DirEdit.Font.Color := TextColor;
  WizardForm.GroupEdit.Color := RaisedColor;
  WizardForm.GroupEdit.Font.Color := TextColor;
  WizardForm.TasksList.Color := RaisedColor;
  WizardForm.TasksList.Font.Color := TextColor;
  WizardForm.ReadyMemo.Color := RaisedColor;
  WizardForm.ReadyMemo.Font.Color := TextColor;
  WizardForm.FinishedLabel.Font.Color := TextColor;
  WizardForm.RunList.Font.Color := TextColor;

  WizardForm.BackButton.Font.Name := 'Segoe UI Semibold';
  WizardForm.BackButton.Font.Color := MutedColor;
  WizardForm.NextButton.Font.Name := 'Segoe UI Semibold';
  WizardForm.NextButton.Font.Color := AccentColor;
  WizardForm.CancelButton.Font.Name := 'Segoe UI Semibold';
  WizardForm.CancelButton.Font.Color := MutedColor;
end;

procedure CurPageChanged(CurPageID: Integer);
begin
  if CurPageID = wpWelcome then
  begin
    WizardForm.WelcomeLabel1.Caption := 'Optimal is ready when you are.';
    WizardForm.WelcomeLabel2.Caption :=
      'Install the stable Windows tuning control center.' + #13#10 + #13#10 +
      'Nothing is optimized during setup. Every change still requires review and confirmation inside Optimal.';
  end;
end;
