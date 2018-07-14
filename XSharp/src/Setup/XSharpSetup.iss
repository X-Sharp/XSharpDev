;
; Note that all folder name variables include the trailing backslash !
;

; mssigntool = "c:\Program Files (x86)\Windows Kits\10\bin\10.0.17134.0\x86\signtool.exe"   $p


#ifndef Compression
#define Compression     "lzma/fast"
#endif
#define FOX
;#undef FOX


; version info and similar stuff.
  
#define Version             "2.0.0.3"
#define FileNameVersion     "2Beta3"
#define VIVersion           "2.0.0.3"
#define TouchDate           "2018-07-14"
#define TouchTime           "02:00:03"

#define DevFolder           "C:\Xsharp\Dev\XSharp"
#define DevPublicFolder     "C:\Xsharp\DevPublic"
#define SetupFolder         DevFolder + "\src\Setup"

#define BinPFolder          DevPublicFolder + "\Binaries\Release\"
#define BinRtFolder         "C:\Xsharp\DevRt\Binaries\Release\"
#define BinRtDFolder        "C:\Xsharp\DevRt\Binaries\Debug\"
#define BinRtHelpFolder     "C:\Xsharp\DevRt\Binaries\Help\"

#define ObjFolder           DevPublicFolder +"\Binaries\Obj\"
#define DebuggerObjFolder   ObjFolder + "Release\XSharpDebugger\"
#define ProjectObjFolder    ObjFolder + "Release\XSharpProject\"
#define BinDFolder          DevFolder + "\Binaries\Debug_AnyCPU\"
#ifdef FOX
#define Suffix              "Fox"
#define BinFolder           DevFolder + "\Binaries\Release_AnyCPU\"
#else   
#define Suffix              "Public"
#define BinFolder           DevFolder + "\Binaries\Debug_AnyCPU\"
#endif

#define SetupExeName        "XSharpSetup"+FileNameVersion+Suffix

#define Product             "XSharp"
#define ProdBuild           "XSharp Bandol Beta version "+ Version
#define Company             "XSharp BV"
#define RegCompany          "XSharpBV"
#define XSharpURL           "http://www.xsharp.info"
#define CopyRight           "Copyright � 2015-2018 XSharp B.V."
#define InstallPath         "XSharpPath"

; Code Signing
#define KeyFile             DevFolder + "\build\Signatures\XSharpCert.pfx"
#define TimeStampURL        "http://timestamp.globalsign.com/scripts/timstamp.dll"
#define KeyPassword         "J1O39dGG6FPLXWj"
#define Description         "XSharp, xBase compiler for .Net"

;Registry key for the PATH setting
#define EnvironmentKey      "'SYSTEM\CurrentControlSet\Control\Session Manager\Environment'";

;Source Folders and other related stuff

#define CommonFolder        DevFolder + "\src\Common\"
#define ToolsFolder         DevFolder + "\src\Tools\"
#define VOXporterFolder     DevPublicFolder + "\Binaries\Release\"
#define VOXporterBinFolder  DevPublicFolder + "\Binaries\Release\"
#define ExamplesFolder      DevPublicFolder + "\Samples\"
#define ScriptFolder        DevPublicFolder + "\ScriptSamples\"
#define OutPutFolder        DevFolder + "\Binaries\Setup"
#define DocFolder           DevFolder + "\Binaries\Help\"
#define XIDEFolder          DevFolder + "\Xide\"
#define SnippetsSource      DevPublicFolder + "\VisualStudio\ProjectPackage\Snippets\"
#define XIDESetup           "XIDE_Set_up_1.14.exe"
#define XIDEVersion         "1.14"
#define StdFlags            "ignoreversion overwritereadonly sortfilesbyextension sortfilesbyname touch uninsremovereadonly"
#define GACInstall          "gacinstall uninsnosharedfileprompt uninsrestartdelete"
#define GACShared           "gacinstall sharedfile uninsnosharedfileprompt uninsrestartdelete"
#define ImmutableVersion    "System.Collections.Immutable, Version=1.2.2.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a"
#define MetadataVersion     "System.Reflection.Metadata, Version=1.4.2.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a"
#define ValueTupleVersion   "System.ValueTuple, Version=4.0.1.1, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51"
#define XSharpVersion       ", Version="+Version+", Culture=neutral, PublicKeyToken=ed555a0467764586, processorArchitecture=MSIL" 


;
; preprocess the help cab files
;
#expr Exec( SetupFolder+'\makecabs.cmd')
#expr Exec( 'taskkill  /f /t /fi "IMAGENAME eq XSCompiler.exe"')

; Write version to VSIX files
; The updatevsix.exe is created from updatevsix.prg in this folder
#expr Exec(SetupFolder+'\updatevsix.exe', '"' + VIVersion + '" "' + DebuggerObjFolder + 'extension.vsixmanifest"' ,SetupFolder,1,sw_ShowNormal)
#expr Exec(SetupFolder+'\updatevsix.exe', '"' + VIVersion + '" "' + ProjectObjFolder  + 'extension.vsixmanifest"' ,SetupFolder,1,sw_ShowNormal)

; Registry name for prgx extension
#define XSScript            "XSharpScript"

; FOlders and registry keys defined by others
#define VulcanEditorGuid     "Editors\{{e6787d5e-718e-4810-9c26-7cc920baa335}\Extensions"
#define VulcanWedGuid        "Editors\{{e9eecf7e-7aa2-490e-affc-c55fa2acc5a3}\Extensions"
#define VulcanMedGuid        "Editors\{{adee1755-5ac3-485b-b857-f82d902362ca}\Extensions"
#define VulcanDedGuid        "Editors\{{5325db94-5d6c-41fd-be44-c5b277612ce6}\Extensions"
#define VulcanFedGuid        "Editors\{{4849278c-aacb-4bbe-9a15-d96da837aeb7}\Extensions"
#define VS14RegPath          "Software\Microsoft\VisualStudio\14.0"
#define VS14LocalDir         "{localappdata}\Microsoft\VisualStudio\14.0"
#define VS15LocalDir         "{localappdata}\Microsoft\VisualStudio\15.0_"
#define SnippetsPath         "\Snippets\1033"

; Snippets of code for the Help installer.
#define HelpInstall1  "/operation install /silent /catalogname "
#define HelpInstall2  "/locale en-us /sourceuri """"{app}\help\XSharp.msha"""" /wait 0"
#define HelpUninstall1 "/silent /operation uninstall /catalogname"
#define HelpUninstall2 "/locale en-us /vendor """"XSharp"""" /productname """"X#"""" /booklist """"X# Documentation"""" /wait 0"


[Setup]
; NOTE: The value of AppId uniquely identifies this application.
; Do not use the same AppId value in installers for other applications.
; (To generate a new GUID, click Tools | Generate GUID inside the IDE.)
AppId={{32EB192A-B120-4055-800E-74B48B80DA06}
DisableWelcomePage=no
DisableStartupPrompt=yes
DisableReadyMemo=yes
DisableFinishedPage=no
InfoBeforeFile=Baggage\ReadmeShort{# Suffix}.rtf
AppName={#Product}
AppVersion={#Version}
AppCopyright={# CopyRight}
AppVerName={#Product} {#Version}
AppPublisher={#Company}
AppPublisherURL={#XSharpURL}
AppSupportURL={#XSharpURL}
AppUpdatesURL={#XSharpURL}
DefaultDirName={pf}\{#Product}
DefaultGroupName={#Product}
LicenseFile=Baggage\License.txt
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
VersionInfoTextVersion={#= VIVersion}
VersionInfoCopyRight={# CopyRight}
VersionInfoProductName={# Product}
VersionInfoProductVersion={# VIVersion}
Wizardsmallimagefile=Baggage\XSharp_Bmp_Banner.bmp 
WizardImagefile=Baggage\XSharp_Bmp_Dialog.bmp

;Uninstaller
UninstallFilesDir={app}\uninst
UninstallDisplayName={#=ProdBuild}
UninstallDisplayIcon={app}\Images\XSharp.ico
UninstallLogMode=append


TouchDate={#=TouchDate}
TouchTime={#=TouchTime}                               


; Make sure they are admins
PrivilegesRequired=admin
UsedUserAreasWarning=false
; Make sure they are running on Windows 2000 Or Higher
Minversion=6.0.600

; Code Signing
Signtool=mssigntool sign /f {# KeyFile} /p {# Keypassword}  /t {# TimeStampURL}  /d "{# Description}" $f        
SignToolRetryCount=5
; Tell windows that we associte the prgx extension
ChangesAssociations=yes
ChangesEnvironment=yes


[Components]
Name: "main";             Description: "The XSharp Compiler and Build System";        Types: full compact custom; Flags: fixed; 
Name: "main\script";      Description: "Register .prgx as X# Script extension";       Types: full custom;  
Name: "main\ngen";        Description: "Optimize performance by generating native images"; Types: full custom;  
Name: "vs2015";           Description: "Visual Studio 2015 Integration";              Types: full custom;         Check: Vs2015IsInstalled;
Name: "vs2015\help";      Description: "Install VS 2015 documentation";               Types: full custom;         Check: HelpViewer22Found;
Name: "vs2017";           Description: "Visual Studio 2017 Integration";              Types: full custom;         Check: vs2017IsInstalled;
Name: "vs2017\help";      Description: "Install VS 2017 documentation";               Types: full custom;         Check: vs2017IsInstalled;
Name: "xide";             Description: "Include the XIDE {# XIDEVersion} installer";  Types: full custom;                  



;[Tasks]
;Name: envPath; Description: "Add XSharp to your PATH variable" 
;Name: desktopicon; Description: "Create a &desktop icon"; GroupDescription: "Additional icons:"; Components: main
;Name: desktopicon\common; Description: "For all users"; GroupDescription: "Additional icons:"; Components: main; Flags: exclusive
;Name: desktopicon\user; Description: "For the current user only"; GroupDescription: "Additional icons:"; Components: main; Flags: exclusive unchecked
;Name: quicklaunchicon; Description: "Create a &Quick Launch icon"; GroupDescription: "Additional icons:"; Components: main; Flags: unchecked
;Name: associate; Description: "&Associate files"; GroupDescription: "Other tasks:"; Flags: unchecked

[Dirs]
Name: "{app}\Assemblies";
Name: "{app}\Bin";
#ifdef FOX
Name: "{app}\Debug";
#endif
Name: "{app}\Help";
Name: "{app}\Images";
Name: "{app}\Include";
Name: "{app}\Portable";
Name: "{app}\ProjectSystem";                                
Name: "{app}\Redist"
Name: "{app}\Snippets"
Name: "{app}\Tools";
Name: "{app}\Uninst";
Name: "{app}\Xide";
Components: vs2015; Name: "{code:GetVs2015IdeDir}\Extensions\XSharp";                              
Components: vs2015; Name: "{userdocs}\Visual Studio 2015\Code Snippets\XSharp\My Code Snippets";   
Components: vs2017; Name: "{code:GetVs2017IdeDir|1}\Extensions\XSharp";    Check: HasVs2017('1');
Components: vs2017; Name: "{code:GetVs2017IdeDir|2}\Extensions\XSharp";    Check: HasVs2017('1');
Components: vs2017; Name: "{code:GetVs2017IdeDir|3}\Extensions\XSharp";    Check: HasVs2017('1');
Components: vs2017; Name: "{userdocs}\Visual Studio 2017\Code Snippets\XSharp\My Code Snippets";   
; user template folders
Components: vs2015; Name: "{userdocs}\Visual Studio 2015\Templates\ProjectTemplates\XSharp";   
Components: vs2015; Name: "{userdocs}\Visual Studio 2015\Templates\ItemTemplates\XSharp";   
Components: vs2017; Name: "{userdocs}\Visual Studio 2017\Templates\ProjectTemplates\XSharp";   
Components: vs2017; Name: "{userdocs}\Visual Studio 2017\Templates\ItemTemplates\XSharp";   


[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
; Main program
; Text files, independent of Debug/Release and independent of Fox
Source: "{#BinFolder}xsc.rsp";                            DestDir: "{app}\bin"; Flags: {#StdFlags};   
Source: "{#BinFolder}xsi.rsp";                            DestDir: "{app}\bin"; Flags: {#StdFlags}; 
Source: "{#BinFolder}xsc.exe.config";                     DestDir: "{app}\bin"; Flags: {#StdFlags}; 
Source: "{#BinFolder}xsi.exe.config";                     DestDir: "{app}\bin"; Flags: {#StdFlags}; 
Source: "{#BinFolder}XSCompiler.exe.config ";             DestDir: "{app}\bin"; Flags: {#StdFlags}; 


Source: "{#BinFolder}xsc.exe";                            DestDir: "{app}\bin"; Flags: {#StdFlags} signonce ; 
Source: "{#BinFolder}xsc.pdb";                            DestDir: "{app}\bin"; Flags: {#StdFlags}; 
Source: "{#BinFolder}XSCompiler.exe";                     DestDir: "{app}\bin"; Flags: {#StdFlags} signonce ; BeforeInstall: TaskKill('xscompiler.exe');
Source: "{#BinFolder}XSCompiler.pdb";                     DestDir: "{app}\bin"; Flags: {#StdFlags}; 
Source: "{#BinFolder}XSharp.CodeAnalysis.dll";            DestDir: "{app}\bin"; Flags: {#StdFlags} signonce ; 
Source: "{#BinFolder}XSharp.CodeAnalysis.pdb";            DestDir: "{app}\bin"; Flags: {#StdFlags}; 
Source: "{#BinFolder}xsi.exe";                            DestDir: "{app}\bin"; Flags: {#StdFlags} signonce ; 
Source: "{#BinFolder}xsi.pdb";                            DestDir: "{app}\bin"; Flags: {#StdFlags}; 
Source: "{#BinFolder}XSharp.Scripting.dll";               DestDir: "{app}\bin"; Flags: {#StdFlags} signonce ; 
Source: "{#BinFolder}XSharp.Scripting.pdb";               DestDir: "{app}\bin"; Flags: {#StdFlags}; 

Source: "{#BinFolder}Portable\XSharp.CodeAnalysis.dll";   DestDir: "{app}\Portable"; Flags: {#StdFlags} signonce ; 
Source: "{#BinFolder}Portable\XSharp.CodeAnalysis.pdb";   DestDir: "{app}\Portable"; Flags: {#StdFlags}; 
Source: "{#BinFolder}Portable\XSharp.Scripting.dll";      DestDir: "{app}\Portable"; Flags: {#StdFlags} signonce ; 
Source: "{#BinFolder}Portable\XSharp.Scripting.pdb";      DestDir: "{app}\Portable"; Flags: {#StdFlags}; 


; GAC files in Bin folder, must be found by intellisense or Macro compiler as well
Source: "{#BinFolder}System.Collections.Immutable.dll";   DestDir: "{app}\bin"; StrongAssemblyName: "{#ImmutableVersion}";  Flags: {#StdFlags} {#GACShared};
Source: "{#BinFolder}System.Reflection.Metadata.dll";     DestDir: "{app}\bin"; StrongAssemblyName: "{#MetadataVersion}";   Flags: {#StdFlags} {#GACShared};
Source: "{#BinFolder}System.Valuetuple.dll";              DestDir: "{app}\bin"; StrongAssemblyName: "{#ValueTupleVersion}"; Flags: {#StdFlags} {#GACShared};

; 'Normal' Ms Runtime DLLs needed by 4.6 version
Source: "{#BinFolder}Microsoft.DiaSymReader.*.Dll";       DestDir: "{app}\bin"; Flags: {#StdFlags} ; 
Source: "{#BinFolder}System.Security.Crypt*.Alg*.dll";    DestDir: "{app}\bin"; Flags: {#StdFlags} ;
Source: "{#BinFolder}System.Text.Encoding.*.dll";         DestDir: "{app}\bin"; Flags: {#StdFlags} ;

; 'Normal' Ms Runtime DLLs needed by Portable Version
Source: "{#BinFolder}Microsoft.DiaSymReader.*.Dll";       DestDir: "{app}\portable"; Flags: {#StdFlags} ; 
Source: "{#BinFolder}System.AppContext.dll";              DestDir: "{app}\portable"; Flags: {#StdFlags} ; 
Source: "{#BinFolder}System.Composition.*.dll";           DestDir: "{app}\portable"; Flags: {#StdFlags} ;
Source: "{#BinFolder}System.Console.dll";                 DestDir: "{app}\portable"; Flags: {#StdFlags} ;
Source: "{#BinFolder}System.Diagnostics.*.dll";           DestDir: "{app}\portable"; Flags: {#StdFlags} ;
Source: "{#BinFolder}System.IO.*.dll";                    DestDir: "{app}\portable"; Flags: {#StdFlags} ;
Source: "{#BinFolder}System.Security.*.dll";              DestDir: "{app}\portable"; Flags: {#StdFlags} ;
Source: "{#BinFolder}System.Text.*.dll";                  DestDir: "{app}\portable"; Flags: {#StdFlags} ;
Source: "{#BinFolder}System.Threading.*.dll";             DestDir: "{app}\portable"; Flags: {#StdFlags} ;
Source: "{#BinFolder}System.Valuetuple.dll";              DestDir: "{app}\portable"; Flags: {#StdFlags} ;
Source: "{#BinFolder}System.Xml.*.dll";                   DestDir: "{app}\portable"; Flags: {#StdFlags} ;


; native resource compiler
Source: "{#BinPFolder}baggage\rc.exe";                    DestDir: "{app}\bin"; Flags: {#StdFlags}; 
Source: "{#BinPFolder}baggage\rcdll.dll";                 DestDir: "{app}\bin"; Flags: {#StdFlags}; 

; Vulcan - VO Exporter
Source: "{#BinPFolder}xporter.exe";                       DestDir: "{app}\bin"; Flags: {#StdFlags} signonce; 
Source: "{#BinPFolder}xporter.pdb";                       DestDir: "{app}\bin"; Flags: {#StdFlags}; 
;
; VO XPorter
;
Source: "{#VOXPorterBinFolder}VOXporter.exe";             DestDir: "{app}\VOXPorter"; Flags: {#StdFlags} signonce; 
Source: "{#VOXPorterBinFolder}Fab_VO_Entities.dll";       DestDir: "{app}\VOXPorter"; Flags: {#StdFlags} ; 
Source: "{#VOXPorterBinFolder}XI*.dll";                   DestDir: "{app}\VOXPorter"; Flags: {#StdFlags} ; 
Source: "{#VOXPorterBinFolder}SDK_DEFINES.dll";           DestDir: "{app}\VOXPorter"; Flags: {#StdFlags} ; 
Source: "{#VOXPorterFolder}VOXporter.ini";                DestDir: "{app}\VOXPorter"; Flags: {#StdFlags} ; 
Source: "{#VOXPorterFolder}ReadMe.rtf";                   DestDir: "{app}\VOXPorter"; Flags: {#StdFlags} ; 
; Templates
Source: "{#VOXPorterFolder}Templates\*";                   DestDir: "{app}\VOXPorter\Templates"; Flags: {#StdFlags} ; 

; pdb files needed ?
;Source: "{#VOXPorterBinFolder}VOXporter.pdb";             DestDir: "{app}\VOXPorter"; Flags: {#StdFlags} ; 
;Source: "{#VOXPorterBinFolder}Fab_VO_Entities.pdb";       DestDir: "{app}\VOXPorter"; Flags: {#StdFlags} ; 
;Source: "{#VOXPorterBinFolder}XICOMMON.pdb";              DestDir: "{app}\VOXPorter"; Flags: {#StdFlags} ; 
;Source: "{#VOXPorterBinFolder}XIRES.pdb";                 DestDir: "{app}\VOXPorter"; Flags: {#StdFlags} ; 
;Source: "{#VOXPorterBinFolder}SDK_DEFINES.pdb";           DestDir: "{app}\VOXPorter"; Flags: {#StdFlags} ; 


; Support files
Source: "Baggage\Readme{# Suffix}.rtf";                          DestDir: "{app}";   DestName:"Readme.rtf" ; Flags: isreadme {#StdFlags}; 
Source: "Baggage\Whatsnew.rtf";                           DestDir: "{app}";   DestName:"Whatsnew.rtf" ; Flags: isreadme {#StdFlags}; 

Source: "Baggage\Redist.txt";                             DestDir: "{app}\Redist" ; Flags: {#StdFlags}; 
Source: "Baggage\XSharp.ico";                             DestDir: "{app}\Images"; Flags: touch {#StdFlags}; 
Source: "Baggage\XSharpProject.ico";                      DestDir: "{app}\Images"; Flags: touch {#StdFlags}; 
Source: "Baggage\License.txt";                            DestDir: "{app}";        Flags: touch {#StdFlags}; 

; Include Files
Source: "{#CommonFolder}*.xh";                            DestDir: "{app}\Include"; Flags: touch {#StdFlags}; 

;MsBuild Files
Source: "{#BinPFolder}Xaml\*.*";                          DestDir: "{app}\MsBuild\Rules";  Flags: {#StdFlags}; 
Source: "{#BinPFolder}Targets\*.*";                       DestDir: "{app}\MsBuild";        Flags: {#StdFlags}; 
Source: "{#BinPFolder}XSharp.Build.dll";                  DestDir: "{app}\MsBuild";        Flags: {#StdFlags}; 
Source: "{#BinPFolder}XSharp.Build.pdb";                  DestDir: "{app}\MsBuild";        Flags: {#StdFlags}; 

;Documentation
Source: "{#DocFolder}XSharp.pdf";                        DestDir: "{app}\Help";        Flags: touch {#StdFlags}; 
Source: "{#DocFolder}XSharp.chm";                        DestDir: "{app}\Help";        Flags: touch {#StdFlags}; 
Source: "{#DocFolder}XSVulcan.chm";                      DestDir: "{app}\Help";        Flags: touch {#StdFlags}; 
Source: "{#DocFolder}XSharp.msha";                       DestDir: "{app}\Help";        Flags: touch {#StdFlags}; 
Source: "{#DocFolder}XSharp.cab";                        DestDir: "{app}\Help";        Flags: touch {#StdFlags} signonce; 
Source: "{#DocFolder}XSVulcan.cab";                      DestDir: "{app}\Help";        Flags: touch {#StdFlags} signonce; 
Source: "{#BinRtHelpFolder}XSRuntime.chm";               DestDir: "{app}\Help";        Flags: touch {#StdFlags}; 
Source: "{#BinRtHelpFolder}XSRuntime.cab";               DestDir: "{app}\Help";        Flags: touch {#StdFlags} signonce; 

;XIDE
Components: Xide; Source: "{#XIDEFolder}{#XIDESetup}";   DestDir: "{app}\Xide";        Flags: touch {#StdFlags}; 

; Runtime
Source: "{#BinRtFolder}XSharp.Core.dll";                    DestDir: "{app}\Redist"; Flags: {#StdFlags} signonce {#GACInstall};  StrongAssemblyName: "XSharp.Core{#XSharpVersion}" 
Source: "{#BinRtFolder}XSharp.Core.pdb";                    DestDir: "{app}\Redist"; Flags: {#StdFlags} ;
Source: "{#BinRtFolder}XSharp.VO.dll";                      DestDir: "{app}\Redist"; Flags: {#StdFlags} signonce {#GACInstall};  StrongAssemblyName: "XSharp.VO{#XSharpVersion}" 
Source: "{#BinRtFolder}XSharp.VO.pdb";                      DestDir: "{app}\Redist"; Flags: {#StdFlags} ;
; Macro compiler
Source: "{#BinFolder}XSharp.MacroCompiler.dll";             DestDir: "{app}\Redist"; Flags: {#StdFlags} signonce {#GACInstall};  StrongAssemblyName: "XSharp.MacroCompiler{#XSharpVersion}" 
Source: "{#BinFolder}XSharp.MacroCompiler.pdb";             DestDir: "{app}\Redist"; Flags: {#StdFlags} ;
Source: "{#BinFolder}XSharp.CodeAnalysis.dll";              DestDir: "{app}\Redist"; Flags: {#StdFlags} signonce {#GACInstall};  StrongAssemblyName: "XSharp.CodeAnalysis{#XSharpVersion}" 
Source: "{#BinFolder}XSharp.CodeAnalysis.pdb";              DestDir: "{app}\Redist"; Flags: {#StdFlags} ; 
Source: "{#BinFolder}XSharp.Scripting.dll";                 DestDir: "{app}\Redist"; Flags: {#StdFlags} signonce {#GACInstall};  StrongAssemblyName: "XSharp.Scripting{#XSharpVersion}" 
Source: "{#BinFolder}XSharp.Scripting.pdb";                 DestDir: "{app}\Redist"; Flags: {#StdFlags} ; 
Source: "{#BinFolder}System.Collections.Immutable.dll";     DestDir: "{app}\Redist"; Flags: {#StdFlags} ; 
Source: "{#BinFolder}System.Reflection.Metadata.dll";       DestDir: "{app}\Redist"; Flags: {#StdFlags} ; 
Source: "{#BinFolder}System.Valuetuple.dll";                DestDir: "{app}\Redist"; Flags: {#StdFlags} ;
;Debug versions
#ifdef FOX
Source: "{#BinRtDFolder}XSharp.Core.dll";                    DestDir: "{app}\Debug"; Flags: {#StdFlags} signonce;
Source: "{#BinRtDFolder}XSharp.Core.pdb";                    DestDir: "{app}\Debug"; Flags: {#StdFlags} ;
Source: "{#BinRtDFolder}XSharp.VO.dll";                      DestDir: "{app}\Debug"; Flags: {#StdFlags} signonce;
Source: "{#BinRtDFolder}XSharp.VO.pdb";                      DestDir: "{app}\Debug"; Flags: {#StdFlags} ;
Source: "{#BinDFolder}XSharp.MacroCompiler.dll";             DestDir: "{app}\Debug"; Flags: {#StdFlags} signonce 
Source: "{#BinDFolder}XSharp.MacroCompiler.pdb";             DestDir: "{app}\Debug"; Flags: {#StdFlags} ;
Source: "{#BinDFolder}XSharp.CodeAnalysis.dll";              DestDir: "{app}\Debug"; Flags: {#StdFlags} signonce 
Source: "{#BinDFolder}XSharp.CodeAnalysis.pdb";              DestDir: "{app}\Debug"; Flags: {#StdFlags} ; 
Source: "{#BinDFolder}XSharp.Scripting.dll";                 DestDir: "{app}\Debug"; Flags: {#StdFlags} signonce 
Source: "{#BinDFolder}XSharp.Scripting.pdb";                 DestDir: "{app}\Debug"; Flags: {#StdFlags} ; 
#endif
; Assemblies for Add References Dialog
Source: "{#BinRtFolder}XSharp.Core.dll";                    DestDir: "{app}\Assemblies"; Flags: signonce {#StdFlags} 
Source: "{#BinRtFolder}XSharp.VO.dll";                      DestDir: "{app}\Assemblies"; Flags: signonce {#StdFlags} 

Source: "{#BinPFolder}SetupCheck2017.exe";          DestDir: "{tmp}";      Flags: signonce {#StdFlags};


Components: vs2015; Source: "{#BinPFolder}XSharpProject.vsix";        DestDir: "{app}\ProjectSystem"; Flags: {#StdFlags}
Components: vs2015; Source: "{#BinPFolder}XSharpDebugger.vsix";       DestDir: "{app}\ProjectSystem"; Flags: {#StdFlags}

; Deploy and Cleanup VS folders
; this images are the triggers to delete old files in the various VS Installations
Components: vs2015;  Source: "{#BinPFolder}XSharpVSIXLogo.png ";               DestDir: "{code:GetVs2015IdeDir}\Extensions\XSharp\Project";  Flags: {#StdFlags}; BeforeInstall: DeleteOldFiles(2015);
Components: vs2017;  Source: "{#BinPFolder}XSharpVSIXLogo.png ";               DestDir: "{code:GetVs2017IdeDir|1}\Extensions\XSharp\Project"; Flags: {#StdFlags}; Check: HasVs2017('1'); BeforeInstall: DeleteOldFiles(20171);
Components: vs2017;  Source: "{#BinPFolder}XSharpVSIXLogo.png ";               DestDir: "{code:GetVs2017IdeDir|2}\Extensions\XSharp\Project"; Flags: {#StdFlags}; Check: HasVs2017('2'); BeforeInstall: DeleteOldFiles(20172);
Components: vs2017;  Source: "{#BinPFolder}XSharpVSIXLogo.png ";               DestDir: "{code:GetVs2017IdeDir|3}\Extensions\XSharp\Project"; Flags: {#StdFlags}; Check: HasVs2017('3'); BeforeInstall: DeleteOldFiles(20173);


Components: vs2015 or vs2017; Source: "{#BinFolder}XSharp.CodeAnalysis.dll";           DestDir: "{app}\Extension\Project"; Flags: {#StdFlags}; 
Components: vs2015 or vs2017; Source: "{#BinFolder}XSharp.CodeAnalysis.pdb";           DestDir: "{app}\Extension\Project"; Flags: {#StdFlags}; 

; Codedom provider must go to the gac because of WPF
; always install this, also on a build server

Source: "{#BinPFolder}XSharpCodeDomProvider.dll";         DestDir: "{app}\Extension\Project"; StrongAssemblyName: "XSharpCodeDomProvider{#XSharpVersion}" ; Flags: {#StdFlags} {#GACInstall};
Source: "{#BinPFolder}XSharpCodeDomProvider.pdb";         DestDir: "{app}\Extension\Project"; Flags: {#StdFlags}; 


Components: vs2015 or vs2017; Source: "{#BinPFolder}XSharpColorizer.dll";               DestDir: "{app}\Extension\Project"; Flags: {#StdFlags}; 
Components: vs2015 or vs2017; Source: "{#BinPFolder}XSharpColorizer.pdb";               DestDir: "{app}\Extension\Project"; Flags: {#StdFlags}; 
Components: vs2015 or vs2017; Source: "{#BinPFolder}XSharpModel.dll";                   DestDir: "{app}\Extension\Project"; Flags: {#StdFlags}; 
Components: vs2015 or vs2017; Source: "{#BinPFolder}XSharpModel.pdb";                   DestDir: "{app}\Extension\Project"; Flags: {#StdFlags}; 
Components: vs2015 or vs2017; Source: "{#BinFolder}Microsoft.DiaSymReader.*.Dll";      DestDir: "{app}\Extension\Project"; Flags: {#StdFlags} ; 
Components: vs2015 or vs2017; Source: "{#BinFolder}System.Security.Cryp*Alg*.dll";     DestDir: "{app}\Extension\Project"; Flags: {#StdFlags} ;
Components: vs2015 or vs2017; Source: "{#BinFolder}System.Text.Encoding.*.dll";        DestDir: "{app}\Extension\Project"; Flags: {#StdFlags} ;
Components: vs2015 or vs2017; Source: "{#BinFolder}System.Valuetuple.dll";             DestDir: "{app}\Extension\Project"; Flags: {#StdFlags} ;

; VOEditors
Components: vs2015 or vs2017; Source: "{#BinPFolder}XSharp.CodeGenerator.dll";          DestDir: "{app}\Extension\Project"; Flags: {#StdFlags}; 
Components: vs2015 or vs2017; Source: "{#BinPFolder}XSharpVoEditors.dll";               DestDir: "{app}\Extension\Project"; Flags: {#StdFlags}; 
Components: vs2015 or vs2017; Source: "{#BinPFolder}XSharp.VODesigners.dll";            DestDir: "{app}\Extension\Project"; Flags: {#StdFlags};

; ItemTemplates per folder
Components: vs2015 or vs2017; Source: "{#BinPFolder}Itemtemplates\Wpf*.Zip";            DestDir: "{app}\Extension\Project\ItemTemplates\WPF";        Flags: recursesubdirs {#StdFlags}; 
Components: vs2015 or vs2017; Source: "{#BinPFolder}Itemtemplates\VO*.Zip";             DestDir: "{app}\Extension\Project\ItemTemplates\VO";         Flags: recursesubdirs {#StdFlags}; 
Components: vs2015 or vs2017; Source: "{#BinPFolder}Itemtemplates\*Internal.Zip";       DestDir: "{app}\Extension\Project\ItemTemplates\Internal";   Flags: recursesubdirs {#StdFlags}; 
Components: vs2015 or vs2017; Source: "{#BinPFolder}Itemtemplates\Wcf*.Zip";            DestDir: "{app}\Extension\Project\ItemTemplates\WCF";        Flags: recursesubdirs {#StdFlags}; 
Components: vs2015 or vs2017; Source: "{#BinPFolder}Itemtemplates\Form*.Zip";           DestDir: "{app}\Extension\Project\ItemTemplates\Windows Forms"; Flags: recursesubdirs {#StdFlags}; 
Components: vs2015 or vs2017; Source: "{#BinPFolder}Itemtemplates\U*.Zip";              DestDir: "{app}\Extension\Project\ItemTemplates\Windows Forms"; Flags: recursesubdirs {#StdFlags}; 
Components: vs2015 or vs2017; Source: "{#BinPFolder}Itemtemplates\C*.Zip";              DestDir: "{app}\Extension\Project\ItemTemplates\Code";        Flags: recursesubdirs {#StdFlags}; 
Components: vs2015 or vs2017; Source: "{#BinPFolder}Itemtemplates\H*.Zip";              DestDir: "{app}\Extension\Project\ItemTemplates\Code";        Flags: recursesubdirs {#StdFlags}; 
Components: vs2015 or vs2017; Source: "{#BinPFolder}Itemtemplates\T*.Zip";              DestDir: "{app}\Extension\Project\ItemTemplates\Code";        Flags: recursesubdirs {#StdFlags}; 
Components: vs2015 or vs2017; Source: "{#BinPFolder}Itemtemplates\R*.Zip";              DestDir: "{app}\Extension\Project\ItemTemplates\Resources";   Flags: recursesubdirs {#StdFlags}; 

Components: vs2015 or vs2017; Source: "{#BinPFolder}ProjectTemplates\*test*.zip";       DestDir: "{app}\Extension\Project\ProjectTemplates\Test";     Flags: recursesubdirs {#StdFlags}; 
Components: vs2015 or vs2017; Source: "{#BinPFolder}ProjectTemplates\*unit*.zip";       DestDir: "{app}\Extension\Project\ProjectTemplates\Test";     Flags: recursesubdirs {#StdFlags}; 
Components: vs2015 or vs2017; Source: "{#BinPFolder}ProjectTemplates\C*.zip";           DestDir: "{app}\Extension\Project\ProjectTemplates\Windows";  Flags: recursesubdirs {#StdFlags}; 
Components: vs2015 or vs2017; Source: "{#BinPFolder}ProjectTemplates\*Cons*.zip";       DestDir: "{app}\Extension\Project\ProjectTemplates\Windows";  Flags: recursesubdirs {#StdFlags}; 
Components: vs2015 or vs2017; Source: "{#BinPFolder}ProjectTemplates\*App*.zip";        DestDir: "{app}\Extension\Project\ProjectTemplates\Windows";  Flags: recursesubdirs {#StdFlags}; 
; Snippets
Components: vs2015 or vs2017; Source: "{#SnippetsSource}*.*";                          DestDir: "{app}\Extension\{# SnippetsPath}";          Flags: recursesubdirs {#StdFlags}; 


Components: vs2015 or vs2017; Source: "{#BinPFolder}XSharpProject.dll";                 DestDir: "{app}\Extension\Project"; Flags: {#StdFlags}; 
Components: vs2015 or vs2017; Source: "{#BinPFolder}XSharpProject.dll.config";          DestDir: "{app}\Extension\Project"; Flags: {#StdFlags}; 
Components: vs2015 or vs2017; Source: "{#BinPFolder}XSharpProject.pdb";                 DestDir: "{app}\Extension\Project"; Flags: {#StdFlags}; 
Components: vs2015 or vs2017; Source: "{#BinPFolder}XSharpProject.pkgdef";              DestDir: "{app}\Extension\Project"; Flags: {#StdFlags}; 
Components: vs2015 or vs2017; Source: "{#ProjectObjFolder}extension.vsixmanifest";      DestDir: "{app}\Extension\Project"; Flags: {#StdFlags}; 
Components: vs2015 or vs2017; Source: "{#BinPFolder}Designers.pkgdef";                  DestDir: "{app}\Extension\Project"; Flags: {#StdFlags}; 

Components: vs2015 or vs2017; Source: "Baggage\XSharp.ico";                             DestDir: "{app}\Extension\Project";  Flags: {#StdFlags}; 
Components: vs2015 or vs2017; Source: "Baggage\License.txt";                            DestDir: "{app}\Extension\Project";  Flags: {#StdFlags}; 
Components: vs2015 or vs2017; Source: "{#BinPFolder}XSharpVSIXLogo.png ";               DestDir: "{app}\Extension\Project";  Flags: {#StdFlags}; 

Components: vs2015 or vs2017; Source: "{#BinPFolder}XSharpDebugger.dll";                DestDir: "{app}\Extension\Debugger"; Flags: {#StdFlags}; 
Components: vs2015 or vs2017; Source: "{#BinPFolder}XSharpDebugger.pdb";                DestDir: "{app}\Extension\Debugger"; Flags: {#StdFlags}; 
Components: vs2015 or vs2017; Source: "{#BinPFolder}XSharpDebugger.pkgdef";             DestDir: "{app}\Extension\Debugger"; Flags: {#StdFlags}; 
Components: vs2015 or vs2017; Source: "{#BinPFolder}XSharpDebugger.vsdconfig";          DestDir: "{app}\Extension\Debugger"; Flags: {#StdFlags}; 
Components: vs2015 or vs2017; Source: "Baggage\XSharp.ico";                             DestDir: "{app}\Extension\Debugger"; Flags: {#StdFlags}; 
Components: vs2015 or vs2017; Source: "Baggage\License.txt";                            DestDir: "{app}\Extension\Debugger"; Flags: {#StdFlags}; 
Components: vs2015 or vs2017; Source: "{#BinPFolder}XSharpVSIXLogo.png ";               DestDir: "{app}\Extension\Debugger"; Flags: {#StdFlags}; 
Components: vs2015 or vs2017; Source: "{#DebuggerObjFolder}extension.vsixmanifest";     DestDir: "{app}\Extension\Debugger"; Flags: {#StdFlags}; 


; Examples
Source: "{#ExamplesFolder}*.prg";              DestDir: "{commondocs}\XSharp\Examples";    Flags: recursesubdirs {#StdFlags};
Source: "{#ExamplesFolder}SDK_Defines.dll";    DestDir: "{commondocs}\XSharp\Examples";    Flags: recursesubdirs {#StdFlags};
Source: "{#ExamplesFolder}*.rc";               DestDir: "{commondocs}\XSharp\Examples";    Flags: recursesubdirs {#StdFlags};
Source: "{#ExamplesFolder}*.resx";             DestDir: "{commondocs}\XSharp\Examples";    Flags: recursesubdirs {#StdFlags};
Source: "{#ExamplesFolder}*.txt";              DestDir: "{commondocs}\XSharp\Examples";    Flags: recursesubdirs {#StdFlags};
Source: "{#ExamplesFolder}*.?h";               DestDir: "{commondocs}\XSharp\Examples";    Flags: recursesubdirs {#StdFlags};
Source: "{#ExamplesFolder}*.sln";              DestDir: "{commondocs}\XSharp\Examples";    Flags: recursesubdirs {#StdFlags};
Source: "{#ExamplesFolder}*.dbf";              DestDir: "{commondocs}\XSharp\Examples";    Flags: recursesubdirs {#StdFlags};
Source: "{#ExamplesFolder}*.ntx";              DestDir: "{commondocs}\XSharp\Examples";    Flags: recursesubdirs {#StdFlags};
Source: "{#ExamplesFolder}*.xs*";              DestDir: "{commondocs}\XSharp\Examples";    Flags: recursesubdirs {#StdFlags};
Source: "{#ExamplesFolder}*.vi*";              DestDir: "{commondocs}\XSharp\Examples";    Flags: recursesubdirs {#StdFlags};
Source: "{#ExamplesFolder}*.ico";              DestDir: "{commondocs}\XSharp\Examples";    Flags: recursesubdirs {#StdFlags} skipifsourcedoesntexist;
Source: "{#ExamplesFolder}*.bmp";              DestDir: "{commondocs}\XSharp\Examples";    Flags: recursesubdirs {#StdFlags} skipifsourcedoesntexist;
Source: "{#ExamplesFolder}*.cur";              DestDir: "{commondocs}\XSharp\Examples";    Flags: recursesubdirs {#StdFlags} skipifsourcedoesntexist;
Source: "{#ExamplesFolder}ca?o*.dll";          DestDir: "{commondocs}\XSharp\Examples";    Flags: recursesubdirs {#StdFlags} skipifsourcedoesntexist;
Source: "{#ExamplesFolder}msvc*.dll";          DestDir: "{commondocs}\XSharp\Examples";    Flags: recursesubdirs {#StdFlags} skipifsourcedoesntexist;

; Scripting
Source: "{#ScriptFolder}*.*";                  DestDir: "{commondocs}\XSharp\Scripting";    Flags: recursesubdirs {#StdFlags};

; update machine.config
Source:"{#BinPFolder}RegisterProvider.exe";    DestDir: "{app}\Tools";                     Flags: signonce {#StdFlags};

[Icons]
Name: "{group}\{cm:ProgramOnTheWeb,{#Product}}";          Filename: "{#XSharpURL}";    IconFilename:{app}\Images\XSharp.ico;
Name: "{group}\{cm:UninstallProgram,{#Product}}";         Filename: "{uninstallexe}";  WorkingDir: "{app}\uninst" ; Parameters: "/LOG";
Name: "{group}\{#Product} XPorter";                       Filename: "{app}\bin\xporter.exe";
Name: "{group}\{#Product} VOXPorter";                     Filename: "{app}\VoXPorter\VoXPorter.exe";
Name: "{group}\{#Product} Readme";                        Filename: "{app}\Readme.rtf";
Name: "{group}\{#Product} What's New";                    Filename: "{app}\Whatsnew.rtf";
Name: "{group}\{#Product} Documentation (CHM)";           Filename: "{app}\Help\XSharp.chm"; 
Name: "{group}\{#Product} Documentation (PDF)";           Filename: "{app}\Help\XSharp.pdf"; 
Name: "{group}\{#Product} Vulcan Runtime Reference (CHM)"; Filename: "{app}\Help\XSVulcan.chm"; 
Name: "{group}\{#Product} Runtime Reference (CHM)";       Filename: "{app}\Help\XSRuntime.chm"; 
Name: "{group}\{cm:UninstallProgram,{#Product}}";         Filename: "{uninstallexe}"; 
Name: "{group}\{#Product} Examples";                      Filename: "{commondocs}\XSharp\Examples";
Name: "{commondesktop}\XSharp Examples";                  Filename: "{commondocs}\XSharp\Examples";
Name: "{commondesktop}\XSharp Script Examples";           Filename: "{commondocs}\XSharp\Scripting";
Name: "{userdocs}\XSharp Examples";                       Filename: "{commondocs}\XSharp\Examples";
Name: "{userdocs}\XSharp Script Examples";                Filename: "{commondocs}\XSharp\Scripting";


[Registry]
Root: HKLM; Subkey: "Software\{#RegCompany}"; Flags: uninsdeletekeyifempty 
Root: HKLM; Subkey: "Software\{#RegCompany}\{#Product}"; Flags: uninsdeletekey 
Root: HKLM; Subkey: "Software\{#RegCompany}\{#Product}"; ValueName: "{#InstallPath}"; ValueType: string; ValueData: "{app}" ;
Components: vs2015\help; Root: HKLM; Subkey: "Software\{#RegCompany}\{#Product}"; ValueName: "Help22Installed"; ValueType: string; ValueData: "yes" ;  
Components: vs2017\help; Root: HKLM; Subkey: "Software\{#RegCompany}\{#Product}"; ValueName: "Help23Installed"; ValueType: string; ValueData: "yes" ;  

; set the VSHelp to Offline
Components: vs2015\help;  Root: HKCU; Subkey: "{#Vs14RegPath}\Help"; ValueName:"UseOnlineHelp"; ValueType: dword; ValueData: 0; Flags: noerror;
; Cannot set VS 2017 Online help off. It is in a private registry

; When Vulcan is Installed then update its extension registration so we can handle the priorities in our project system

Components: vs2015; Root: HKLM; Subkey: "{#Vs14RegPath}\{#VulcanEditorGuid}"; ValueName: "ppo"; ValueData: 1; ValueType: dword  ;Check: VulcanPrgAssociated;
Components: vs2015; Root: HKLM; Subkey: "{#Vs14RegPath}\{#VulcanEditorGuid}"; ValueName: "prg"; ValueData: 1; ValueType: dword  ;Check: VulcanPrgAssociated;
Components: vs2015; Root: HKLM; Subkey: "{#Vs14RegPath}\{#VulcanEditorGuid}"; ValueName: "vh";  ValueData: 1; ValueType: dword  ;Check: VulcanPrgAssociated;
Components: vs2015; Root: HKLM; Subkey: "{#Vs14RegPath}\{#VulcanWedGuid}"; ValueName: "vnfrm";  ValueData: 1; ValueType: dword  ;Check: VulcanPrgAssociated;
Components: vs2015; Root: HKLM; Subkey: "{#Vs14RegPath}\{#VulcanMedGuid}"; ValueName: "vnmnu";  ValueData: 1; ValueType: dword  ;Check: VulcanPrgAssociated;
Components: vs2015; Root: HKLM; Subkey: "{#Vs14RegPath}\{#VulcanDedGuid}"; ValueName: "vndbs";  ValueData: 1; ValueType: dword  ;Check: VulcanPrgAssociated;
Components: vs2015; Root: HKLM; Subkey: "{#Vs14RegPath}\{#VulcanFedGuid}"; ValueName: "vnfs";  ValueData: 1; ValueType: dword  ;Check: VulcanPrgAssociated;

Components: vs2015; Root: HKCU; Subkey: "{#Vs14RegPath}_Config\{#VulcanEditorGuid}"; ValueName: "ppo"; ValueData: 1; ValueType: dword  ;Check: VulcanPrgAssociated;
Components: vs2015; Root: HKCU; Subkey: "{#Vs14RegPath}_Config\{#VulcanEditorGuid}"; ValueName: "prg"; ValueData: 1; ValueType: dword  ;Check: VulcanPrgAssociated;
Components: vs2015; Root: HKCU; Subkey: "{#Vs14RegPath}_Config\{#VulcanEditorGuid}"; ValueName: "vh";  ValueData: 1; ValueType: dword  ;Check: VulcanPrgAssociated;
Components: vs2015; Root: HKCU; Subkey: "{#Vs14RegPath}_Config\{#VulcanWedGuid}"; ValueName: "vnfrm";  ValueData: 1; ValueType: dword  ;Check: VulcanPrgAssociated;
Components: vs2015; Root: HKCU; Subkey: "{#Vs14RegPath}_Config\{#VulcanMedGuid}"; ValueName: "vnmnu";  ValueData: 1; ValueType: dword  ;Check: VulcanPrgAssociated;
Components: vs2015; Root: HKCU; Subkey: "{#Vs14RegPath}_Config\{#VulcanDedGuid}"; ValueName: "vndbs";  ValueData: 1; ValueType: dword  ;Check: VulcanPrgAssociated;
Components: vs2015; Root: HKCU; Subkey: "{#Vs14RegPath}_Config\{#VulcanFedGuid}"; ValueName: "vnfs";  ValueData: 1; ValueType: dword  ;Check: VulcanPrgAssociated;


; associate prgx extension

Components: main\script; Root: HKCR; Subkey: ".prgx";                            ValueData: "{#XSScript}";                Flags: uninsdeletekey;   ValueType: string;  ValueName: ""
Components: main\script; Root: HKCR; Subkey: "{#XSScript}";                      ValueData: "Program {#XSScript}";        Flags: uninsdeletekey;   ValueType: string;  ValueName: ""
Components: main\script; Root: HKCR; Subkey: "{#XSScript}\DefaultIcon";          ValueData: "{app}\Images\xsharp.ico,0";  ValueType: string;  ValueName: ""
Components: main\script; Root: HKCR; Subkey: "{#XSScript}\shell\open\command";   ValueData: """{app}\bin\xsi.exe"" ""%1"" %*";  ValueType: string;  ValueName: ""

; Location for the files that appear in the Add Reference dialog inside Visual Studio
Root: HKLM; Subkey: "Software\Microsoft\.NETFramework\v4.0.30319\AssemblyFoldersEx\XSharp"; ValueData: "{app}\Assemblies"; ValueType: string; 

; todo - see registration from Vulcan
; associate .xsproj extension
; associate .xsprj extension
; .prg 
; .xs
; .ppo
; .vh
; .xh

;project
Components: vs2015 or vs2017; Root: HKCR; SubKey: ".xsproj";                              ValueData: "XSharp.xsprojfile";         ValueType: string;  ValueName: "";  Flags: uninsdeletekey; 
Components: vs2015 or vs2017; Root: HKCR; SubKey: ".xsproj";                              ValueData: "text/plain";                ValueType: string;  ValueName: "Content Type";
Components: vs2015 or vs2017; Root: HKCR; SubKey: ".xsprj";                               ValueData: "XSharp.xsprojfile";         ValueType: string;  ValueName: "";  Flags: uninsdeletekey; 
Components: vs2015 or vs2017; Root: HKCR; SubKey: ".xsprj";                               ValueData: "text/plain";                ValueType: string;  ValueName: "Content Type";
Components: vs2015 or vs2017; Root: HKCR; Subkey: "XSharp.xsprojfile";                    ValueData: "XSharp Project File";       ValueType: string;  ValueName: "";   Flags: uninsdeletekey; 
Components: vs2015 or vs2017; Root: HKCR; Subkey: "XSharp.xsprojfile\DefaultIcon";        ValueData: "{app}\Images\XSharpProject.ico,0"; ValueType: string;  ValueName: ""
Components: vs2015;           Root: HKCR; Subkey: "XSharp.xsprojfile\shell\Open";         ValueData: "&Open in Visual Studio 2015";  ValueType: string;  ValueName: ""
Components: vs2017;           Root: HKCR; Subkey: "XSharp.xsprojfile\shell\Open";         ValueData: "&Open in Visual Studio 2017";  ValueType: string;  ValueName: ""
Components: vs2015;           Root: HKCR; Subkey: "XSharp.xsprojfile\shell\open\command"; ValueData: """{code:GetVs2015IdeDir}\devenv.exe""  ""%1""";  ValueType: string;  ValueName: ""
; associate with 1st version of Vs2017
Components: vs2017;           Root: HKCR; Subkey: "XSharp.xsprojfile\shell\open\command"; ValueData: """{code:GetVs2017IdeDir|1}\devenv.exe""  ""%1""";  ValueType: string;  ValueName: ""

; source
Components: vs2015 or vs2017; Root: HKCR; SubKey: ".prg";                                 ValueData: "XSharp.sourcefile";             ValueType: string;  ValueName: "";  Flags: uninsdeletekey; 
Components: vs2015 or vs2017; Root: HKCR; SubKey: ".prg";                                 ValueData: "text/plain";                    ValueType: string;  ValueName: "Content Type";
Components: vs2015 or vs2017; Root: HKCR; SubKey: ".xs";                                  ValueData: "XSharp.sourcefile";             ValueType: string;  ValueName: "";  Flags: uninsdeletekey; 
Components: vs2015 or vs2017; Root: HKCR; SubKey: ".xs";                                  ValueData: "text/plain";                    ValueType: string;  ValueName: "Content Type";
Components: vs2015 or vs2017; Root: HKCR; Subkey: "XSharp.sourcefile";                    ValueData: "XSharp Source File";            ValueType: string;  ValueName: "" ;  Flags: uninsdeletekey; 
Components: vs2015 or vs2017; Root: HKCR; Subkey: "XSharp.sourcefile\DefaultIcon";           ValueData: "{app}\Images\xsharp.ico,0";     ValueType: string;  ValueName: ""
Components: vs2015;           Root: HKCR; Subkey: "XSharp.sourcefile\shell\Open";         ValueData: "&Open in Visual Studio 2015";   ValueType: string;  ValueName: ""
Components: vs2017;           Root: HKCR; Subkey: "XSharp.sourcefile\shell\Open";         ValueData: "&Open in Visual Studio 2017";   ValueType: string;  ValueName: ""
Components: vs2015;           Root: HKCR; Subkey: "XSharp.sourcefile\shell\open\command"; ValueData: """{code:GetVs2015IdeDir}\devenv.exe""  ""%1""";  ValueType: string;  ValueName: ""
Components: vs2017;           Root: HKCR; Subkey: "XSharp.sourcefile\shell\open\command"; ValueData: """{code:GetVs2017IdeDir|1}\devenv.exe""  ""%1""";  ValueType: string;  ValueName: ""

;ppo
Components: vs2015 or vs2017; Root: HKCR; SubKey: ".ppo";                              ValueData: "XSharp.ppofile";            ValueType: string;  ValueName: "";  Flags: uninsdeletekey; 
Components: vs2015 or vs2017; Root: HKCR; SubKey: ".ppo";                              ValueData: "text/plain";                ValueType: string;  ValueName: "Content Type";
Components: vs2015 or vs2017; Root: HKCR; Subkey: "XSharp.ppofile";                    ValueData: "XSharp Preprocessor Output File";        ValueType: string;  ValueName: "" ;  Flags: uninsdeletekey; 
Components: vs2015 or vs2017; Root: HKCR; Subkey: "XSharp.ppofile\DefaultIcon";        ValueData: "{app}\Images\xsharp.ico,0"; ValueType: string;  ValueName: ""
Components: vs2015;           Root: HKCR; Subkey: "XSharp.ppofile\shell\Open";         ValueData: "&Open in Visual Studio 2015";  ValueType: string;  ValueName: ""
Components: vs2017;           Root: HKCR; Subkey: "XSharp.ppofile\shell\Open";         ValueData: "&Open in Visual Studio 2017";  ValueType: string;  ValueName: ""
Components: vs2015;           Root: HKCR; Subkey: "XSharp.ppofile\shell\open\command"; ValueData: """{code:GetVs2015IdeDir}\devenv.exe""  ""%1""";  ValueType: string;  ValueName: ""
Components: vs2017;           Root: HKCR; Subkey: "XSharp.ppofile\shell\open\command"; ValueData: """{code:GetVs2017IdeDir|1}\devenv.exe""  ""%1""";  ValueType: string;  ValueName: ""

; headers
Components: vs2015 or vs2017; Root: HKCR; SubKey: ".vh";                                  ValueData: "XSharp.headerfile";         ValueType: string;  ValueName: "";  Flags: uninsdeletekey; 
Components: vs2015 or vs2017; Root: HKCR; SubKey: ".vh";                                  ValueData: "text/plain";                ValueType: string;  ValueName: "Content Type";
Components: vs2015 or vs2017; Root: HKCR; SubKey: ".xh";                                  ValueData: "XSharp.headerfile";         ValueType: string;  ValueName: "";  Flags: uninsdeletekey; 
Components: vs2015 or vs2017; Root: HKCR; SubKey: ".xh";                                  ValueData: "text/plain";                ValueType: string;  ValueName: "Content Type";
Components: vs2015 or vs2017; Root: HKCR; Subkey: "XSharp.headerfile";                    ValueData: "XSharp Header File";        ValueType: string;  ValueName: "" ;  Flags: uninsdeletekey; 
Components: vs2015 or vs2017; Root: HKCR; Subkey: "XSharp.headerfile\DefaultIcon";        ValueData: "{app}\Images\xsharp.ico,0"; ValueType: string;  ValueName: ""
Components: vs2015;           Root: HKCR; Subkey: "XSharp.headerfile\shell\Open";         ValueData: "&Open in Visual Studio 2015";  ValueType: string;  ValueName: ""
Components: vs2017;           Root: HKCR; Subkey: "XSharp.headerfile\shell\Open";         ValueData: "&Open in Visual Studio 2017";  ValueType: string;  ValueName: ""
Components: vs2015;           Root: HKCR; Subkey: "XSharp.headerfile\shell\open\command"; ValueData: """{code:GetVs2015IdeDir}\devenv.exe""  ""%1""";  ValueType: string;  ValueName: ""
Components: vs2017;           Root: HKCR; Subkey: "XSharp.headerfile\shell\open\command"; ValueData: """{code:GetVs2017IdeDir|1}\devenv.exe""  ""%1""";  ValueType: string;  ValueName: ""


[Ini]
Components: vs2015; Filename: "{code:GetVs2015IdeDir}\Extensions\extensions.configurationchanged"; Section:"XSharp"; Key: "Installed"; String: "{#Version}"; Flags: uninsdeletesection; 
Components: vs2017; Filename: "{code:GetVs2017IdeDir|1}\Extensions\extensions.configurationchanged"; Section:"XSharp"; Key: "Installed"; String: "{#Version}"; Flags: uninsdeletesection; Check: HasVs2017('1')
Components: vs2017; Filename: "{code:GetVs2017IdeDir|2}\Extensions\extensions.configurationchanged"; Section:"XSharp"; Key: "Installed"; String: "{#Version}"; Flags: uninsdeletesection; Check: HasVs2017('2')
Components: vs2017; Filename: "{code:GetVs2017IdeDir|3}\Extensions\extensions.configurationchanged"; Section:"XSharp"; Key: "Installed"; String: "{#Version}"; Flags: uninsdeletesection; Check: HasVs2017('3')

Filename: "{app}\VOXPorter\VoXporter.Ini";  Section:"General"; Key: "NOWARNINGSCREEN"; String: "0" ;
Filename: "{app}\VOXPorter\VoXporter.Ini";  Section:"General"; Key: "SDKDEFINESDLL";   String: "{app}\VOXPorter\SDK_DEFINES.dll" ;


[Run]
Filename: "{app}\Tools\RegisterProvider.exe"; Flags: runhidden;

; Remove old Help contents
Components: vs2015\help; Filename: "{code:GetHelp22Dir}\HlpCtntMgr.exe"; Parameters: "{#HelpUninstall1} VisualStudio14 {#HelpUninstall2}";   StatusMsg:"UnInstalling VS Help for VS2015"; Flags: waituntilidle;
Components: vs2017\help; Filename: "{code:GetHelp23Dir}\HlpCtntMgr.exe"; Parameters: "{#HelpUninstall1} VisualStudio15 {#HelpUninstall2}";   StatusMsg:"UnInstalling VS Help for VS2017"; Flags: waituntilidle;

Components: vs2015\help; Filename: "{code:GetHelp22Dir}\HlpCtntMgr.exe"; Parameters: "{#HelpInstall1} VisualStudio14 {#HelpInstall2}";     StatusMsg:"Installing VS Help for VS2015"; Flags: waituntilidle;
Components: vs2017\help; Filename: "{code:GetHelp23Dir}\HlpCtntMgr.exe"; Parameters: "{#HelpInstall1} VisualStudio15 {#HelpInstall2}";     StatusMsg:"Installing VS Help for VS2017";  Flags: waituntilidle;
Components: XIDE;        Filename: "{app}\Xide\{#XIDESetup}"; Description:"Run XIDE {# XIDEVersion} Installer"; Flags: postInstall;  

Components: main\ngen;  Filename: "{app}\uninst\instngen.cmd";      Check: CreateNGenTask(); Flags: Runhidden ; StatusMsg: "Generating Native images, please wait.....";
Components: vs2015;     Filename: "{app}\uninst\deployvs2015.cmd";  Check: DeployToVs2015(); Flags: Runhidden ; StatusMsg: "Deploying extension to VS 2015, please wait.....";
Components: vs2017;     Filename: "{app}\uninst\deployvs2017.cmd";  Check: DeployToVs2017(); Flags: Runhidden ; StatusMsg: "Deploying extension to VS 2017, please wait.....";

[UninstallRun]
Components: vs2015\help; Filename: "{code:GetHelp22Dir}\HlpCtntMgr.exe"; Parameters: "{#HelpUninstall1} VisualStudio14 {#HelpUninstall2}";   StatusMsg:"UnInstalling VS Help for VS2015"; Flags: waituntilidle;
Components: vs2017\help; Filename: "{code:GetHelp23Dir}\HlpCtntMgr.exe"; Parameters: "{#HelpUninstall1} VisualStudio15 {#HelpUninstall2}";   StatusMsg:"UnInstalling VS Help for VS2017"; Flags: waituntilidle;

Components: main\ngen;  Filename: "{app}\uninst\uninstngen.cmd";  Flags: Runhidden;



[InstallDelete]
; The old License.rtf file.
; Template cache, component cache and previous installation of our project system vs2015
; Also the CodeDom provider from the GAC

Type: filesandordirs; Name: "{win}\Microsoft.NET\assembly\GAC_MSIL\XSharpCodeDomProvider";
Type: files;          Name: "{app}\License.rtf"; 
Type: filesandordirs; Name: "{app}\Xide"; 
Type: filesandordirs; Name: "{app}\VOXporter"; 
Type: filesandordirs; Name: "{app}\Bin"; 
#ifdef FOX
Type: filesandordirs; Name: "{app}\Debug"; 
#endif
Type: filesandordirs; Name: "{app}\Extension"; 
Type: filesandordirs; Name: "{app}\MsBuild"; 
Type: filesandordirs; Name: "{app}\Assemblies"; 
Type: filesandordirs; Name: "{app}\Redist";
Type: filesandordirs; Name: "{app}\Help";
 
Components: vs2015; Type: filesandordirs; Name: "{#Vs14LocalDir}\vtc";                            
Components: vs2015; Type: filesandordirs; Name: "{#Vs14LocalDir}\ComponentModelCache";            
Components: vs2015; Type: filesandordirs; Name: "{code:GetVs2015IdeDir}\Extensions\XSharp";       

; vs2017
Components: vs2017; Type: filesandordirs; Name: "{#Vs15LocalDir}{code:GetVs2017InstanceId|1}\vtc";                       Check: HasVs2017('1');
Components: vs2017; Type: filesandordirs; Name: "{#Vs15LocalDir}{code:GetVs2017InstanceId|2}\vtc";                       Check: HasVs2017('2');
Components: vs2017; Type: filesandordirs; Name: "{#Vs15LocalDir}{code:GetVs2017InstanceId|3}\vtc";                       Check: HasVs2017('3');
Components: vs2017; Type: filesandordirs; Name: "{#Vs15LocalDir}{code:GetVs2017InstanceId|1}\ComponentModelCache";       Check: HasVs2017('1');
Components: vs2017; Type: filesandordirs; Name: "{#Vs15LocalDir}{code:GetVs2017InstanceId|2}\ComponentModelCache";       Check: HasVs2017('2');
Components: vs2017; Type: filesandordirs; Name: "{#Vs15LocalDir}{code:GetVs2017InstanceId|3}\ComponentModelCache";       Check: HasVs2017('3');
Components: vs2017; Type: filesandordirs; Name: "{code:GetVs2017IdeDir|1}\Extensions\XSharp";  Check: HasVs2017('1');
Components: vs2017; Type: filesandordirs; Name: "{code:GetVs2017IdeDir|2}\Extensions\XSharp";  Check: HasVs2017('2');
Components: vs2017; Type: filesandordirs; Name: "{code:GetVs2017IdeDir|3}\Extensions\XSharp";  Check: HasVs2017('3');
Components: vs2017; Type: filesandordirs; Name: "{code:GetVs2017BaseDir|1}\MsBuild\XSharp";  Check: HasVs2017('1');
Components: vs2017; Type: filesandordirs; Name: "{code:GetVs2017BaseDir|2}\MsBuild\XSharp";  Check: HasVs2017('2');
Components: vs2017; Type: filesandordirs; Name: "{code:GetVs2017BaseDir|3}\MsBuild\XSharp";  Check: HasVs2017('3');

Type: filesandordirs; Name: "{group}" ;

[UninstallDelete]
Type: filesandordirs; Name: "{app}\Assemblies"                    ; 
Type: filesandordirs; Name: "{app}\Bin"                           ; 
#ifdef FOX
Type: filesandordirs; Name: "{app}\Debug"; 
#endif
Type: filesandordirs; Name: "{app}\Extension"                     ; 
Type: filesandordirs; Name: "{app}\Help"                          ; 
Type: filesandordirs; Name: "{app}\Images"                        ; 
Type: dirifempty;     Name: "{app}\Include"; 
Type: filesandordirs; Name: "{app}\Portable"                      ; 
Type: filesandordirs; Name: "{app}\ProjectSystem"                 ; 
Type: filesandordirs; Name: "{app}\Redist"                        ; 
Type: filesandordirs; Name: "{app}\Tools"                         ; 
Type: filesandordirs; Name: "{app}\Uninst"                        ;
Type: filesandordirs; Name: "{app}\MsBuild";                      
Type: filesandordirs; Name: "{app}\Xide"; 
Type: dirifempty;     Name: "{app}\Snippets"     ; 
Type: dirifempty;     Name: "{app}\VOXPorter"    ; 
Type: dirifempty;     Name: "{app}\VOXPorter\Templates"; 
Type: dirifempty;     Name: "{app}"; 
Type: filesandordirs; Name: "{group}";


Type: filesandordirs; Name: "{pf}\MsBuild\{#Product}"             ; 
Type: filesandordirs; Name: "{commondocs}\XSharp\Examples";
Type: filesandordirs; Name: "{commondocs}\XSharp\Scripting";
Type: dirifempty;     Name: "{commondocs}\XSharp"; 

Components: vs2015; Type: filesandordirs; Name: "{code:GetVs2015IdeDir}\Extensions\XSharp"  ;  
Components: vs2017; Type: filesandordirs; Name: "{code:GetVs2017IdeDir|1}\Extensions\XSharp" ;  Check: HasVs2017('1');
Components: vs2017; Type: filesandordirs; Name: "{code:GetVs2017IdeDir|2}\Extensions\XSharp";  Check: HasVs2017('2');
Components: vs2017; Type: filesandordirs; Name: "{code:GetVs2017IdeDir|3}\Extensions\XSharp";  Check: HasVs2017('3');
Components: vs2017; Type: filesandordirs; Name: "{code:GetVs2017BaseDir|1}\MsBuild\XSharp";  Check: HasVs2017('1');
Components: vs2017; Type: filesandordirs; Name: "{code:GetVs2017BaseDir|2}\MsBuild\XSharp";  Check: HasVs2017('2');
Components: vs2017; Type: filesandordirs; Name: "{code:GetVs2017BaseDir|3}\MsBuild\XSharp";  Check: HasVs2017('3');


; Template cache and component cache
;vs2015
Components: vs2015; Type: filesandordirs; Name: "{#Vs14LocalDir}\vtc";                            
Components: vs2015; Type: filesandordirs; Name: "{#Vs14LocalDir}\ComponentModelCache";            
;vs2017
Components: vs2017; Type: filesandordirs; Name: "{#Vs15LocalDir}{code:GetVs2017InstanceId|1}\vtc";                        Check: HasVs2017('1');
Components: vs2017; Type: filesandordirs; Name: "{#Vs15LocalDir}{code:GetVs2017InstanceId|2}\vtc";                        Check: HasVs2017('2');
Components: vs2017; Type: filesandordirs; Name: "{#Vs15LocalDir}{code:GetVs2017InstanceId|3}\vtc";                        Check: HasVs2017('3');
Components: vs2017; Type: filesandordirs; Name: "{#Vs15LocalDir}{code:GetVs2017InstanceId|1}\ComponentModelCache";        Check: HasVs2017('1');
Components: vs2017; Type: filesandordirs; Name: "{#Vs15LocalDir}{code:GetVs2017InstanceId|2}\ComponentModelCache";        Check: HasVs2017('2');
Components: vs2017; Type: filesandordirs; Name: "{#Vs15LocalDir}{code:GetVs2017InstanceId|3}\ComponentModelCache";        Check: HasVs2017('3');

[Messages]
WelcomeLabel1=Welcome to {# Product} (X#) 
WelcomeLabel2=This installer will install {#ProdBuild} on your computer.%n%nIt is recommended that you close all other applications before continuing, especially all running copies of Visual Studio.
;WizardInfoBefore=Warning
;InfoBeforeLabel=You are about to install Beta software
;InfoBeforeClickLabel=Only continue the installation if you are aware of the following:



[Code]
Program setup;
var
  
  PrintButton: TButton;
  Vs2015Path : String;
  Vs2015Installed: Boolean;
  Vs2015BaseDir: String;
  VulcanInstalled: Boolean;
  Vulcan4Installed: Boolean;
  VulcanBaseDir: String;
  CancelSetup : Boolean;
  
  vs2017Path1 : String;
  vs2017Path2 : String;
  vs2017Path3 : String;
  vs2017Installed: Boolean;

  vs2017Count: Integer;
  vs2017InstanceId1: String;
  vs2017InstanceId2: String;
  vs2017InstanceId3: String;

  vs2017BaseDir1: String;
  vs2017BaseDir2: String;
  vs2017BaseDir3: String;

  vs2017Version1: String;
  vs2017Version2: String;
  vs2017Version3: String;
  
  VulcanPrgAssociation: Boolean;
  VulcanGuid : String;
  HelpViewer22Installed : Boolean;
  HelpViewer22Dir : String;
  OurHelp22Installed: Boolean;
  HelpViewer23Installed : Boolean;
  HelpViewer23Dir : String;
  OurHelp23Installed: Boolean;
  OriginalTypeChange: TNotifyEvent;


/////////////////////////////////////////////////////////////////////
procedure PrintButtonClick(Sender: TObject);
var ResultCode :integer;
begin
ExtractTemporaryFile('license.txt');
if not ShellExec('Print', ExpandConstant('{tmp}\license.txt'),
     '', '', SW_SHOW, ewNoWait, ResultCode) then
//if not ShellExec('', ExpandConstant('{tmp}\license.txt'),
//     '', '', SW_SHOW, ewNoWait, ResultCode) then
end;
{
VS2017.txt will contain:
InstanceId: 8aa9303a (Complete)
InstallationVersion: 15.0.25914.0 (4222126348959744)
InstallationPath: C:\Program Files (x86)\Microsoft Visual Studio\2017\Professional
Product: Microsoft.VisualStudio.Product.Professional
Workloads:
    Microsoft.VisualStudio.Workload.CoreEditor
    Microsoft.VisualStudio.Workload.ManagedDesktop
}

/////////////////////////////////////////////////////////////////////
procedure EnvAddPath(Path: string);
var
    Paths: string;
begin
    { Retrieve current path (use empty string if entry not exists) }
    if not RegQueryStringValue(HKEY_LOCAL_MACHINE, {#EnvironmentKey}, 'Path', Paths)
    then Paths := '';

    { Skip if string already found in path }
    if Pos(';' + Uppercase(Path) + ';', ';' + Uppercase(Paths) + ';') > 0 then exit;

    { App string to the end of the path variable }
    Paths := Paths + ';'+ Path +';'
    StringChangeEx(Paths, ';;',';',true);
    { Overwrite (or create if missing) path environment variable }
    if RegWriteStringValue(HKEY_LOCAL_MACHINE, {#EnvironmentKey}, 'Path', Paths)
    then Log(Format('The [%s] added to PATH: [%s]', [Path, Paths]))
    else Log(Format('Error while adding the [%s] to PATH: [%s]', [Path, Paths]));
end;

/////////////////////////////////////////////////////////////////////
procedure EnvRemovePath(Path: string);
var
    Paths: string;
    P: Integer;
begin
    { Skip if registry entry not exists }
    if not RegQueryStringValue(HKEY_LOCAL_MACHINE, {#EnvironmentKey}, 'Path', Paths) then
        exit;

    { Skip if string not found in path }
    P := Pos(';' + Uppercase(Path) + ';', ';' + Uppercase(Paths) + ';');
    if P = 0 then exit;

    { Update path variable }
    Delete(Paths, P - 1, Length(Path) + 1);
    StringChangeEx(Paths, ';;',';',true);
    { Overwrite path environment variable }
    if RegWriteStringValue(HKEY_LOCAL_MACHINE, {#EnvironmentKey}, 'Path', Paths)
    then Log(Format('The [%s] removed from PATH: [%s]', [Path, Paths]))
    else Log(Format('Error while removing the [%s] from PATH: [%s]', [Path, Paths]));
end;

/////////////////////////////////////////////////////////////////////
procedure TaskKill(FileName: String);
var
  ResultCode: Integer;
begin
    Log('Kill running task (when necessary):'+FileName);
    Exec(ExpandConstant('taskkill.exe'), '/f /im ' + '"' + FileName + '"', '', SW_HIDE,
     ewWaitUntilTerminated, ResultCode);
end;

/////////////////////////////////////////////////////////////////////
function GetUninstallString(): String;
var
  sUnInstPath: String;
  sUnInstallString: String;
begin
  sUnInstPath := ExpandConstant('Software\Microsoft\Windows\CurrentVersion\Uninstall\{#emit SetupSetting("AppId")}_is1');
  sUnInstallString := '';
  if not RegQueryStringValue(HKLM, sUnInstPath, 'UninstallString', sUnInstallString) then
    RegQueryStringValue(HKCU, sUnInstPath, 'UninstallString', sUnInstallString);
  Result := sUnInstallString;
end;

/////////////////////////////////////////////////////////////////////
function IsUpgrade(): Boolean;
begin
  Result := (GetUninstallString() <> '');
end;


/////////////////////////////////////////////////////////////////////
Function UninstallPreviousVersion(): Integer;
var
  sUnInstallString: String;
  iResultCode: Integer;
begin
// Return Values:
// 1 - uninstall string is empty
// 2 - error executing the UnInstallString
// 3 - successfully executed the UnInstallString

  // default return value
  Result := 0;

  // get the uninstall string of the old app
  sUnInstallString := GetUninstallString();
  if sUnInstallString <> '' then begin
    sUnInstallString := RemoveQuotes(sUnInstallString);
    WizardForm.StatusLabel.Caption := 'Uninstalling previous version...';
    if Exec(sUnInstallString, '/VERYSILENT /NORESTART /SUPPRESSMSGBOXES /LOG','', SW_HIDE, ewWaitUntilTerminated, iResultCode) then
      Result := 3
    else
      Result := 2;
  end else
    Result := 1;
end;


procedure FindVS2017;
var FileContents : TArrayOfString;
  ResultCode: Integer;
  i: integer;
  j: integer;
  line: String;
  tokenLength: Integer;
  token: String;
  vs2017InstanceId: String;
  vs2017BaseDir: String;
  vs2017Version: String;
begin
  Log('Detect 2017 start');
  ExtractTemporaryFile('SetupCheck2017.exe');
  if Exec(ExpandConstant('{tmp}\SetupCheck2017.exe'),'','',SW_HIDE, ewWaitUntilTerminated, ResultCode) then
  begin
      if ResultCode = 0 then
      begin
        if LoadStringsFromFile(ExpandConstant('{tmp}\vs2017.txt'), FileContents) then
        begin
            Log('========================================');
            Log('Installed versions of Visual Studio 2017');
            Log('========================================');
            for i:= 0 to GetArrayLength(FileContents)-1 do
            begin
               line := Trim( FileContents[i]);
               Log(line);
               token := 'InstanceId:';
               if Pos(token, line) > 0  then 
               begin
                  vs2017Count := vs2017Count + 1;
                  tokenLength := Length(token);
                  vs2017InstanceId := Trim(Copy(line, tokenLength+1, Length(line) - tokenLength));
                  j := Pos('(',vs2017InstanceId)
                  if  j > 0 then
                     vs2017InstanceId := trim(Copy(vs2017InstanceId, 1, j-1));
                  if vs2017Count = 1 then
                     vs2017InstanceId1 := vs2017InstanceId;
                  if vs2017Count = 2 then
                     vs2017InstanceId2 := vs2017InstanceId;
                  if vs2017Count = 3 then
                     vs2017InstanceId3 := vs2017InstanceId;

               end;
               token := 'InstallationPath:';
               if Pos(token, line) > 0  then 
                begin
                  tokenLength := Length(token);
                  vs2017BaseDir := Trim(Copy(line, tokenLength+1, Length(line) - tokenLength));
                  if vs2017Count = 1 then
                     vs2017BaseDir1 := vs2017BaseDir;
                  if vs2017Count = 2 then
                     vs2017BaseDir2 := vs2017BaseDir;
                  if vs2017Count = 3 then
                     vs2017BaseDir3 := vs2017BaseDir;
                  vs2017Installed := true;
                end;
               token := 'Product:';
               if Pos(token, line) > 0 then
                begin
                  tokenLength := Length(token);
                  vs2017Version := ExtractFileExt(line);
                  vs2017Version := Copy(vs2017Version,2,Length(vs2017Version)-1);
                  if vs2017Count = 1 then
                     vs2017Version1 := vs2017Version;
                  if vs2017Count = 2 then
                     vs2017Version2 := vs2017Version;
                  if vs2017Count = 3 then
                     vs2017Version3 := vs2017Version;
                  vs2017Installed := true;
                end
            end
        end
      end;

      Log('Vs 2017 # of installations: '+IntToStr(vs2017Count));
      if vs2017Count > 0 then
      begin
        Log('Vs 2017 instanceId 1      : '+vs2017InstanceId1);
        Log('Vs 2017 installation dir 1: '+vs2017BaseDir1);
        Log('Vs 2017 product version 1 : '+vs2017Version1);
        vs2017path1 := vs2017BaseDir1 + '\Common7\Ide\';
      end;
      if vs2017Count > 1 then
      begin
        Log('Vs 2017 instanceId 2      : '+vs2017InstanceId2);
        Log('Vs 2017 installation dir 2: '+vs2017BaseDir2);
        Log('Vs 2017 product version 2 : '+vs2017Version2);
        vs2017path2 := vs2017BaseDir2 + '\Common7\Ide\';
      end;
      if vs2017Count > 2 then
      begin
        Log('Vs 2017 instanceId 3      : '+vs2017InstanceId3);
        Log('Vs 2017 installation dir 3: '+vs2017BaseDir3);
        Log('Vs 2017 product version  3: '+vs2017Version3);
        vs2017path3 := vs2017BaseDir3 + '\Common7\Ide\';
      end
  end;
  Log('Detect 2017 end');
end;


procedure DetectVS();
var temp : String;
begin
  Log('Start VS Detection');
  VulcanInstalled := RegQueryStringValue(HKEY_LOCAL_MACHINE,'SOFTWARE\Grafx\Vulcan.NET','InstallPath',VulcanBaseDir) ;
  Vulcan4Installed := RegQueryStringValue(HKEY_LOCAL_MACHINE,'SOFTWARE\Grafx\Vulcan.NET\4.0','InstallPath',temp) ;  
  Vs2015Installed := RegQueryStringValue(HKEY_LOCAL_MACHINE,'SOFTWARE\Microsoft\VisualStudio\SxS\VS7','14.0',Vs2015BaseDir) ;
  if Vs2015Installed then Vs2015Path := Vs2015BaseDir+'Common7\Ide\';
  FindVS2017;

  HelpViewer22Installed := RegQueryStringValue(HKEY_LOCAL_MACHINE,'SOFTWARE\Microsoft\Help\v2.2','AppRoot',HelpViewer22Dir) ;
  HelpViewer23Installed := RegQueryStringValue(HKEY_LOCAL_MACHINE,'SOFTWARE\Microsoft\Help\v2.3','AppRoot',HelpViewer23Dir) ;
  VulcanPrgAssociation := false;
  if Vs2015Installed then
  begin
  VulcanPrgAssociation := RegQueryStringValue(HKEY_LOCAL_MACHINE, '{#Vs14RegPath}\Languages\File Extensions\.prg', '', VulcanGuid) ;
  if VulcanPrgAssociation then 
      begin
      VulcanPrgAssociation := (UpperCase(VulcanGuid) = '{8D3F6D25-C81C-4FD8-9599-2F72B5D4B0C9}');
      end
  end;
  Log('End VS Detection');
  OurHelp22Installed := RegQueryStringValue(HKEY_LOCAL_MACHINE,'Software\{#RegCompany}\{#Product}','Help22Installed',temp) ;
  OurHelp23Installed := RegQueryStringValue(HKEY_LOCAL_MACHINE,'Software\{#RegCompany}\{#Product}','Help23Installed',temp) ;
  Log('------------------');
  Log('Detected locations');
  Log('------------------');
  Log('Vulcan       : ' + VulcanBaseDir);
  if (Vulcan4Installed) then
    Log('Vulcan 4?    : True' )
  else
    Log('Vulcan 4?    : False' );
  Log('Helpviewer22 : ' + HelpViewer22Dir);
  Log('Helpviewer23 : ' + HelpViewer23Dir);
  Log('VS2015       : ' + Vs2015BaseDir); 
  Log('VS2017       : ' + Vs2017BaseDir1);
  Log('VS2017       : ' + Vs2017BaseDir2);
  Log('VS2017       : ' + Vs2017BaseDir3);
 

  
end;


function VulcanIsInstalled: Boolean;
begin
  result := VulcanInstalled;
end;

function GetVulcanDir(Param: String): String;
begin
  result := VulcanBaseDir;
end;   

function MustInstallVulcanRT: Boolean;
begin
  result := not Vulcan4Installed;
end;


function VulcanPrgAssociated: Boolean;
begin
  result := Vs2015Installed and VulcanPrgAssociation;
end;

function HelpViewer22Found: Boolean;
begin
  result := HelpViewer22Installed;
end;

function HelpViewer23Found: Boolean;
begin
  result := HelpViewer23Installed;

end;

function DelFolder(path: String) : String;
begin
  result := 'IF EXIST "'+path+'\*.*" RD "'+path+'" /s /q'+#13+#10;
end;

function DelUserFolders(path: String) : String;
begin
  result := '';
  result := result + DelFolder(path+'\ComponentModelCache');
  result := result + DelFolder(path+'\VTC');
  result := result + DelFolder(path+'Exp\ComponentModelCache');
  result := result + DelFolder(path+'Exp\Vtc');
end;

function CopyMsBuild(path: String) : String;
begin
  result := '';
  result := result+ 'XCOPY ' + ExpandConstant('"{app}\MsBuild\*.*" "')+path+'" /i /s /y'+#13+#10;

end;

function CopyToVsFolder(path: String) : String;
begin
  result := '';
  result := result+ 'XCOPY '+ExpandConstant('"{app}\Extension\*.*" "')+path+'Extensions\XSharp" /i /s /y'+#13+#10;
  result := result + '"'+path+'Devenv.exe" /updateconfiguration'+#13+#10;

end;

function DeployToVs2015: Boolean;
var commands: string;
begin
  result := Vs2015Installed;
  if result then 
  begin
      commands := '';
      commands := commands + DelFolder(Vs2015Path+'Extensions\XSharp');
      commands := commands + DelUserFolders(ExpandConstant('{#VS14LocalDir}'));
      commands := commands + CopyToVsFolder(Vs2015Path);
      commands := commands + DelFolder(ExpandConstant('{pf}\MsBuild\XSharp'));
      commands := commands + CopyMsBuild(ExpandConstant('{pf}\MsBuild\XSharp'));
      SaveStringToFile( ExpandConstant('{app}\uninst\deployvs2015.cmd'), commands, False);
  end
end;

/////////////////////////////////////////////////////////////////////
function HasVs2017(version: String): Boolean;
begin
  case version of
  '1' : result := Length(vs2017path1) > 0 ;
  '2' : result := Length(vs2017path2) > 0 ;
  '3' : result := Length(vs2017path3) > 0 ;
  end;
end ;


/////////////////////////////////////////////////////////////////////
function DeployToVs2017: Boolean;
var commands: string;
begin
  result := Vs2017Installed;
  if result then 
  begin
      commands := '';
      if HasVs2017('1') then 
      begin
        commands := commands + DelFolder(Vs2017Path1+'Extensions\XSharp');
        commands := commands + DelUserFolders(ExpandConstant('{#VS15LocalDir}'+vs2017InstanceId1));
        commands := commands + CopyToVsFolder(Vs2017Path1);
        commands := commands + DelFolder(Vs2017BaseDir1+'\MsBuild\XSharp');
        commands := commands + CopyMsBuild(Vs2017BaseDir1+'\MsBuild\XSharp');
       end;
      if HasVs2017('2') then 
      begin
        commands := commands + DelFolder(Vs2017Path2+'Extensions\XSharp');
        commands := commands + DelUserFolders(ExpandConstant('{#VS15LocalDir}'+vs2017InstanceId2));
        commands := commands + CopyToVsFolder(Vs2017Path2);
        commands := commands + DelFolder(Vs2017BaseDir2+'\MsBuild\XSharp');
        commands := commands + CopyMsBuild(Vs2017BaseDir2+'\MsBuild\XSharp');
       end ;
      if HasVs2017('3') then 
      begin
        commands := commands + DelFolder(Vs2017Path3+'Extensions\XSharp');
        commands := commands + DelUserFolders(ExpandConstant('{#VS15LocalDir}'+vs2017InstanceId3));
        commands := commands + CopyToVsFolder(Vs2017Path3);
        commands := commands + DelFolder(Vs2017BaseDir3+'\MsBuild\XSharp');
        commands := commands + CopyMsBuild(Vs2017BaseDir3+'\MsBuild\XSharp');
      end;

      SaveStringToFile( ExpandConstant('{app}\uninst\deployvs2017.cmd'), commands, False);
  end
end;

/////////////////////////////////////////////////////////////////////
function Vs2015IsInstalled: Boolean;
begin
  result := Vs2015Installed;
end;

/////////////////////////////////////////////////////////////////////
function vs2017IsInstalled: Boolean;
begin
  result := vs2017Installed;
end;

/////////////////////////////////////////////////////////////////////
function GetVs2015IdeDir(Param: String): String;
begin
  result := Vs2015Path;
end;

/////////////////////////////////////////////////////////////////////
function GetVs2017IdeDir(Param: String): String;
begin
  case param of 
  '1': result := vs2017Path1;
  '2': result := vs2017Path2;
  '3': result := vs2017Path3;
  end;
end;

{ get instance id so we can delete the proper folder in appdata\local }
function GetVs2017InstanceId(Param: String): String;
begin
  case param of 
  '1': result := Vs2017InstanceId1;
  '2': result := Vs2017InstanceId2;
  '3': result := Vs2017InstanceId3;
  end;
end;


/////////////////////////////////////////////////////////////////////
function GetVs2017BaseDir(Param: String): String;
begin
  case param of 
  '1': result := Vs2017BaseDir1;
  '2': result := Vs2017BaseDir2;
  '3': result := Vs2017BaseDir3;
  end;
end;

/////////////////////////////////////////////////////////////////////
function GetHelp22Dir(Param: String): String;
begin
  result := HelpViewer22Dir;
end;

/////////////////////////////////////////////////////////////////////
function GetHelp23Dir(Param: String): String;
begin
  result := HelpViewer23Dir;
end;

/////////////////////////////////////////////////////////////////////
Procedure Checkvs2017Help();
var
  VsHelpItem: Integer;
  Caption: String;
  i: Integer;
begin
    
  for i:= 0 to WizardForm.ComponentsList.Items.Count-1 do
  begin
    Caption := WizardForm.ComponentsList.ItemCaption[i];
    if (Pos('2017',Caption) > 0) and (Pos('documentation', Caption) > 0) then VsHelpItem := i;
  end;
   
  if Vs2017Installed and Not HelpViewer23Installed then
  begin
     WizardForm.ComponentsList.ItemCaption[VsHelpItem] := 'Cannot install the VS 2017 documentation because the Help Viewer component is not installed';
     WizardForm.ComponentsList.ItemEnabled[VsHelpItem] := false;
     WizardForm.ComponentsList.Checked[VsHelpItem] := false;
  end;

end;

/////////////////////////////////////////////////////////////////////
procedure DeleteOldFiles(folder: integer);
var
  FindRec: TFindRec;
  FolderName: String;
  Ok : Boolean;
  i: Integer;
begin
  case folder of 
  2015:   begin
            FolderName := GetVs2015IdeDir('');
            Ok := true;
          end;
  20171:  begin
            FolderName := GetVs2017IdeDir('1');
            Ok := true;
          end ;
  20172:  begin
            FolderName := GetVs2017IdeDir('2');
            Ok := true;
          end;
  20173: begin
          FolderName := GetVs2017IdeDir('3');
          Ok := true;
         end;
  end;
  if Ok then begin
    for i := 1 to 2 do
    begin
      if i = 2 then FolderName := FolderName+'PrivateAssemblies\';
      Log('Delete old files in folder '+FolderName);
      if FindFirst(FolderName+'XSharp*.dll', FindRec) then begin
        try
          repeat
            Log('Delete old file '+FindRec.Name);
            DeleteFile(FolderName+FindRec.Name)
          until not FindNext(FindRec);
        finally
          FindClose(FindRec);
        end;
      end;
      if FindFirst(FolderName+'XSharp*.pdb', FindRec) then begin
        try
          repeat
            Log('Delete old file '+FindRec.Name);
            DeleteFile(FolderName+FindRec.Name)
          until not FindNext(FindRec);
        finally
          FindClose(FindRec);
        end;
      end;
    end;
  end;
end;


/////////////////////////////////////////////////////////////////////
Procedure CurPageChanged(CurPage: Integer);
begin
  PrintButton.Visible := CurPage = wpLicense;
  Checkvs2017Help;
end;


/////////////////////////////////////////////////////////////////////
procedure TypeChange (Sender: TObject);
begin
OriginalTypeChange(Sender);
Checkvs2017Help;
end;

/////////////////////////////////////////////////////////////////////
procedure CurStepChanged(CurStep: TSetupStep);
var ResultCode: integer;
begin
    if CurStep = ssInstall then
    begin
      if (IsUpgrade()) then
      begin
          Log('CurStepChanged = ssInstall; Start uninstall previous version');
          ResultCode := UnInstallPreviousVersion();
          Log('CurStepChanged = ssInstall; End uninstall previous version, result: '+IntToStr(ResultCode));
      end;    
    end;
    if CurStep = ssPostInstall then
    begin
       EnvAddPath(ExpandConstant('{app}\bin'));
    end;
end;

/////////////////////////////////////////////////////////////////////
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
    if CurUninstallStep = usPostUninstall
    then EnvRemovePath(ExpandConstant('{app}\bin'));
end;

/////////////////////////////////////////////////////////////////////
procedure InitializeWizard();
begin
    
    Log('InitializeWizard start');
    { Kill running process to help success the installation}
    TaskKill('xscompiler.exe');
    TaskKill('msbuild.exe');
    PrintButton := TButton.Create(WizardForm);
    PrintButton.Caption := '&Print...';
    PrintButton.Left := WizardForm.InfoAfterPage.Left + 96;
    PrintButton.Top := WizardForm.InfoAfterPage.Height + 88;
    PrintButton.OnClick := @PrintButtonClick;
    PrintButton.Parent := WizardForm.NextButton.Parent;
    OriginalTypeChange := WizardForm.TypesCombo.OnChange ;
    WizardForm.TypesCombo.OnChange := @TypeChange;
    Log('InitializeWizard end');
end;


function InitializeSetup(): Boolean;
var
  ErrorCode: Integer;
begin
  CancelSetup := false;
  Log('InitializeSetup start');
  DetectVS();
  result := not CancelSetup ;
  if result and not Vs2015Installed and not vs2017Installed then
  begin
    if MsgBox('Visual Studio 2015 has not been detected, do you want to download the free Visual Studio Community Edition ?', mbConfirmation, MB_YESNO) = IDYES then
    begin
    ShellExec('open','https://www.visualstudio.com/en-us/downloads/download-visual-studio-vs.aspx','','',SW_SHOW,ewWaitUntilIdle, ErrorCode);
    result := false;
    end
  end;
  { When more than 1 VS 2017 installation, ask for the instance}
  Log('InitializeSetup end');  
end;

/////////////////////////////////////////////////////////////////////
function GetV4NetDir(version: string) : string;
var regkey, regval  : string;
begin

    // in case the target is 3.5, replace 'v4' with 'v3.5'
    // for other info, check out this link 
    // http://stackoverflow.com/questions/199080/how-to-detect-what-net-framework-versions-and-service-packs-are-installed
    regkey := 'SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full'

    RegQueryStringValue(HKLM, regkey, 'InstallPath', regval);

    result := regval;
end; 

/////////////////////////////////////////////////////////////////////
function GetV4Net64Dir(version: string) : string;
var regkey, regval  : string;
begin

    // in case the target is 3.5, replace 'v4' with 'v3.5'
    // for other info, check out this link 
    // http://stackoverflow.com/questions/199080/how-to-detect-what-net-framework-versions-and-service-packs-are-installed
    regkey := 'SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full'

    RegQueryStringValue(HKLM, regkey, 'InstallPath', regval);

    StringChangeEx(regval, '\Framework', '\Framework64', false);
    result := regval;
end; 

/////////////////////////////////////////////////////////////////////
function CreateNGenTask : Boolean;
var commands: string;
var ngenpath: string;
begin
      ngenpath := GetV4NetDir('')+'ngen.exe';
      commands := '';
      commands := commands + ngenpath + ' install "' +ExpandConstant('{app}\bin\xsc.exe')+'" /queue' +#13+#10;
      commands := commands + ngenpath + ' install "' +ExpandConstant('{app}\bin\xsi.exe') +'" /queue'+#13+#10;
      commands := commands + ngenpath + ' install "' +ExpandConstant('{app}\bin\xscompiler.exe') +'" /queue'+#13+#10;
      ngenpath := GetV4Net64Dir('')+'ngen.exe';
      if FileExists(ngenpath) then 
      begin
        commands := commands + ngenpath + ' install "' +ExpandConstant('{app}\bin\xsc.exe')+'" /queue' +#13+#10;
        commands := commands + ngenpath + ' install "' +ExpandConstant('{app}\bin\xsi.exe') +'" /queue'+#13+#10;
        commands := commands + ngenpath + ' install "' +ExpandConstant('{app}\bin\xscompiler.exe') +'" /queue'+#13+#10;
      end;
      SaveStringToFile( ExpandConstant('{app}\uninst\instngen.cmd '), commands, False);
      StringChangeEx(commands, ' install ', ' uninstall ', false);
      StringChangeEx(commands, '/queue', '', false);
      SaveStringToFile( ExpandConstant('{app}\uninst\uninstngen.cmd '), commands, False);
      result := true;
end;


{ Code to run when uninstalling }
function InitializeUninstall(): Boolean;
begin
  TaskKill('xscompiler.exe');
  result := true;
end;


#expr SaveToFile(AddBackslash(SourcePath) + "Preprocessed.iss")
