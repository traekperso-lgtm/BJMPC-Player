; BJMPC Player - installateur (Inno Setup 6)
; Compilé par GitHub Actions : ISCC /DAppVersion=x.y.z /DSourceDir=publish [/DSIGN] installer\BJMPCPlayer.iss
; Installation par utilisateur (aucun droit administrateur), comme tools\install.ps1.

#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif
#ifndef SourceDir
  #define SourceDir "..\publish"
#endif

[Setup]
AppId={{6E1D3C4A-8B2F-4C1E-9A57-B1F0C3D2E4A1}
AppName=BJMPC Player
AppVersion={#AppVersion}
AppVerName=BJMPC Player {#AppVersion}
AppPublisher=BJMPC Informatique
AppPublisherURL=https://github.com/traekperso-lgtm/BJMPC-Player
AppCopyright=BJMPC Informatique
VersionInfoVersion={#AppVersion}
VersionInfoCompany=BJMPC Informatique
VersionInfoDescription=Installateur de BJMPC Player
DefaultDirName={autopf}\BJMPC Player
DefaultGroupName=BJMPC Player
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
OutputDir=..\installer-out
OutputBaseFilename=BJMPC-Player-{#AppVersion}-Setup
SetupIconFile=..\src\BJMPCPlayer\Assets\BJMPCPlayer.ico
UninstallDisplayIcon={app}\BJMPCPlayer.exe
UninstallDisplayName=BJMPC Player
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
ChangesAssociations=yes
#ifdef SIGN
SignTool=bjmpc
SignedUninstaller=yes
#endif

[Languages]
Name: "fr"; MessagesFile: "compiler:Languages\French.isl"

[Tasks]
Name: "desktopicon"; Description: "Créer un raccourci sur le Bureau"; GroupDescription: "Raccourcis :"
Name: "sendto"; Description: "Ajouter BJMPC Player au menu « Envoyer vers »"; GroupDescription: "Raccourcis :"

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\BJMPC Player"; Filename: "{app}\BJMPCPlayer.exe"; Comment: "Lecteur multimédia BJMPC"
Name: "{group}\Désinstaller BJMPC Player"; Filename: "{uninstallexe}"
Name: "{autodesktop}\BJMPC Player"; Filename: "{app}\BJMPCPlayer.exe"; Tasks: desktopicon
Name: "{usersendto}\BJMPC Player"; Filename: "{app}\BJMPCPlayer.exe"; Tasks: sendto

[Registry]
; « Ouvrir avec » pour les formats audio et vidéo
Root: HKA; Subkey: "Software\Classes\Applications\BJMPCPlayer.exe"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\Applications\BJMPCPlayer.exe"; ValueType: string; ValueName: "FriendlyAppName"; ValueData: "BJMPC Player"
Root: HKA; Subkey: "Software\Classes\Applications\BJMPCPlayer.exe\DefaultIcon"; ValueType: string; ValueData: "{app}\BJMPCPlayer.exe,0"
Root: HKA; Subkey: "Software\Classes\Applications\BJMPCPlayer.exe\shell\open\command"; ValueType: string; ValueData: """{app}\BJMPCPlayer.exe"" ""%1"""
Root: HKA; Subkey: "Software\Classes\Applications\BJMPCPlayer.exe\SupportedTypes"; ValueType: string; ValueName: ".mp3"; ValueData: ""
Root: HKA; Subkey: "Software\Classes\.mp3\OpenWithList\BJMPCPlayer.exe"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\Applications\BJMPCPlayer.exe\SupportedTypes"; ValueType: string; ValueName: ".flac"; ValueData: ""
Root: HKA; Subkey: "Software\Classes\.flac\OpenWithList\BJMPCPlayer.exe"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\Applications\BJMPCPlayer.exe\SupportedTypes"; ValueType: string; ValueName: ".wav"; ValueData: ""
Root: HKA; Subkey: "Software\Classes\.wav\OpenWithList\BJMPCPlayer.exe"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\Applications\BJMPCPlayer.exe\SupportedTypes"; ValueType: string; ValueName: ".m4a"; ValueData: ""
Root: HKA; Subkey: "Software\Classes\.m4a\OpenWithList\BJMPCPlayer.exe"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\Applications\BJMPCPlayer.exe\SupportedTypes"; ValueType: string; ValueName: ".aac"; ValueData: ""
Root: HKA; Subkey: "Software\Classes\.aac\OpenWithList\BJMPCPlayer.exe"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\Applications\BJMPCPlayer.exe\SupportedTypes"; ValueType: string; ValueName: ".ogg"; ValueData: ""
Root: HKA; Subkey: "Software\Classes\.ogg\OpenWithList\BJMPCPlayer.exe"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\Applications\BJMPCPlayer.exe\SupportedTypes"; ValueType: string; ValueName: ".opus"; ValueData: ""
Root: HKA; Subkey: "Software\Classes\.opus\OpenWithList\BJMPCPlayer.exe"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\Applications\BJMPCPlayer.exe\SupportedTypes"; ValueType: string; ValueName: ".wma"; ValueData: ""
Root: HKA; Subkey: "Software\Classes\.wma\OpenWithList\BJMPCPlayer.exe"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\Applications\BJMPCPlayer.exe\SupportedTypes"; ValueType: string; ValueName: ".aiff"; ValueData: ""
Root: HKA; Subkey: "Software\Classes\.aiff\OpenWithList\BJMPCPlayer.exe"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\Applications\BJMPCPlayer.exe\SupportedTypes"; ValueType: string; ValueName: ".ape"; ValueData: ""
Root: HKA; Subkey: "Software\Classes\.ape\OpenWithList\BJMPCPlayer.exe"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\Applications\BJMPCPlayer.exe\SupportedTypes"; ValueType: string; ValueName: ".wv"; ValueData: ""
Root: HKA; Subkey: "Software\Classes\.wv\OpenWithList\BJMPCPlayer.exe"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\Applications\BJMPCPlayer.exe\SupportedTypes"; ValueType: string; ValueName: ".mka"; ValueData: ""
Root: HKA; Subkey: "Software\Classes\.mka\OpenWithList\BJMPCPlayer.exe"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\Applications\BJMPCPlayer.exe\SupportedTypes"; ValueType: string; ValueName: ".ac3"; ValueData: ""
Root: HKA; Subkey: "Software\Classes\.ac3\OpenWithList\BJMPCPlayer.exe"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\Applications\BJMPCPlayer.exe\SupportedTypes"; ValueType: string; ValueName: ".eac3"; ValueData: ""
Root: HKA; Subkey: "Software\Classes\.eac3\OpenWithList\BJMPCPlayer.exe"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\Applications\BJMPCPlayer.exe\SupportedTypes"; ValueType: string; ValueName: ".dts"; ValueData: ""
Root: HKA; Subkey: "Software\Classes\.dts\OpenWithList\BJMPCPlayer.exe"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\Applications\BJMPCPlayer.exe\SupportedTypes"; ValueType: string; ValueName: ".thd"; ValueData: ""
Root: HKA; Subkey: "Software\Classes\.thd\OpenWithList\BJMPCPlayer.exe"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\Applications\BJMPCPlayer.exe\SupportedTypes"; ValueType: string; ValueName: ".dsf"; ValueData: ""
Root: HKA; Subkey: "Software\Classes\.dsf\OpenWithList\BJMPCPlayer.exe"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\Applications\BJMPCPlayer.exe\SupportedTypes"; ValueType: string; ValueName: ".m4b"; ValueData: ""
Root: HKA; Subkey: "Software\Classes\.m4b\OpenWithList\BJMPCPlayer.exe"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\Applications\BJMPCPlayer.exe\SupportedTypes"; ValueType: string; ValueName: ".mid"; ValueData: ""
Root: HKA; Subkey: "Software\Classes\.mid\OpenWithList\BJMPCPlayer.exe"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\Applications\BJMPCPlayer.exe\SupportedTypes"; ValueType: string; ValueName: ".mkv"; ValueData: ""
Root: HKA; Subkey: "Software\Classes\.mkv\OpenWithList\BJMPCPlayer.exe"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\Applications\BJMPCPlayer.exe\SupportedTypes"; ValueType: string; ValueName: ".mp4"; ValueData: ""
Root: HKA; Subkey: "Software\Classes\.mp4\OpenWithList\BJMPCPlayer.exe"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\Applications\BJMPCPlayer.exe\SupportedTypes"; ValueType: string; ValueName: ".m4v"; ValueData: ""
Root: HKA; Subkey: "Software\Classes\.m4v\OpenWithList\BJMPCPlayer.exe"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\Applications\BJMPCPlayer.exe\SupportedTypes"; ValueType: string; ValueName: ".avi"; ValueData: ""
Root: HKA; Subkey: "Software\Classes\.avi\OpenWithList\BJMPCPlayer.exe"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\Applications\BJMPCPlayer.exe\SupportedTypes"; ValueType: string; ValueName: ".mov"; ValueData: ""
Root: HKA; Subkey: "Software\Classes\.mov\OpenWithList\BJMPCPlayer.exe"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\Applications\BJMPCPlayer.exe\SupportedTypes"; ValueType: string; ValueName: ".wmv"; ValueData: ""
Root: HKA; Subkey: "Software\Classes\.wmv\OpenWithList\BJMPCPlayer.exe"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\Applications\BJMPCPlayer.exe\SupportedTypes"; ValueType: string; ValueName: ".webm"; ValueData: ""
Root: HKA; Subkey: "Software\Classes\.webm\OpenWithList\BJMPCPlayer.exe"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\Applications\BJMPCPlayer.exe\SupportedTypes"; ValueType: string; ValueName: ".flv"; ValueData: ""
Root: HKA; Subkey: "Software\Classes\.flv\OpenWithList\BJMPCPlayer.exe"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\Applications\BJMPCPlayer.exe\SupportedTypes"; ValueType: string; ValueName: ".ts"; ValueData: ""
Root: HKA; Subkey: "Software\Classes\.ts\OpenWithList\BJMPCPlayer.exe"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\Applications\BJMPCPlayer.exe\SupportedTypes"; ValueType: string; ValueName: ".m2ts"; ValueData: ""
Root: HKA; Subkey: "Software\Classes\.m2ts\OpenWithList\BJMPCPlayer.exe"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\Applications\BJMPCPlayer.exe\SupportedTypes"; ValueType: string; ValueName: ".mts"; ValueData: ""
Root: HKA; Subkey: "Software\Classes\.mts\OpenWithList\BJMPCPlayer.exe"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\Applications\BJMPCPlayer.exe\SupportedTypes"; ValueType: string; ValueName: ".mpg"; ValueData: ""
Root: HKA; Subkey: "Software\Classes\.mpg\OpenWithList\BJMPCPlayer.exe"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\Applications\BJMPCPlayer.exe\SupportedTypes"; ValueType: string; ValueName: ".mpeg"; ValueData: ""
Root: HKA; Subkey: "Software\Classes\.mpeg\OpenWithList\BJMPCPlayer.exe"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\Applications\BJMPCPlayer.exe\SupportedTypes"; ValueType: string; ValueName: ".vob"; ValueData: ""
Root: HKA; Subkey: "Software\Classes\.vob\OpenWithList\BJMPCPlayer.exe"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\Applications\BJMPCPlayer.exe\SupportedTypes"; ValueType: string; ValueName: ".ogv"; ValueData: ""
Root: HKA; Subkey: "Software\Classes\.ogv\OpenWithList\BJMPCPlayer.exe"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\Applications\BJMPCPlayer.exe\SupportedTypes"; ValueType: string; ValueName: ".3gp"; ValueData: ""
Root: HKA; Subkey: "Software\Classes\.3gp\OpenWithList\BJMPCPlayer.exe"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\Applications\BJMPCPlayer.exe\SupportedTypes"; ValueType: string; ValueName: ".divx"; ValueData: ""
Root: HKA; Subkey: "Software\Classes\.divx\OpenWithList\BJMPCPlayer.exe"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\Applications\BJMPCPlayer.exe\SupportedTypes"; ValueType: string; ValueName: ".m3u"; ValueData: ""
Root: HKA; Subkey: "Software\Classes\.m3u\OpenWithList\BJMPCPlayer.exe"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\Applications\BJMPCPlayer.exe\SupportedTypes"; ValueType: string; ValueName: ".m3u8"; ValueData: ""
Root: HKA; Subkey: "Software\Classes\.m3u8\OpenWithList\BJMPCPlayer.exe"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\Applications\BJMPCPlayer.exe\SupportedTypes"; ValueType: string; ValueName: ".pls"; ValueData: ""
Root: HKA; Subkey: "Software\Classes\.pls\OpenWithList\BJMPCPlayer.exe"; Flags: uninsdeletekey

[Run]
Filename: "{app}\BJMPCPlayer.exe"; Description: "Lancer BJMPC Player"; Flags: nowait postinstall skipifsilent

[Messages]
fr.FinishedLabel=BJMPC Player est installé.%n%nAstuce : clic droit sur un film ou une musique > Ouvrir avec > BJMPC Player (cochez « Toujours » pour en faire le lecteur par défaut).
