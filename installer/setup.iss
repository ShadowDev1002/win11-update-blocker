#define MyAppName "Win11 Update Blocker"

#define MyAppVersion "1.0.0"

#define MyAppPublisher "Win11 Update Blocker"

#define MyAppExeName "Win11UpdateBlocker.exe"

#define MyAppServiceExe "Win11UpdateBlocker.Service.exe"



[Setup]

AppId={{A7B3C4D5-E6F7-4890-ABCD-EF1234567890}}

AppName={#MyAppName}

AppVersion={#MyAppVersion}

AppVerName={#MyAppName} {#MyAppVersion}

AppPublisher={#MyAppPublisher}

AppCopyright=Copyright (C) 2026

DefaultDirName={autopf}\Win11 Update Blocker

DefaultGroupName={#MyAppName}

DisableProgramGroupPage=no

LicenseFile=LICENSE.txt

OutputDir=output

OutputBaseFilename=Win11 Update Blocker Setup

SetupIconFile=..\assets\icon.ico

WizardImageFile=..\assets\installer-sidebar.bmp

WizardSmallImageFile=..\assets\installer-small.bmp

UninstallDisplayIcon={app}\Assets\icon.ico

UninstallDisplayName={#MyAppName}

Compression=lzma2/ultra64

SolidCompression=yes

WizardStyle=modern

PrivilegesRequired=admin

ArchitecturesAllowed=x64compatible

ArchitecturesInstallIn64BitMode=x64compatible

MinVersion=10.0.22000

CloseApplications=force

RestartApplications=no

AlwaysRestart=no

ChangesAssociations=no

DisableWelcomePage=no

DisableDirPage=no

DisableReadyPage=no



[Languages]

Name: "german"; MessagesFile: "compiler:Languages\German.isl"



[Tasks]

Name: "desktopicon"; Description: "Desktop-Verknüpfung erstellen"; GroupDescription: "Zusätzliche Verknüpfungen:"; Flags: checkedonce

Name: "launchapp"; Description: "Win11 Update Blocker nach der Installation starten"; GroupDescription: "Abschluss:"; Flags: checkedonce



[Dirs]
Name: "{commonappdata}\Win11UpdateBlocker"; Permissions: users-modify

[Files]

Source: "..\publish\gui\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

Source: "..\publish\service\*"; DestDir: "{app}\service"; Flags: ignoreversion recursesubdirs createallsubdirs

Source: "scripts\*"; DestDir: "{app}\scripts"; Flags: ignoreversion

Source: "..\assets\icon.ico"; DestDir: "{app}\Assets"; Flags: ignoreversion

Source: "..\assets\icon.png"; DestDir: "{app}\Assets"; Flags: ignoreversion

Source: "..\assets\logo.png"; DestDir: "{app}\Assets"; Flags: ignoreversion

Source: "LICENSE.txt"; DestDir: "{app}"; Flags: ignoreversion



[Icons]

Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\Assets\icon.ico"

Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"

Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\Assets\icon.ico"; Tasks: desktopicon



[Run]
Filename: "powershell.exe"; Parameters: "-ExecutionPolicy Bypass -File ""{app}\scripts\install-service.ps1"" -AppPath ""{app}"""; StatusMsg: "Hintergrund-Dienst wird eingerichtet..."; Flags: runhidden waituntilterminated
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent; Tasks: launchapp



[UninstallRun]

Filename: "powershell.exe"; Parameters: "-ExecutionPolicy Bypass -File ""{app}\scripts\uninstall-cleanup.ps1"" -AppPath ""{app}"""; Flags: runhidden waituntilterminated runascurrentuser; RunOnceId: "UninstallCleanup"



[UninstallDelete]

Type: filesandordirs; Name: "{app}\Assets"

Type: filesandordirs; Name: "{app}\scripts"

Type: filesandordirs; Name: "{app}\service"

Type: filesandordirs; Name: "{commonappdata}\Win11UpdateBlocker"



[Messages]

german.FinishedLabel=Die Installation wurde erfolgreich abgeschlossen.%n%nWin11 Update Blocker kann sofort verwendet werden — ein Neustart des Computers ist nicht erforderlich.

german.FinishedLabelNoIcons=Die Installation wurde erfolgreich abgeschlossen.%n%nWin11 Update Blocker kann sofort verwendet werden — ein Neustart des Computers ist nicht erforderlich.



[Code]

procedure StopRunningInstances;

var

  ResultCode: Integer;

begin

  Exec('sc.exe', 'stop Win11UpdateBlockerService', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);

  Exec('taskkill.exe', '/IM Win11UpdateBlocker.exe /F', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);

end;



function NeedRestart(): Boolean;

begin

  Result := False;

end;



function VerifyInstallation(): Boolean;

var

  AppDir, MainExe, ServiceExe, IconFile, UiIconFile: String;

begin

  AppDir := ExpandConstant('{app}');

  MainExe := AppDir + '\{#MyAppExeName}';

  ServiceExe := AppDir + '\service\{#MyAppServiceExe}';

  IconFile := AppDir + '\Assets\icon.ico';

  UiIconFile := AppDir + '\Assets\icon.png';



  Result := True;



  if not FileExists(MainExe) then

  begin

    MsgBox('Installation unvollständig: Hauptprogramm fehlt.' + #13#10 + MainExe, mbError, MB_OK);

    Result := False;

    Exit;

  end;



  if not FileExists(ServiceExe) then

  begin

    MsgBox('Installation unvollständig: Hintergrund-Dienst fehlt.' + #13#10 + ServiceExe, mbError, MB_OK);

    Result := False;

    Exit;

  end;



  if not FileExists(IconFile) then

  begin

    MsgBox('Installation unvollständig: Anwendungs-Icon fehlt.' + #13#10 + IconFile, mbError, MB_OK);

    Result := False;

    Exit;

  end;



  if not FileExists(UiIconFile) then

  begin

    MsgBox('Installation unvollständig: UI-Assets fehlen.' + #13#10 + UiIconFile, mbError, MB_OK);

    Result := False;

    Exit;

  end;

end;



procedure WriteConfigFile;

var

  ConfigDir, ConfigPath, ConfigJson: String;

begin

  ConfigDir := ExpandConstant('{commonappdata}\Win11UpdateBlocker');

  ConfigPath := ConfigDir + '\config.json';

  ConfigJson :=

    '{' + #13#10 +

    '  "mode": "allowAll",' + #13#10 +

    '  "preferences": {' + #13#10 +

    '    "allowFeatureUpdates": true,' + #13#10 +

    '    "allowSecurityUpdates": true,' + #13#10 +

    '    "allowQualityUpdates": true,' + #13#10 +

    '    "allowDriverUpdates": true,' + #13#10 +

    '    "allowOptionalUpdates": true' + #13#10 +

    '  },' + #13#10 +

    '  "trayEnabled": true,' + #13#10 +

    '  "autostartEnabled": true,' + #13#10 +

    '  "backgroundServiceEnabled": true,' + #13#10 +

    '  "settingsVersion": 2' + #13#10 +

    '}';

  ForceDirectories(ConfigDir);

  SaveStringToFile(ConfigPath, ConfigJson, False);

end;



procedure CurStepChanged(CurStep: TSetupStep);

begin

  if CurStep = ssPostInstall then

  begin

    WriteConfigFile;

    if not VerifyInstallation() then

      Abort;

  end;

end;



function InitializeSetup(): Boolean;

begin

  Result := True;

end;



function PrepareToInstall(var NeedsRestart: Boolean): String;

begin

  Result := '';

  NeedsRestart := False;

  StopRunningInstances;

end;



function InitializeUninstall(): Boolean;

begin

  Result := True;

end;


