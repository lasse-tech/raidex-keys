@echo off
rem SPDX-FileCopyrightText: 2026 lasse tech, Lars Gossard
rem SPDX-License-Identifier: MIT
rem Raidex Keys on Windows (Linux: Makefile). Makefile.bat help lists the targets.

setlocal
set SLN=desktop\RaidexKeys.slnx
set APP=desktop\src\RaidexKeys.App\RaidexKeys.App.csproj
set RID=win-x64
set DIST=dist\%RID%
set TARGET=%LOCALAPPDATA%\Programs\RaidexKeys
set SHORTCUT=%APPDATA%\Microsoft\Windows\Start Menu\Programs\Raidex Keys.lnk
rem Set by the Windows installer.
set UNINSTKEY=HKCU\Software\Microsoft\Windows\CurrentVersion\Uninstall\RaidexKeys
if not defined WOW_DIR set "WOW_DIR=%ProgramFiles(x86)%\World of Warcraft"
if not defined ADDONS_DIR set "ADDONS_DIR=%WOW_DIR%\_retail_\Interface\AddOns"

if "%~1"=="" goto help
goto %~1 2>nul || (echo unknown target: %~1 & goto help)

rem help: list the targets
:help
for /f "tokens=1,*" %%a in ('findstr /b /r /c:"rem [abcdefghijklmnopqrstuvwxyz][abcdefghijklmnopqrstuvwxyz0123456789-]*: " "%~f0"') do echo   %%b
exit /b 0

rem debug: build unoptimised
:debug
dotnet build %SLN% -c Debug -warnaserror || exit /b 1
exit /b 0

rem run: start the debug build
:run
dotnet run --project %APP% -c Debug
exit /b %errorlevel%

rem preview: start with sample data: Makefile.bat preview guild
:preview
set SECTION=%~2
if "%SECTION%"=="" set SECTION=overview
dotnet run --project %APP% -c Debug -- --preview %SECTION%
exit /b %errorlevel%

rem lint: warning-free build and formatting
:lint
dotnet build %SLN% -c Release -warnaserror -nologo || exit /b 1
dotnet format %SLN% --verify-no-changes || exit /b 1
exit /b 0

rem test: Core tests (xunit)
:test
dotnet test %SLN% -nologo -v q || exit /b 1
exit /b 0

rem install: the app: release build into %LOCALAPPDATA%\Programs\RaidexKeys, Start menu entry
:install
if exist "%DIST%" rmdir /s /q "%DIST%"
dotnet publish %APP% -c Release -r %RID% --self-contained -p:PublishSingleFile=true -o %DIST% || exit /b 1
if not exist "%TARGET%\licenses" mkdir "%TARGET%\licenses"
del /q "%TARGET%\*.pdb" 2>nul
copy /y "%DIST%\RaidexKeys.exe" "%TARGET%\" >nul || exit /b 1
copy /y desktop\LICENSE "%TARGET%\LICENSE.txt" >nul || exit /b 1
copy /y desktop\packaging\THIRD-PARTY-NOTICES.txt "%TARGET%\" >nul || exit /b 1
copy /y desktop\src\RaidexKeys.App\Assets\Fonts\OFL-*.txt "%TARGET%\licenses\" >nul || exit /b 1
powershell -NoProfile -Command "$s = (New-Object -ComObject WScript.Shell).CreateShortcut('%SHORTCUT%'); $s.TargetPath = '%TARGET%\RaidexKeys.exe'; $s.WorkingDirectory = '%TARGET%'; $s.Save()" || exit /b 1
echo installed: %TARGET%\RaidexKeys.exe
exit /b 0

rem uninstall: remove what install put down
:uninstall
if exist "%TARGET%" rmdir /s /q "%TARGET%"
if exist "%SHORTCUT%" del "%SHORTCUT%"
reg delete "%UNINSTKEY%" /f >nul 2>&1
echo removed; settings in %APPDATA%\RaidexKeys are kept
exit /b 0

rem install-addon: the addon into WoW (set WOW_DIR=... or ADDONS_DIR=... if not found)
:install-addon
rem No ( ) blocks around the path: "Program Files (x86)" would close them.
if not exist "%ADDONS_DIR%" goto addon_missing
if exist "%ADDONS_DIR%\RaidexKeys" rmdir /s /q "%ADDONS_DIR%\RaidexKeys"
xcopy /e /i /q addon\RaidexKeys "%ADDONS_DIR%\RaidexKeys" >nul || exit /b 1
echo addon installed: %ADDONS_DIR%\RaidexKeys - /reload in game
exit /b 0

:addon_missing
echo WoW AddOns folder not found: "%ADDONS_DIR%"
echo Set WOW_DIR to your World of Warcraft folder and try again.
exit /b 1

rem uninstall-addon: remove the addon from WoW; its SavedVariables stay
:uninstall-addon
if exist "%ADDONS_DIR%\RaidexKeys" rmdir /s /q "%ADDONS_DIR%\RaidexKeys"
echo addon removed; its SavedVariables stay
exit /b 0

rem deploy: no server - points to the README's build section
:deploy
echo Raidex Keys has no server to deploy. Build and install: README.md, section 'Build from source'.
exit /b 0

rem clean: remove build output
:clean
dotnet clean %SLN% -v q -nologo
if exist dist rmdir /s /q dist
exit /b 0

rem distclean: clean, plus bin\ and obj\
:distclean
call "%~f0" clean
for /d %%d in (desktop\src\* desktop\tests\* desktop\tools\*) do (
  if exist "%%d\bin" rmdir /s /q "%%d\bin"
  if exist "%%d\obj" rmdir /s /q "%%d\obj"
)
exit /b 0
