[Setup]
AppName=Imperial Shield
AppVersion=1.0.5
AppPublisher=Imperial Shield Security (Jonathan Hecl)
AppPublisherURL=https://github.com/jonathanhecl/imperial-shield
DefaultDirName={autopf}\Imperial Shield
DefaultGroupName=Imperial Shield
UninstallDisplayIcon={app}\ImperialShield.exe
Compression=lzma2
SolidCompression=yes
SetupIconFile=..\ImperialShield\Resources\shield.ico
LicenseFile=LICENSE.txt
PrivilegesRequired=admin

[Languages]
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"

[Files]
Source: "..\ImperialShield\bin\Release\net8.0-windows\win-x64\publish\ImperialShield.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\ImperialShield\bin\Release\net8.0-windows\win-x64\publish\appsettings.json"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\ImperialShield\Resources\shield.ico"; DestDir: "{app}\Resources"; Flags: ignoreversion

[Icons]
Name: "{group}\Imperial Shield"; Filename: "{app}\ImperialShield.exe"; IconFilename: "{app}\Resources\shield.ico"
Name: "{autodesktop}\Imperial Shield"; Filename: "{app}\ImperialShield.exe"; IconFilename: "{app}\Resources\shield.ico"

[Run]
Filename: "{app}\ImperialShield.exe"; Description: "{cm:LaunchProgram,Imperial Shield}"; Flags: nowait postinstall skipifsilent
