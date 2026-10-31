; SPDX-FileCopyrightText: 2026 lasse tech, Lars Gossard
; SPDX-License-Identifier: MIT
;
; Raidex Keys - the Windows installer. NSIS 3, built on Linux by build-setup.sh
; beside this file, in the release build.
;
; Per user and without admin rights, like Makefile.bat install: the app goes to
; %LOCALAPPDATA%\Programs\RaidexKeys, with a Start-menu entry, a desktop one on
; request and an entry in Windows' app list to uninstall it. The WoW addon
; comes along unless deselected. For it the installer proposes the AddOns
; folder the way the app finds it - the folder confirmed last time, else
; Blizzard's registry key, else the usual path - and the user confirms it. A
; fresh install writes that folder into the app's settings, so the app doesn't
; ask again at first start.
;
; Defines, all passed by build-setup.sh:
;   VERSION  the app's version (0.12.0)    BUILD    the build number
;   STAGE    the win-x64 payload           ADDON    the packed addon folder
;   LICENSE  LICENSE.txt as UTF-8 with BOM and CRLF, as the licence page needs it
;   ICON     the app's .ico                WIZARD   the welcome/finish panel
;   OUTFILE  the setup .exe
;   SIGN     optional: a command that signs the file named last, in place

Unicode true
ManifestDPIAware true
RequestExecutionLevel user
SetCompressor /SOLID lzma

!include MUI2.nsh
!include LogicLib.nsh
!include Sections.nsh
!include FileFunc.nsh
!include WordFunc.nsh

!define APPNAME "Raidex Keys"
!define EXE "RaidexKeys.exe"
!define UNINSTKEY "Software\Microsoft\Windows\CurrentVersion\Uninstall\RaidexKeys"
; In the 32-bit registry view this is HKLM\SOFTWARE\WOW6432Node\…, the key the
; app's WowLocator reads.
!define BLIZZARDKEY "SOFTWARE\Blizzard Entertainment\World of Warcraft"

Name "${APPNAME}"
OutFile "${OUTFILE}"
InstallDir "$LOCALAPPDATA\Programs\RaidexKeys"
InstallDirRegKey HKCU "${UNINSTKEY}" "InstallLocation"
BrandingText "${APPNAME} ${VERSION} (Build ${BUILD})"

!ifdef SIGN
  !finalize '${SIGN} "%1"'
  !uninstfinalize '${SIGN} "%1"'
!endif

Var AddOnsDir

!define MUI_ICON "${ICON}"
!define MUI_UNICON "${ICON}"
!define MUI_WELCOMEFINISHPAGE_BITMAP "${WIZARD}"
!define MUI_UNWELCOMEFINISHPAGE_BITMAP "${WIZARD}"
!define MUI_ABORTWARNING
!define MUI_COMPONENTSPAGE_NODESC

!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_LICENSE "${LICENSE}"
!insertmacro MUI_PAGE_COMPONENTS
!insertmacro MUI_PAGE_DIRECTORY

; The AddOns folder - only when the addon is to be installed.
!define MUI_PAGE_HEADER_TEXT "$(AddOnsHeader)"
!define MUI_PAGE_HEADER_SUBTEXT "$(AddOnsSubheader)"
!define MUI_DIRECTORYPAGE_TEXT_TOP "$(AddOnsText)"
!define MUI_DIRECTORYPAGE_TEXT_DESTINATION "$(AddOnsFolder)"
!define MUI_DIRECTORYPAGE_VARIABLE $AddOnsDir
!define MUI_PAGE_CUSTOMFUNCTION_PRE AddOnsPre
!define MUI_PAGE_CUSTOMFUNCTION_LEAVE AddOnsLeave
!insertmacro MUI_PAGE_DIRECTORY

!insertmacro MUI_PAGE_INSTFILES

!define MUI_FINISHPAGE_RUN "$INSTDIR\${EXE}"
!insertmacro MUI_PAGE_FINISH

!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES

; English first: it is the fallback for every system language but German.
!insertmacro MUI_LANGUAGE "English"
!insertmacro MUI_LANGUAGE "German"

; NSIS's own German addresses the user as "Sie"; these texts stay impersonal
; so the two don't clash.
LangString SecAddon ${LANG_ENGLISH} "WoW addon"
LangString SecAddon ${LANG_GERMAN} "WoW-Addon"
LangString SecDesktop ${LANG_ENGLISH} "Desktop shortcut"
LangString SecDesktop ${LANG_GERMAN} "Verknüpfung auf dem Desktop"
LangString AddOnsHeader ${LANG_ENGLISH} "WoW's AddOns folder"
LangString AddOnsHeader ${LANG_GERMAN} "AddOns-Ordner von WoW"
LangString AddOnsSubheader ${LANG_ENGLISH} "Where the addon goes."
LangString AddOnsSubheader ${LANG_GERMAN} "Wohin das Addon kommt."
LangString AddOnsText ${LANG_ENGLISH} "Raidex Keys reads what its addon saves in World of Warcraft. Setup has looked for WoW; please check the folder - it ends in _retail_\Interface\AddOns."
LangString AddOnsText ${LANG_GERMAN} "Raidex Keys liest, was sein Addon in World of Warcraft speichert. Setup hat nach WoW gesucht; bitte den Ordner prüfen – er endet auf _retail_\Interface\AddOns."
LangString AddOnsFolder ${LANG_ENGLISH} "AddOns folder"
LangString AddOnsFolder ${LANG_GERMAN} "AddOns-Ordner"
LangString AddOnsUnusual ${LANG_ENGLISH} "This does not look like WoW's AddOns folder: there is no Wow.exe two levels above it.$\r$\n$\r$\nUse it anyway?"
LangString AddOnsUnusual ${LANG_GERMAN} "Das sieht nicht nach dem AddOns-Ordner von WoW aus: Zwei Ebenen darüber liegt keine Wow.exe.$\r$\n$\r$\nTrotzdem verwenden?"
LangString AppRunning ${LANG_ENGLISH} "Raidex Keys is still running. Please close it and try again."
LangString AppRunning ${LANG_GERMAN} "Raidex Keys läuft noch. Bitte die App beenden und erneut versuchen."
LangString RemoveAddon ${LANG_ENGLISH} "Remove the Raidex Keys addon from World of Warcraft as well?$\r$\n$\r$\nIts saved data in WTF stays."
LangString RemoveAddon ${LANG_GERMAN} "Das Raidex-Keys-Addon auch aus World of Warcraft entfernen?$\r$\n$\r$\nSeine gespeicherten Daten in WTF bleiben."

VIProductVersion "${VERSION}.0"
VIFileVersion "${VERSION}.0"
VIAddVersionKey /LANG=${LANG_ENGLISH} "ProductName" "${APPNAME}"
VIAddVersionKey /LANG=${LANG_ENGLISH} "ProductVersion" "${VERSION}"
VIAddVersionKey /LANG=${LANG_ENGLISH} "FileVersion" "${VERSION}"
VIAddVersionKey /LANG=${LANG_ENGLISH} "FileDescription" "${APPNAME} Setup"
VIAddVersionKey /LANG=${LANG_ENGLISH} "CompanyName" "lasse tech"
VIAddVersionKey /LANG=${LANG_ENGLISH} "LegalCopyright" "© 2026 lasse tech, Lars Gossard"

; The running app keeps its .exe locked: replacing or removing it has to wait.
!macro EnsureClosed un
  Function ${un}EnsureClosed
    retry:
    ClearErrors
    ${If} ${FileExists} "$INSTDIR\${EXE}"
      Delete "$INSTDIR\${EXE}"
      ${If} ${Errors}
        MessageBox MB_RETRYCANCEL|MB_ICONEXCLAMATION "$(AppRunning)" /SD IDCANCEL IDRETRY retry
        Abort
      ${EndIf}
    ${EndIf}
  FunctionEnd
!macroend
!insertmacro EnsureClosed ""
!insertmacro EnsureClosed "un."

Section "!${APPNAME}" SecApp
  SectionIn RO
  Call EnsureClosed
  SetOutPath "$INSTDIR\licenses"
  File "${STAGE}/licenses/*.txt"
  SetOutPath "$INSTDIR"
  File "${STAGE}/${EXE}"
  File "${STAGE}/LICENSE.txt"
  File "${STAGE}/THIRD-PARTY-NOTICES.txt"
  ; Debug symbols an older Makefile.bat install left behind.
  Delete "$INSTDIR\*.pdb"
  WriteUninstaller "$INSTDIR\Uninstall.exe"
  CreateShortcut "$SMPROGRAMS\${APPNAME}.lnk" "$INSTDIR\${EXE}"

  WriteRegStr HKCU "${UNINSTKEY}" "DisplayName" "${APPNAME}"
  WriteRegStr HKCU "${UNINSTKEY}" "DisplayVersion" "${VERSION}"
  WriteRegStr HKCU "${UNINSTKEY}" "Publisher" "lasse tech"
  WriteRegStr HKCU "${UNINSTKEY}" "DisplayIcon" "$INSTDIR\${EXE}"
  WriteRegStr HKCU "${UNINSTKEY}" "InstallLocation" "$INSTDIR"
  WriteRegStr HKCU "${UNINSTKEY}" "UninstallString" '"$INSTDIR\Uninstall.exe"'
  WriteRegStr HKCU "${UNINSTKEY}" "QuietUninstallString" '"$INSTDIR\Uninstall.exe" /S'
  WriteRegStr HKCU "${UNINSTKEY}" "URLInfoAbout" "https://raidex.app/keys/"
  WriteRegStr HKCU "${UNINSTKEY}" "HelpLink" "https://github.com/lasse-tech/raidex-keys/issues"
  WriteRegDWORD HKCU "${UNINSTKEY}" "NoModify" 1
  WriteRegDWORD HKCU "${UNINSTKEY}" "NoRepair" 1
  ${GetSize} "$INSTDIR" "/S=0K" $0 $1 $2
  WriteRegDWORD HKCU "${UNINSTKEY}" "EstimatedSize" $0
SectionEnd

Section "$(SecAddon)" SecAddon
  ${If} $AddOnsDir == ""
    Return
  ${EndIf}
  ; A fresh copy, so no file an older version had lingers. The addon's saved
  ; data lives in WTF, not here.
  RMDir /r "$AddOnsDir\RaidexKeys"
  SetOutPath "$AddOnsDir\RaidexKeys"
  File /r "${ADDON}/*"
  WriteRegStr HKCU "${UNINSTKEY}" "AddOnsFolder" "$AddOnsDir"

  ; What the app would ask at first start has just been confirmed here. Only a
  ; first install writes it - existing settings are the app's. UTF-16 with BOM,
  ; which .NET reads as well as the UTF-8 the app writes itself.
  ${IfNot} ${FileExists} "$APPDATA\RaidexKeys\settings.json"
    ${WordReplace} "$AddOnsDir" "\" "\\" "+" $0
    CreateDirectory "$APPDATA\RaidexKeys"
    FileOpen $1 "$APPDATA\RaidexKeys\settings.json" w
    FileWriteWord $1 0xFEFF
    FileWriteUTF16LE $1 '{$\r$\n  "AddOnsFolder": "$0"$\r$\n}$\r$\n'
    FileClose $1
  ${EndIf}
SectionEnd

Section /o "$(SecDesktop)" SecDesktop
  CreateShortcut "$DESKTOP\${APPNAME}.lnk" "$INSTDIR\${EXE}"
SectionEnd

Function .onInit
  ReadRegStr $AddOnsDir HKCU "${UNINSTKEY}" "AddOnsFolder"
  ${If} $AddOnsDir == ""
    SetRegView 32
    ReadRegStr $0 HKLM "${BLIZZARDKEY}" "InstallPath"
    ${If} $0 != ""
      ; Blizzard's key points at the _retail_ folder or at the WoW folder.
      StrCpy $1 $0 1 -1
      ${If} $1 == "\"
        StrCpy $0 $0 -1
      ${EndIf}
      ${GetFileName} "$0" $1
      ${If} $1 == "_retail_"
        StrCpy $AddOnsDir "$0\Interface\AddOns"
      ${Else}
        StrCpy $AddOnsDir "$0\_retail_\Interface\AddOns"
      ${EndIf}
    ${Else}
      StrCpy $AddOnsDir "$PROGRAMFILES32\World of Warcraft\_retail_\Interface\AddOns"
    ${EndIf}
  ${EndIf}
FunctionEnd

Function AddOnsPre
  ${IfNot} ${SectionIsSelected} ${SecAddon}
    Abort
  ${EndIf}
FunctionEnd

; WoW keeps its addons in _retail_\Interface\AddOns, beside _retail_\Wow.exe.
Function AddOnsLeave
  ${IfNot} ${FileExists} "$AddOnsDir\..\..\Wow.exe"
    MessageBox MB_YESNO|MB_ICONQUESTION "$(AddOnsUnusual)" /SD IDYES IDYES +2
    Abort
  ${EndIf}
FunctionEnd

; Known files only: $INSTDIR may be a folder the user picked, so nothing is
; removed recursively. The settings in %APPDATA%\RaidexKeys stay.
Section "Uninstall"
  Call un.EnsureClosed
  Delete "$INSTDIR\LICENSE.txt"
  Delete "$INSTDIR\THIRD-PARTY-NOTICES.txt"
  Delete "$INSTDIR\licenses\OFL-*.txt"
  RMDir "$INSTDIR\licenses"
  Delete "$INSTDIR\Uninstall.exe"
  RMDir "$INSTDIR"
  Delete "$SMPROGRAMS\${APPNAME}.lnk"
  Delete "$DESKTOP\${APPNAME}.lnk"

  ReadRegStr $0 HKCU "${UNINSTKEY}" "AddOnsFolder"
  ${If} $0 != ""
  ${AndIf} ${FileExists} "$0\RaidexKeys\*.*"
    MessageBox MB_YESNO|MB_ICONQUESTION "$(RemoveAddon)" /SD IDNO IDNO +2
    RMDir /r "$0\RaidexKeys"
  ${EndIf}
  DeleteRegKey HKCU "${UNINSTKEY}"
SectionEnd
