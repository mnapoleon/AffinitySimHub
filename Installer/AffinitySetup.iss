#ifndef MyAppVersion
#define MyAppVersion "0.0.0"
#endif

#ifndef MyOutputDir
#define MyOutputDir "..\artifacts\installer"
#endif

[Setup]
AppId={{02C6CCD6-8DA8-482B-9848-9EA31BEB82AD}
AppName=Affinity SimHub Plugin
AppPublisher=AffinitySimHub
AppVersion={#MyAppVersion}
DefaultDirName={pf32}\SimHub
DefaultGroupName=Affinity SimHub Plugin
DisableProgramGroupPage=yes
OutputDir={#MyOutputDir}
OutputBaseFilename=AffinitySetup-v{#MyAppVersion}
Compression=lzma2
SolidCompression=yes
PrivilegesRequired=admin
UninstallDisplayName=Affinity SimHub Plugin
UninstallDisplayIcon={app}\Affinity.dll
WizardStyle=modern

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
Source: "..\Affinity\bin\Release\net48\Affinity.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\Affinity\bin\Release\net48\Affinity.pdb"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist
Source: "..\Affinity\bin\Release\net48\ac_track_id_map.json"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\Affinity\bin\Release\net48\System.Data.SQLite.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\Affinity\bin\Release\net48\x64\SQLite.Interop.dll"; DestDir: "{app}\x64"; Flags: ignoreversion
Source: "..\Affinity\bin\Release\net48\x86\SQLite.Interop.dll"; DestDir: "{app}\x86"; Flags: ignoreversion
Source: "..\Affinity\bin\Release\net48\x64\SQLite.Interop.dll"; DestDir: "{app}\PluginsData\Affinity\sqlite-native\x64"; Flags: ignoreversion
Source: "..\Affinity\bin\Release\net48\x86\SQLite.Interop.dll"; DestDir: "{app}\PluginsData\Affinity\sqlite-native\x86"; Flags: ignoreversion

[Icons]
Name: "{group}\Uninstall Affinity SimHub Plugin"; Filename: "{uninstallexe}"

[Messages]
FinishedHeadingLabel=Affinity SimHub Plugin installed
FinishedLabelNoIcons=Affinity and its SQLite dependencies were installed into the selected SimHub folder. Start or restart SimHub to load the plugin.
