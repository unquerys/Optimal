#define MyAppName "Optimal"
#define MyAppVersion "1.0.2"
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
VersionInfoVersion=1.0.2.0
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
WizardSmallImageFile=..\Optimal Symbol.png
WizardImageFile=..\OptimalLogo.png
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma2/ultra64
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin
PrivilegesRequiredOverridesAllowed=dialog
WizardStyle=modern
WizardSizePercent=125
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
Filename: "{app}\{#MyAppExeName}"; Parameters: "--onboarding"; Description: "Open Optimal"; Flags: nowait postinstall skipifsilent

[Code]
const
  CanvasColor = $00120B08;
  SidebarColor = $001A100B;
  SurfaceColor = $00211510;
  RaisedColor = $002B1D17;
  BorderColor = $00422E25;
  TextColor = $00FCF7F6;
  MutedColor = $00BBA9A0;
  AccentColor = $00FF8263;
  AccentDarkColor = $006A3025;
  PurpleColor = $00E67596;
  SuccessColor = $00A6D665;

var
  ShellPanel, SidebarPanel, ContentPanel, FooterPanel, TopAccent: TPanel;
  LogoImage: TBitmapImage;
  BrandLabel, VersionLabel, EyebrowLabel, TitleLabel, DetailLabel: TNewStaticText;
  StepLabels: array[0..4] of TNewStaticText;
  TermsMemo: TNewMemo;
  TermsCard, AcceptCard, AcceptBox, LocationCard, ShortcutCard: TPanel;
  AcceptMark, AcceptText, LocationLabel, ShortcutTitle, ShortcutText: TNewStaticText;
  LocationEdit: TNewEdit;
  BackPanel, NextPanel, CancelPanel: TPanel;
  BackText, NextText, CancelText: TNewStaticText;
  ProgressTrack, ProgressFill: TPanel;
  TermsAccepted, DesktopShortcut: Boolean;

function MakeLabel(ParentControl: TWinControl; CaptionText: String; X, Y, W, H, Size: Integer; Color: TColor; Bold: Boolean): TNewStaticText;
begin
  Result := TNewStaticText.Create(WizardForm);
  Result.Parent := ParentControl;
  Result.Caption := CaptionText;
  Result.Left := ScaleX(X);
  Result.Top := ScaleY(Y);
  Result.Width := ScaleX(W);
  Result.Height := ScaleY(H);
  Result.AutoSize := False;
  Result.WordWrap := True;
  Result.Font.Name := 'Segoe UI';
  Result.Font.Size := Size;
  Result.Font.Color := Color;
  if Bold then Result.Font.Style := [fsBold];
end;

function MakePanel(ParentControl: TWinControl; X, Y, W, H: Integer; Color: TColor): TPanel;
begin
  Result := TPanel.Create(WizardForm);
  Result.Parent := ParentControl;
  Result.Left := ScaleX(X);
  Result.Top := ScaleY(Y);
  Result.Width := ScaleX(W);
  Result.Height := ScaleY(H);
  Result.Color := Color;
  Result.BevelOuter := bvNone;
end;

procedure SetVisible(Control: TControl; Value: Boolean);
begin
  Control.Visible := Value;
end;

procedure SetStepState(Index, CurrentIndex: Integer);
begin
  if Index = CurrentIndex then
  begin
    StepLabels[Index].Font.Color := TextColor;
    StepLabels[Index].Font.Style := [fsBold];
    StepLabels[Index].Caption := '●  ' + Copy(StepLabels[Index].Caption, 4, 255);
  end
  else
  begin
    StepLabels[Index].Font.Color := MutedColor;
    StepLabels[Index].Font.Style := [];
    StepLabels[Index].Caption := '○  ' + Copy(StepLabels[Index].Caption, 4, 255);
  end;
end;

procedure RefreshAcceptance;
begin
  if TermsAccepted then
  begin
    AcceptBox.Color := AccentColor;
    AcceptMark.Caption := '✓';
    AcceptMark.Font.Color := CanvasColor;
    AcceptText.Caption := 'Terms accepted';
    AcceptText.Font.Color := SuccessColor;
    WizardForm.LicenseAcceptedRadio.Checked := True;
    WizardForm.NextButton.Enabled := True;
  end
  else
  begin
    AcceptBox.Color := RaisedColor;
    AcceptMark.Caption := '';
    AcceptText.Caption := 'I have read and accept the Optimal terms';
    AcceptText.Font.Color := TextColor;
    WizardForm.LicenseNotAcceptedRadio.Checked := True;
    WizardForm.NextButton.Enabled := False;
  end;
end;

procedure AcceptClick(Sender: TObject);
begin
  TermsAccepted := not TermsAccepted;
  RefreshAcceptance;
end;

procedure ShortcutClick(Sender: TObject);
begin
  DesktopShortcut := not DesktopShortcut;
  if DesktopShortcut then
  begin
    WizardSelectTasks('desktopicon');
    ShortcutCard.Color := AccentDarkColor;
    ShortcutTitle.Caption := '✓  Desktop shortcut enabled';
    ShortcutTitle.Font.Color := SuccessColor;
  end
  else
  begin
    WizardSelectTasks('');
    ShortcutCard.Color := SurfaceColor;
    ShortcutTitle.Caption := '○  Add a desktop shortcut';
    ShortcutTitle.Font.Color := TextColor;
  end;
end;

procedure BackClick(Sender: TObject);
begin
  WizardForm.BackButton.OnClick(WizardForm.BackButton);
end;

procedure NextClick(Sender: TObject);
begin
  if WizardForm.CurPageID = wpSelectDir then
    WizardForm.DirEdit.Text := LocationEdit.Text;
  WizardForm.NextButton.OnClick(WizardForm.NextButton);
end;

procedure CancelClick(Sender: TObject);
begin
  WizardForm.CancelButton.OnClick(WizardForm.CancelButton);
end;

procedure ShowCustomPage(StepIndex: Integer; Eyebrow, Title, Detail: String);
var
  I: Integer;
begin
  for I := 0 to 4 do SetStepState(I, StepIndex);
  EyebrowLabel.Caption := Uppercase(Eyebrow);
  TitleLabel.Caption := Title;
  DetailLabel.Caption := Detail;

  SetVisible(TermsCard, False);
  SetVisible(AcceptCard, False);
  SetVisible(LocationCard, False);
  SetVisible(ShortcutCard, False);
  SetVisible(ProgressTrack, False);
  SetVisible(BackPanel, WizardForm.CurPageID <> wpWelcome);
  SetVisible(NextPanel, (WizardForm.CurPageID <> wpInstalling) and (WizardForm.CurPageID <> wpFinished));

  if WizardForm.CurPageID = wpWelcome then
    NextText.Caption := 'Continue  →'
  else if WizardForm.CurPageID = wpLicense then
  begin
    SetVisible(TermsCard, True);
    SetVisible(AcceptCard, True);
    NextText.Caption := 'Continue  →';
    RefreshAcceptance;
  end
  else if WizardForm.CurPageID = wpSelectDir then
  begin
    SetVisible(LocationCard, True);
    LocationEdit.Text := WizardForm.DirEdit.Text;
    NextText.Caption := 'Continue  →';
  end
  else if WizardForm.CurPageID = wpSelectTasks then
  begin
    SetVisible(ShortcutCard, True);
    NextText.Caption := 'Continue  →';
  end
  else if WizardForm.CurPageID = wpReady then
    NextText.Caption := 'Install Optimal  →'
  else if WizardForm.CurPageID = wpInstalling then
  begin
    SetVisible(ProgressTrack, True);
    SetVisible(BackPanel, False);
    SetVisible(NextPanel, False);
  end
  else if WizardForm.CurPageID = wpFinished then
  begin
    SetVisible(BackPanel, False);
    SetVisible(NextPanel, True);
    NextText.Caption := 'Finish';
  end;
end;

procedure InitializeWizard;
var
  I: Integer;
  StepNames: array[0..4] of String;
begin
  WizardForm.Caption := 'Optimal Setup';
  WizardForm.ClientWidth := ScaleX(820);
  WizardForm.ClientHeight := ScaleY(540);
  WizardForm.Color := CanvasColor;

  WizardForm.MainPanel.Visible := False;
  WizardForm.InnerNotebook.Visible := False;
  WizardForm.OuterNotebook.Visible := False;
  WizardForm.Bevel.Visible := False;
  WizardForm.Bevel1.Visible := False;
  WizardForm.BackButton.Visible := False;
  WizardForm.NextButton.Visible := False;
  WizardForm.CancelButton.Visible := False;

  ShellPanel := MakePanel(WizardForm, 0, 0, 820, 540, CanvasColor);
  TopAccent := MakePanel(ShellPanel, 0, 0, 820, 4, AccentColor);
  SidebarPanel := MakePanel(ShellPanel, 0, 4, 194, 536, SidebarColor);
  ContentPanel := MakePanel(ShellPanel, 194, 4, 626, 468, CanvasColor);
  FooterPanel := MakePanel(ShellPanel, 194, 472, 626, 68, CanvasColor);

  LogoImage := TBitmapImage.Create(WizardForm);
  LogoImage.Parent := SidebarPanel;
  LogoImage.Left := ScaleX(24);
  LogoImage.Top := ScaleY(28);
  LogoImage.Width := ScaleX(34);
  LogoImage.Height := ScaleY(34);
  LogoImage.Stretch := True;
  LogoImage.AutoSize := False;
  LogoImage.Bitmap.Assign(WizardForm.WizardSmallBitmapImage.Bitmap);

  BrandLabel := MakeLabel(SidebarPanel, 'OPTIMAL', 68, 29, 104, 22, 13, TextColor, True);
  VersionLabel := MakeLabel(SidebarPanel, 'STABLE  ·  x64', 68, 51, 104, 16, 8, PurpleColor, True);

  StepNames[0] := 'Welcome';
  StepNames[1] := 'Terms';
  StepNames[2] := 'Location';
  StepNames[3] := 'Shortcut';
  StepNames[4] := 'Install';
  for I := 0 to 4 do
    StepLabels[I] := MakeLabel(SidebarPanel, '○  ' + StepNames[I], 25, 124 + (I * 38), 145, 24, 10, MutedColor, False);

  MakeLabel(SidebarPanel, '●  ADMIN REQUIRED', 25, 423, 150, 20, 8, SuccessColor, True);
  MakeLabel(SidebarPanel, 'UAC elevation protects' + #13#10 + 'system-wide changes.', 25, 446, 150, 40, 8, MutedColor, False);

  EyebrowLabel := MakeLabel(ContentPanel, 'WELCOME', 40, 38, 520, 18, 8, AccentColor, True);
  TitleLabel := MakeLabel(ContentPanel, 'Install Optimal.', 40, 68, 540, 46, 24, TextColor, True);
  DetailLabel := MakeLabel(ContentPanel, '', 40, 120, 540, 48, 10, MutedColor, False);

  TermsCard := MakePanel(ContentPanel, 40, 178, 540, 218, SurfaceColor);
  TermsMemo := TNewMemo.Create(WizardForm);
  TermsMemo.Parent := TermsCard;
  TermsMemo.Left := ScaleX(12);
  TermsMemo.Top := ScaleY(12);
  TermsMemo.Width := ScaleX(516);
  TermsMemo.Height := ScaleY(194);
  TermsMemo.ReadOnly := True;
  TermsMemo.ScrollBars := ssVertical;
  TermsMemo.Color := RaisedColor;
  TermsMemo.Font.Name := 'Segoe UI';
  TermsMemo.Font.Size := 9;
  TermsMemo.Font.Color := TextColor;
  TermsMemo.Text := WizardForm.LicenseMemo.Text;

  AcceptCard := MakePanel(ContentPanel, 40, 408, 540, 38, CanvasColor);
  AcceptCard.Cursor := crHand;
  AcceptCard.OnClick := @AcceptClick;
  AcceptBox := MakePanel(AcceptCard, 0, 7, 22, 22, RaisedColor);
  AcceptBox.Cursor := crHand;
  AcceptBox.OnClick := @AcceptClick;
  AcceptMark := MakeLabel(AcceptBox, '', 3, 1, 18, 18, 11, CanvasColor, True);
  AcceptMark.Cursor := crHand;
  AcceptMark.OnClick := @AcceptClick;
  AcceptText := MakeLabel(AcceptCard, 'I have read and accept the Optimal terms', 34, 8, 490, 22, 10, TextColor, False);
  AcceptText.Cursor := crHand;
  AcceptText.OnClick := @AcceptClick;

  LocationCard := MakePanel(ContentPanel, 40, 190, 540, 100, SurfaceColor);
  LocationLabel := MakeLabel(LocationCard, 'INSTALL LOCATION', 16, 15, 490, 18, 8, AccentColor, True);
  LocationEdit := TNewEdit.Create(WizardForm);
  LocationEdit.Parent := LocationCard;
  LocationEdit.Left := ScaleX(16);
  LocationEdit.Top := ScaleY(43);
  LocationEdit.Width := ScaleX(508);
  LocationEdit.Height := ScaleY(32);
  LocationEdit.Color := RaisedColor;
  LocationEdit.Font.Name := 'Segoe UI';
  LocationEdit.Font.Size := 10;
  LocationEdit.Font.Color := TextColor;

  ShortcutCard := MakePanel(ContentPanel, 40, 190, 540, 94, SurfaceColor);
  ShortcutCard.Cursor := crHand;
  ShortcutCard.OnClick := @ShortcutClick;
  ShortcutTitle := MakeLabel(ShortcutCard, '○  Add a desktop shortcut', 18, 17, 500, 24, 12, TextColor, True);
  ShortcutTitle.Cursor := crHand;
  ShortcutTitle.OnClick := @ShortcutClick;
  ShortcutText := MakeLabel(ShortcutCard, 'Launch Optimal directly from your Windows desktop.', 43, 49, 470, 22, 9, MutedColor, False);
  ShortcutText.Cursor := crHand;
  ShortcutText.OnClick := @ShortcutClick;

  ProgressTrack := MakePanel(ContentPanel, 40, 208, 540, 8, RaisedColor);
  ProgressFill := MakePanel(ProgressTrack, 0, 0, 10, 8, AccentColor);

  BackPanel := MakePanel(FooterPanel, 274, 14, 88, 40, SurfaceColor);
  BackPanel.Cursor := crHand;
  BackPanel.OnClick := @BackClick;
  BackText := MakeLabel(BackPanel, 'Back', 0, 10, 88, 20, 9, MutedColor, True);
  BackText.Alignment := taCenter;
  BackText.Cursor := crHand;
  BackText.OnClick := @BackClick;

  NextPanel := MakePanel(FooterPanel, 374, 14, 178, 40, AccentColor);
  NextPanel.Cursor := crHand;
  NextPanel.OnClick := @NextClick;
  NextText := MakeLabel(NextPanel, 'Continue  →', 0, 10, 178, 20, 9, CanvasColor, True);
  NextText.Alignment := taCenter;
  NextText.Cursor := crHand;
  NextText.OnClick := @NextClick;

  CancelPanel := MakePanel(SidebarPanel, 24, 494, 74, 26, SidebarColor);
  CancelPanel.Cursor := crHand;
  CancelPanel.OnClick := @CancelClick;
  CancelText := MakeLabel(CancelPanel, 'Cancel', 0, 4, 74, 18, 9, MutedColor, False);
  CancelText.Cursor := crHand;
  CancelText.OnClick := @CancelClick;

  TermsAccepted := False;
  DesktopShortcut := False;
end;

procedure CurPageChanged(CurPageID: Integer);
begin
  ShellPanel.BringToFront;
  if CurPageID = wpWelcome then
    ShowCustomPage(0, 'WELCOME', 'Install Optimal.', 'A clean, self-contained setup for the Optimal Windows control center. Nothing is optimized during installation.')
  else if CurPageID = wpLicense then
    ShowCustomPage(1, 'TERMS AND SAFETY', 'Review before continuing.', 'Read the safety terms below. Acceptance is required before Optimal can be installed.')
  else if CurPageID = wpSelectDir then
    ShowCustomPage(2, 'INSTALL LOCATION', 'Choose where Optimal lives.', 'Optimal installs as a self-contained Windows application with no separate runtime required.')
  else if CurPageID = wpSelectTasks then
    ShowCustomPage(3, 'SHORTCUT', 'Keep Optimal within reach.', 'The shortcut is optional and can be removed at any time.')
  else if CurPageID = wpReady then
    ShowCustomPage(4, 'READY TO INSTALL', 'Everything is ready.', 'Setup will copy Optimal to your PC. No Windows tweaks or cleanup actions run during installation.')
  else if CurPageID = wpInstalling then
    ShowCustomPage(4, 'INSTALLING', 'Setting up Optimal...', 'Please keep this window open while the signed package is copied and registered.')
  else if CurPageID = wpFinished then
    ShowCustomPage(4, 'INSTALL COMPLETE', 'Optimal is ready.', 'Open Optimal to review your hardware profile and choose what happens next.');
end;

procedure CurInstallProgressChanged(CurProgress, MaxProgress: Integer);
begin
  if MaxProgress > 0 then
    ProgressFill.Width := (ProgressTrack.Width * CurProgress) div MaxProgress;
end;
