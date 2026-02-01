[Setup]
AppName=Imperial Shield
AppVersion=1.0.6
AppPublisher=Imperial Shield Security (Jonathan Hecl)
AppPublisherURL=https://github.com/jonathanhecl/imperial-shield
DefaultDirName={autopf}\Imperial Shield
DefaultGroupName=Imperial Shield
UninstallDisplayIcon={app}\ImperialShield.exe
Compression=lzma2
SolidCompression=yes
OutputDir=InstallerOutput
OutputBaseFilename=ImperialShield-1.0.6-Setup
SetupIconFile=..\ImperialShield\Resources\shield.ico
LicenseFile=LICENSE.txt
PrivilegesRequired=admin

[Languages]
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"
Name: "startmenuicon"; Description: "Crear acceso directo en el Menú Inicio"; GroupDescription: "{cm:AdditionalIcons}"


[Files]
Source: "..\ImperialShield\bin\Release\net8.0-windows\win-x64\publish\ImperialShield.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\ImperialShield\bin\Release\net8.0-windows\win-x64\publish\appsettings.json"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\ImperialShield\Resources\shield.ico"; DestDir: "{app}\Resources"; Flags: ignoreversion

[Icons]
Name: "{group}\Imperial Shield"; Filename: "{app}\ImperialShield.exe"; IconFilename: "{app}\Resources\shield.ico"; Tasks: startmenuicon
Name: "{autodesktop}\Imperial Shield"; Filename: "{app}\ImperialShield.exe"; IconFilename: "{app}\Resources\shield.ico"; Tasks: desktopicon

[Run]
Filename: "{app}\ImperialShield.exe"; Description: "{cm:LaunchProgram,Imperial Shield}"; Flags: nowait postinstall skipifsilent
