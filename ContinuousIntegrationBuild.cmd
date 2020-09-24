@echo off
if "%VSVERSION%" == "" SET VSVERSION=2019
if "%VSEDITION%" == "" SET VSEDITION=Enterprise
set VsBatch="C:\Program Files (x86)\Microsoft Visual Studio\%VSVERSION%\%VSEDITION%\Common7\Tools\VsDevCmd.bat"
if not exist %VsBatch% goto Error
call %VsBatch%
rem echo Start restoring Roslyn ...
rem call "%~dp0\Roslyn\Restore.cmd"
rem echo Start building Roslyn Compiler ...
rem msbuild "%~dp0\Roslyn\Compilers.sln" /v:m /m
echo Start building XSharp Compilers (Release Configuration)
cd "%~dp0\XSharp"
call Build.cmd Release
Goto End
:Error
Echo Could not find the file %VsBatch%
:End
