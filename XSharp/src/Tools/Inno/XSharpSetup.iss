; Script generated by the Inno Setup Script Wizard.
; Script generated by the Inno Setup Script Wizard.
; SEE THE DOCUMENTATION FOR DETAILS ON CREATING INNO SETUP SCRIPT FILES!

#define Product         "XSharp"
#define ProdVer         "XSharp 0.2.1"
#define ProdBuild       "XSharp Alpha 0.2.1"
#define Company         "XSharp BV"
#define RegCompany      "XSharpBV"
#define XSharpURL       "http://www.xsharp.info"
#define CopyRight       "Copyright � 2015-2016 XSharp B.V."
#define VIVersion       "0.2.1.2100"
#define VITextVersion   "0.2.1.2100 (Alpha 7)"
#define TouchDate       "2016-02-24"
#define TouchTime       "02:01:00"
#define SetupExeName    "XSharpSetup021"
#define InstallPath     "XSharpPath"

;Folders
#define BinFolder       "D:\Xsharp\Dev\XSharp\Binaries\Debug\"
#define VSProjectFolder "d:\Xsharp\Dev\XSharp\src\VisualStudio\XSharp.ProjectType\"
#define OutPutFolder    "D:\XSharp\Dev\XSharp\Binaries\Setup"
#define DocFolder       "D:\Xsharp\Dev\XSharp\Binaries\Help\"
#define XIDEFolder      "D:\Xsharp\Dev\XSharp\Xide\"
#define XIDESetup       "XIDE_Set_up_1.01.exe"

#define StdFlags        "touch ignoreversion overwritereadonly sortfilesbyextension sortfilesbyname"
;#define Compression     "lzma2/ultra64"
#define Compression     "none"

[Setup]
; NOTE: The value of AppId uniquely identifies this application.
; Do not use the same AppId value in installers for other applications.
; (To generate a new GUID, click Tools | Generate GUID inside the IDE.)
AppId={{32EB192A-B120-4055-800E-74B48B80DA06}
DisableWelcomePage=no
DisableStartupPrompt=yes
DisableReadyMemo=yes
DisableFinishedPage=no
InfoBeforeFile=Baggage\ReadmeShort.rtf
AppName={#Product}
AppVersion={#VIVersion}
AppCopyright={# CopyRight}
AppVerName={#ProdVer}
AppPublisher={#Company}
AppPublisherURL={#XSharpURL}
AppSupportURL={#XSharpURL}
AppUpdatesURL={#XSharpURL}
DefaultDirName={pf}\{#Product}
DefaultGroupName={#Product}
LicenseFile=Baggage\License.rtf
OutputDir={#OutPutFolder} 
OutputBaseFilename={#SetupExeName}
OutputManifestFile=Setup-Manifest.txt
SetupIconFile=Baggage\XSharp.ico
Compression={#Compression}
SolidCompression=yes
SetupLogging=yes

; Version Info for Installer and uninstaller
VersionInfoVersion={#= VIVersion}
VersionInfoDescription={# ProdBuild}
VersionInfoCompany={# Company}
VersionInfoTextVersion={#= VITextVersion}
VersionInfoCopyRight={# CopyRight}
VersionInfoProductName={# Product}
VersionInfoProductVersion={# VIVersion}
Wizardsmallimagefile=Baggage\XSharp_Bmp_Banner.bmp 
WizardImagefile=Baggage\XSharp_Bmp_Dialog.bmp

;Uninstaller
UninstallFilesDir={app}\uninst
UninstallDisplayName={#=ProdBuild}
UninstallDisplayIcon={app}\Images\XSharp.ico;
UninstallLogMode=overwrite


TouchDate={#=TouchDate}
TouchTime={#=TouchTime}




; Make sure they are admins
PrivilegesRequired=admin
; Make sure they are running on Windows 2000 Or Higher
Minversion=6.0.600


[Components]
Name: "main";   Description: "The XSharp Compiler and Build System";  Types: full compact custom; Flags: fixed; 
Name: "vs2015"; Description: "Visual Studio 2015 Integration";        Types: full custom;                  Check: Vs2015IsInstalled;
Name: "xide";   Description: "Include the XIDE files";                Types: full custom;                  


[Dirs]
Name: "{app}\Assemblies"
Name: "{app}\Bin"
Name: "{app}\Help"
Name: "{app}\Images"
Name: "{app}\ProjectSystem"
Name: "{app}\Redist"
Name: "{app}\Uninst"
Name: "{app}\Xide"
Name: "{code:GetVs2015IdeDir}\Extensions\XSharp"; Components: vs2015; 


[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
Source: "{#BinFolder}xsc.exe";                            DestDir: "{app}\bin"; Flags: {#StdFlags}; Components: main
Source: "{#BinFolder}xsc.rsp";                            DestDir: "{app}\bin"; Flags: {#StdFlags}; Components: main
Source: "{#BinFolder}XSCompiler.exe";                     DestDir: "{app}\bin"; Flags: {#StdFlags}; Components: main
Source: "{#BinFolder}XSharp.CodeAnalysis.dll";            DestDir: "{app}\bin"; Flags: {#StdFlags}; Components: main

; PDB files
Source: "{#BinFolder}XSharp.CodeAnalysis.pdb";            DestDir: "{app}\bin"; Flags: {#StdFlags}; Components: main
Source: "{#BinFolder}xsc.pdb";                            DestDir: "{app}\bin"; Flags: {#StdFlags}; Components: main
Source: "{#BinFolder}XSCompiler.pdb";                     DestDir: "{app}\bin"; Flags: {#StdFlags}; Components: main

; GAC files
Source: "{#BinFolder}System.Collections.Immutable.dll";   DestDir: "{app}\bin"; StrongAssemblyName: "System.Collections.Immutable, Version=1.1.37.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a"; Flags: gacinstall sharedfile uninsnosharedfileprompt uninsrestartdelete; components: main
Source: "{#BinFolder}System.Reflection.Metadata.dll";     DestDir: "{app}\bin"; StrongAssemblyName: "System.Reflection.Metadata, Version=1.1.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a";    Flags: gacinstall sharedfile uninsnosharedfileprompt uninsrestartdelete; components: main

; Support files
Source: "Baggage\Readme.rtf";                             DestDir: "{app}"    ; Flags: isreadme {#StdFlags}; Components: main
Source: "Baggage\XSharp.ico";                             DestDir: "{app}\Images"; Flags: touch {#StdFlags}; Components: main
Source: "Baggage\License.rtf";                            DestDir: "{app}";        Flags: touch {#StdFlags}; Components: main
Source: "Baggage\License.txt";                            DestDir: "{app}";        Flags: touch {#StdFlags}; Components: main

;MsBuild Files
Source: "{#VsProjectFolder}BuildSystem\Rules\*.*";                DestDir: "{pf}\MsBuild\{#Product}\Rules";  Flags: {#StdFlags} uninsneveruninstall; Components: main
Source: "{#VsProjectFolder}BuildSystem\DeployedBuildSystem\*.*";  DestDir: "{pf}\MsBuild\{#Product}";        Flags: {#StdFlags} uninsneveruninstall; Components: main

Source: "{#BinFolder}XSharp.Build.dll";               DestDir: "{pf}\MsBuild\{#Product}";        Flags: {#StdFlags} uninsneveruninstall; Components: main


;Documentation
Source: "{#DocFolder}\XSharp.pdf";                     DestDir: "{app}\Help";        Flags: touch {#StdFlags}; Components: main
Source: "{#DocFolder}\XSharp.chm";                     DestDir: "{app}\Help";        Flags: touch {#StdFlags}; Components: main


;XIDE
Source: "{#XIDEFolder}{#XIDESetup}";   DestDir: "{app}\Xide";        Flags: touch {#StdFlags}; Components: Xide


;VsProjectSystem
Source: "{#BinFolder}XSharp.ProjectSystem.vsix";          DestDir: "{app}\ProjectSystem"; Flags: {#StdFlags}; Components: vs2015

Source: "{#BinFolder}XSharp.CodeAnalysis.dll";            DestDir: "{code:GetVs2015IdeDir}"; Flags: {#StdFlags}; Components: vs2015
Source: "{#BinFolder}XSharp.CodeAnalysis.pdb";            DestDir: "{code:GetVs2015IdeDir}"; Flags: {#StdFlags}; Components: vs2015

Source: "{#BinFolder}Itemtemplates\*.*";                        DestDir: "{code:GetVs2015IdeDir}\Extensions\XSharp\ItemTemplates";     Flags: recursesubdirs {#StdFlags}; Components: vs2015
Source: "{#BinFolder}ProjectTemplates\*.*";                     DestDir: "{code:GetVs2015IdeDir}\Extensions\XSharp\ProjectTemplates";  Flags: recursesubdirs {#StdFlags}; Components: vs2015
Source: "{#BinFolder}XSharp.ProjectSystem.DLL";                 DestDir: "{code:GetVs2015IdeDir}\Extensions\XSharp";                   Flags: {#StdFlags}; Components: vs2015
Source: "{#BinFolder}XSharp.ProjectSystem.pkgdef";              DestDir: "{code:GetVs2015IdeDir}\Extensions\XSharp";                   Flags: {#StdFlags}; Components: vs2015
Source: "{#BinFolder}extension.vsixmanifest";                   DestDir: "{code:GetVs2015IdeDir}\Extensions\XSharp";                   Flags: {#StdFlags}; Components: vs2015
Source: "{#BinFolder}XSharp.ico ";                              DestDir: "{code:GetVs2015IdeDir}\Extensions\XSharp";                   Flags: {#StdFlags}; Components: vs2015
Source: "{#VsProjectFolder}Images\XSharpImages.imagemanifest";  DestDir: "{code:GetVs2015IdeDir}\Extensions\XSharp\Images";            Flags: {#StdFlags}; Components: vs2015
Source: "{#BinFolder}XSharp.CodeAnalysis.dll";                  DestDir: "{code:GetVs2015IdeDir}\Extensions\XSharp";                   Flags: {#StdFlags}; Components: vs2015 
;
[Icons]
Name: "{group}\{cm:ProgramOnTheWeb,{#Product}}"; Filename: "{#XSharpURL}";IconFilename:{app}\Images\XSharp.ico;
Name: "{group}\{cm:UninstallProgram,{#Product}}"; Filename: "{uninstallexe}"; 
Name: "{group}\{#Product} Documenation (CHM)"; Filename: "{app}\Help\XSharp.chm"; 
Name: "{group}\{#Product} Documenation (PDF)"; Filename: "{app}\Help\XSharp.pdf"; 
Name: "{group}\{cm:UninstallProgram,{#Product}}"; Filename: "{uninstallexe}"; 

[Registry]
Root: HKLM; Subkey: "Software\{#RegCompany}"; Flags: uninsdeletekeyifempty 
Root: HKLM; Subkey: "Software\{#RegCompany}\{#Product}"; Flags: uninsdeletekey 
Root: HKLM; Subkey: "Software\{#RegCompany}\{#Product}"; ValueName: "{#InstallPath}"; ValueType: string; ValueData: "{app}" ;
;Root: HKCU; Subkey: "Software\Microsoft\VisualStudio\14.0\ExtensionManager"; Flags: deletekey uninsdeletekey; Components: vs2015

[Ini]
Filename: "{code:GetVs2015IdeDir}\Extensions\extensions.configurationchanged"; Section:"XSharp"; Key: "Installed"; String: "1"; Flags: uninsdeletesection; Components: vs2015;

[Run]
Filename:  "{app}\Xide\{#XIDESetup}"; Description:"Run XIDE Installer"; Flags: postInstall;  Components: XIDE;

[UninstallRun]
; This XSharp program deletes the templates cache folder and the extensionmanager key in the registry
;Filename: "{app}\uninst\XsVsUnInst.exe"; Flags: runhidden;  Components: vs2015 ;

[InstallDelete]
; Template cache, component cache and previous installation of our project system
Type: filesandordirs; Name: "{localappdata}\Microsoft\VisualStudio\14.0\vtc"    ; Components: vs2015
Type: filesandordirs; Name: "{localappdata}\Microsoft\VisualStudio\14.0\ComponentModelCache"    ; Components: vs2015
Type: filesandordirs; Name: "{code:GetVs2015IdeDir}\Extensions\XSharp"; Components: vs2015; 

; remove the old uninstaller because the uninstall file format has changed
Type: filesandordirs; Name: "{app}\Uninst"

[UninstallDelete]
Type: filesandordirs; Name: "{app}\Assemblies"                    ; Components: main
Type: filesandordirs; Name: "{app}\Bin"                           ; Components: main
Type: filesandordirs; Name: "{app}\Help"                          ; Components: main
Type: filesandordirs; Name: "{app}\Images"                        ; Components: main
Type: filesandordirs; Name: "{app}\ProjectSystem"                 ; Components: main
Type: filesandordirs; Name: "{app}\Redist"                        ; Components: main
Type: filesandordirs; Name: "{app}\Uninst"                        ; Components: main
Type: filesandordirs; Name: "{pf}\MsBuild\{#Product}"             ; Components: main
Type: filesandordirs; Name: "{code:GetVs2015IdeDir}\Extensions\XSharp"; Components: vs2015;  
Type: dirifempty;     Name: "{app}"; 

; Template cache and component cache
Type: filesandordirs; Name: "{localappdata}\Microsoft\VisualStudio\14.0\vtc"; 			Components: vs2015
Type: filesandordirs; Name: "{localappdata}\Microsoft\VisualStudio\14.0\ComponentModelCache"; 	Components: vs2015

[Messages]
WelcomeLabel1=Welcom to [name] (X#)
WelcomeLabel2=This installer will install [name/ver] on your computer.%n%nIt is recommended that you close all other applications before continuing, especially all running copies of Visual Studio.
WizardInfoBefore=Warning
InfoBeforeLabel=You are about to install Beta software
InfoBeforeClickLabel=Only continue the installation if you are aware of the following:


[Code]
Program setup;
var
  Vs2015Path : String;
  Vs2015Installed: Boolean;
  Vs2015BaseDir: String;

procedure DetectVS();
begin
  Vs2015Installed := RegQueryStringValue(HKEY_LOCAL_MACHINE,'SOFTWARE\Microsoft\VisualStudio\SxS\VS7','14.0',Vs2015BaseDir) ;
  if Vs2015Installed then Vs2015Path := Vs2015BaseDir+'\Common7\Ide\';
end;


{function ResetExtensionManager: Boolean;
begin
  RegDeleteKeyIncludingSubKeys(HKEY_CURRENT_USER,'SOFTWARE\Microsoft\VisualStudio\14.0\ExtensionManager');
  result := True;
end;
}
function Vs2015IsInstalled: Boolean;
begin
  result := Vs2015Installed;
end;

function GetVs2015IdeDir(Param: String): String;
begin
  result := Vs2015Path;
end;



function InitializeSetup(): Boolean;
var
  ErrorCode: Integer;
begin
  DetectVS();
  result := true;
  if not Vs2015Installed then
  begin
    if MsgBox('Visual Studio 2015 has not been detected, do you want to download the free Visual Studio Community Edition ?', mbConfirmation, MB_YESNO) = IDYES then
    begin
    ShellExec('open','https://www.visualstudio.com/en-us/downloads/download-visual-studio-vs.aspx','','',SW_SHOW,ewWaitUntilIdle, ErrorCode);
    result := false;
    end
  end;
  
end;
#expr SaveToFile(AddBackslash(SourcePath) + "Preprocessed.iss")
