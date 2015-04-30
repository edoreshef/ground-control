@echo off

rem Find current version
..\3rdParty\Tools\rxfind.exe .\Properties\AssemblyInfo.cs  /P:"(\[assembly: AssemblyVersion\()\"(\d+.\d+)(.\d+.\d+)(\"\)\])" /O /O /R:"$2$3" > Temp.txt
set /p Version=<Temp.txt
del Temp.txt

rem Query for current version
cls
set /p Version=Please enter version number [%Version%]?

rem Change version
..\3rdParty\Tools\rxfind.exe .\Properties\AssemblyInfo.cs /P:"(\[assembly: AssemblyVersion\()\"(\d+.\d+.\d+.\d+)(\"\)\])" /B:2 /Q /R:"$1\"%Version%$3"
..\3rdParty\Tools\rxfind.exe .\Properties\AssemblyInfo.cs /P:"(\[assembly: AssemblyFileVersion\()\"(\d+.\d+.\d+.\d+)(\"\)\])" /B:2 /Q /R:"$1\"%Version%$3"

rem Rebuild solution
"C:\Program Files (x86)\Microsoft Visual Studio 12.0\Common7\IDE\devenv" ..\GroundControl.sln /Rebuild Release

rem Get Major.Minor version
..\3rdParty\Tools\rxfind.exe .\Properties\AssemblyInfo.cs  /P:"(\[assembly: AssemblyVersion\()\"(\d+.\d+)(.\d+.\d+)(\"\)\])" /O /O /R:"$2" > Temp.txt
set /p MMVersion=<Temp.txt
del Temp.txt

rem Put things into zip file
pushd bin\release
"c:\Program Files\WinRAR\WinRAR.exe" a -afzip ..\..\..\Binaries\GroundControl.zip GroundControl.exe
"c:\Program Files\WinRAR\WinRAR.exe" a -afzip ..\..\..\Binaries\GroundControl.zip NAudio.dll
popd

rem Copy to dropbox
copy ..\Binaries\GroundControl.zip C:\Users\User\Dropbox\Public /Y