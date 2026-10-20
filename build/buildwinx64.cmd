@echo off
rem Builds OMT-HX and the plugin, then leaves everything OBS loads in the publish folder.
setlocal
set ROOT=%~dp0..
set OMTHX=%ROOT%\..\OMT-HX
if not exist "%OMTHX%\CMakeLists.txt" (
  echo OMT-HX not found at %OMTHX%. Clone it beside this repository, or set OMTHX.
  exit /b 1
)
powershell -NoProfile -ExecutionPolicy Bypass -File "%OMTHX%\scripts\build.ps1" -Preset release -Shared
if errorlevel 1 exit /b 1
dotnet publish "%ROOT%\omtplugin.csproj" -r win-x64 -c Release
if errorlevel 1 exit /b 1
set OUT=%ROOT%\bin\Release\net8.0\win-x64\publish
copy /y "%OMTHX%\build\release\omthx.dll" "%OUT%\" >nul
copy /y "%OMTHX%\build\release\libomt.dll" "%OUT%\" >nul
copy /y "%OMTHX%\build\release\libvmx.dll" "%OUT%\" >nul
copy /y "%OMTHX%\build\release\av*.dll" "%OUT%\" >nul
copy /y "%OMTHX%\build\release\sw*.dll" "%OUT%\" >nul
echo Plugin and OMT-HX libraries are in %OUT%