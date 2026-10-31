#!/bin/sh
# SPDX-FileCopyrightText: 2026 lasse tech, Lars Gossard
# SPDX-License-Identifier: MIT
# Raidex Keys - builds the Windows installer with NSIS from the win-x64
# payload (the files the .zip carries) and the packed addon. Called by
# the release build.
#
#   build-setup.sh <stage> <addon> <source> <version> <build> <out dir>
#
#   stage    the win-x64 payload: RaidexKeys.exe, LICENSE.txt,
#            THIRD-PARTY-NOTICES.txt, licenses/
#   addon    the packed addon folder, RaidexKeys/ as WoW loads it
#   source   the source tree it was built from (installer script, icon, panel)
#   version  the app's version, e.g. 0.12.0
#   build    the build number
#
# SIGN, if set, is a command that signs the file named last, in place (a
# wrapper around osslsigncode, for one): the installer and its uninstaller
# are then signed. Unset, both go out unsigned.
set -eu

[ $# -eq 6 ] || { echo "usage: $0 <stage> <addon> <source> <version> <build> <out dir>" >&2; exit 2; }
command -v makensis >/dev/null 2>&1 || { echo "makensis is missing - sudo apt install nsis" >&2; exit 1; }

# makensis works from the script's folder, so every path goes in absolute.
mkdir -p "$6"
STAGE=$(cd "$1" && pwd)
ADDON=$(cd "$2" && pwd)
SRC=$(cd "$3" && pwd)
VERSION=$4
BUILD=$5
OUT=$(cd "$6" && pwd)

WINDOWS=$SRC/desktop/packaging/windows
SETUP=$OUT/raidex-keys-$VERSION-win-x64-setup.exe
WORK=$OUT/.setup-work

rm -rf "$WORK"
mkdir -p "$WORK"
# The licence page shows plain text right only as UTF-8 with BOM and CRLF.
{ printf '\357\273\277'; sed 's/$/\r/' "$STAGE/LICENSE.txt"; } > "$WORK/LICENSE.txt"

rm -f "$SETUP"
makensis -INPUTCHARSET UTF8 -V2 -WX \
    -DVERSION="$VERSION" -DBUILD="$BUILD" \
    -DSTAGE="$STAGE" -DADDON="$ADDON" -DLICENSE="$WORK/LICENSE.txt" \
    -DICON="$SRC/desktop/src/RaidexKeys.App/Assets/raidex-keys.ico" \
    -DWIZARD="$WINDOWS/wizard.bmp" -DOUTFILE="$SETUP" \
    ${SIGN:+"-DSIGN=$SIGN"} \
    "$WINDOWS/installer.nsi"
rm -rf "$WORK"
echo "built: $SETUP"
