; Inventor Web 3D Viewer – Inno Setup installer
; Requires Inno Setup 6.x (https://jrsoftware.org/isinfo.php)
; Build: open this file in Inno Setup Compiler → Compile
;
; Detects Inventor 2023–2026 and installs the .bundle into
; %ProgramData%\Autodesk\ApplicationPlugins\

#define MyAppName      "Inventor Web 3D Viewer"
#define MyAppVersion   "1.1.0"
#define MyAppPublisher "InventorWebViewer"
#define MyAppURL       "https://www.linkedin.com/in/zekaee/"
#define BundleName     "InventorWebViewer.bundle"

[Setup]
AppId={{B7E8F901-2345-6789-ABCD-EF0123456789}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
DefaultDirName={commonappdata}\Autodesk\ApplicationPlugins\{#BundleName}
DisableDirPage=yes
DisableProgramGroupPage=yes
OutputDir=Output
OutputBaseFilename=InventorWebViewer_Setup_{#MyAppVersion}
Compression=lzma2
SolidCompression=yes
PrivilegesRequired=admin
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
WizardStyle=modern
SetupIconFile=
UninstallDisplayName={#MyAppName}
VersionInfoVersion={#MyAppVersion}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "persian"; MessagesFile: "compiler:Languages\Farsi.isl"; LicenseFile: 

[Files]
; Bundle structure expected by Autodesk ApplicationPlugins
Source: "..\InventorWebViewer.bundle\PackageContents.xml"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\InventorWebViewer.bundle\Contents\*"; DestDir: "{app}\Contents"; Flags: ignoreversion recursesubdirs createallsubdirs
; DLL must be present in Contents before packaging (copy from bin\Release after build)

[Code]
function InventorFound: Boolean;
begin
  Result :=
    DirExists(ExpandConstant('{pf64}\Autodesk\Inventor 2026')) or
    DirExists(ExpandConstant('{pf64}\Autodesk\Inventor 2025')) or
    DirExists(ExpandConstant('{pf64}\Autodesk\Inventor 2024')) or
    DirExists(ExpandConstant('{pf64}\Autodesk\Inventor 2023')) or
    DirExists(ExpandConstant('{pf}\Autodesk\Inventor 2026')) or
    DirExists(ExpandConstant('{pf}\Autodesk\Inventor 2025'));
end;

function InitializeSetup: Boolean;
begin
  Result := True;
  if not InventorFound then
  begin
    if MsgBox(
      'Autodesk Inventor 2023+ was not detected.'#13#10#13#10 +
      'The add-in requires Inventor 2025+ (SeriesMin 29.0 recommended).'#13#10 +
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

[Icons]
; No desktop icon needed — add-in loads inside Inventor

[Run]
; Optional: open install notes
Filename: "{app}\Contents\README_PUT_DLL_HERE.txt"; Description: "Open install notes"; Flags: postinstall shellexec skipifsilent unchecked

[UninstallDelete]
Type: filesandordirs; Name: "{app}"
