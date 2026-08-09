; =============================================================================
;  Inventor Web 3D Viewer  v1.1.0  —  Inno Setup 6 script
; =============================================================================
;  Prerequisites:
;    1. Install Inno Setup 6:  https://jrsoftware.org/isinfo.php
;    2. Build the add-in (Release | x64) so that
;         InventorWebViewer.bundle\Contents\InventorWebViewer.dll  exists
;       (or run prepare_setup.bat next to this file)
;    3. Open this .iss in Inno Setup Compiler → Build → Compile
;  Output:
;    Installer\Output\InventorWebViewer_Setup_1.1.0.exe
; =============================================================================

#define MyAppName      "Inventor Web 3D Viewer"
#define MyAppVersion   "1.1.0"
#define MyAppPublisher "InventorWebViewer"
#define MyAppURL       "https://www.linkedin.com/in/zekaee/"
#define BundleName     "InventorWebViewer.bundle"

[Setup]
AppId={{B7E8F901-2345-6789-ABCD-EF0123456789}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
DefaultDirName={commonappdata}\Autodesk\ApplicationPlugins\{#BundleName}
DefaultGroupName={#MyAppName}
DisableDirPage=yes
DisableProgramGroupPage=yes
OutputDir=Output
OutputBaseFilename=InventorWebViewer_Setup_{#MyAppVersion}
SetupLogging=yes
Compression=lzma2/ultra64
SolidCompression=yes
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
WizardStyle=modern
UninstallDisplayName={#MyAppName}
VersionInfoVersion={#MyAppVersion}
VersionInfoCompany={#MyAppPublisher}
VersionInfoDescription={#MyAppName} installer
VersionInfoProductName={#MyAppName}
CloseApplications=no
RestartApplications=no
; Allow reinstall / upgrade over previous version
UsePreviousAppDir=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Messages]
WelcomeLabel2=This will install [name/ver] on your computer.%n%nThe add-in is registered under:%n  %ProgramData%\Autodesk\ApplicationPlugins\InventorWebViewer.bundle\%n%nPlease close Autodesk Inventor before continuing.
FinishedLabel=Installation complete.%n%n1. Start Autodesk Inventor%n2. Open an assembly%n3. Use the ribbon tab  "Web 3D Viewer"

[Files]
; Full ApplicationPlugins bundle layout
Source: "..\InventorWebViewer.bundle\PackageContents.xml"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\InventorWebViewer.bundle\Contents\*"; DestDir: "{app}\Contents"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{group}\Open plugins folder"; Filename: "{commonappdata}\Autodesk\ApplicationPlugins"

[Run]
; Nothing to launch — add-in loads inside Inventor

[UninstallDelete]
Type: filesandordirs; Name: "{app}"

[Code]
function DllPresent: Boolean;
begin
  Result := FileExists(ExpandConstant('{#SourcePath}\..\InventorWebViewer.bundle\Contents\InventorWebViewer.dll'));
end;

function InventorFound: Boolean;
begin
  Result :=
    DirExists(ExpandConstant('{pf64}\Autodesk\Inventor 2026')) or
    DirExists(ExpandConstant('{pf64}\Autodesk\Inventor 2025')) or
    DirExists(ExpandConstant('{pf64}\Autodesk\Inventor 2024')) or
    DirExists(ExpandConstant('{pf64}\Autodesk\Inventor 2023')) or
    DirExists(ExpandConstant('{pf}\Autodesk\Inventor 2026')) or
    DirExists(ExpandConstant('{pf}\Autodesk\Inventor 2025')) or
    DirExists(ExpandConstant('{pf}\Autodesk\Inventor 2024'));
end;

function InitializeSetup: Boolean;
begin
  Result := True;

  if not DllPresent then
  begin
    MsgBox(
      'InventorWebViewer.dll was not found in:'#13#10 +
      '  InventorWebViewer.bundle\Contents\'#13#10#13#10 +
      'Build the project (Release | x64) first, or run prepare_setup.bat,'#13#10 +
      'then compile this installer again.',
      mbError, MB_OK);
    Result := False;
    Exit;
  end;

  if not InventorFound then
  begin
    if MsgBox(
      'Autodesk Inventor 2023+ was not detected on this machine.'#13#10#13#10 +
      'The add-in targets Inventor 2025+ (SeriesMin 29.0).'#13#10 +
      'Continue installation anyway?',
      mbConfirmation, MB_YESNO) = IDNO then
      Result := False;
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  PluginsRoot: String;
begin
  if CurStep = ssPostInstall then
  begin
    PluginsRoot := ExpandConstant('{commonappdata}\Autodesk\ApplicationPlugins');
    if not DirExists(PluginsRoot) then
      ForceDirectories(PluginsRoot);
  end;
end;

function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;
end;
