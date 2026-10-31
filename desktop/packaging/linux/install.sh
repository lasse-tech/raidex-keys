#!/bin/sh
# SPDX-FileCopyrightText: 2026 lasse tech, Lars Gossard
# SPDX-License-Identifier: MIT
# Raidex Keys - installs this release for the current user, the same way
# `make install` does from the source: the app under ~/.local/opt, a
# `raidex-keys` command, the menu entry and the icons.
#
#   ./install.sh               install, or update an earlier install
#   ./install.sh --uninstall   remove it again; settings in ~/.config/raidex-keys stay
#
# PREFIX=/usr/local ./install.sh installs system-wide (run it with sudo).
set -eu

HERE=$(cd "$(dirname "$0")" && pwd)
PREFIX=${PREFIX:-$HOME/.local}
OPT=$PREFIX/opt/raidex-keys
BIN=$PREFIX/bin
APPLICATIONS=$PREFIX/share/applications
ICONS=$PREFIX/share/icons/hicolor

refresh() {
    if command -v update-desktop-database >/dev/null 2>&1; then update-desktop-database "$APPLICATIONS" || true; fi
    if command -v gtk-update-icon-cache >/dev/null 2>&1; then gtk-update-icon-cache -q -t "$ICONS" || true; fi
}

if [ "${1:-}" = "--uninstall" ]; then
    rm -rf "$OPT"
    rm -f "$BIN/raidex-keys" "$APPLICATIONS/raidex-keys.desktop"
    find "$ICONS" -name 'raidex-keys.*' -path '*/apps/*' -delete 2>/dev/null || true
    refresh
    echo "Raidex Keys removed; settings in ~/.config/raidex-keys are kept."
    exit 0
fi

rm -rf "$OPT"
mkdir -p "$OPT" "$BIN" "$APPLICATIONS"
cp "$HERE/RaidexKeys" "$HERE/LICENSE.txt" "$HERE/THIRD-PARTY-NOTICES.txt" "$OPT/"
cp -r "$HERE/licenses" "$OPT/"
chmod 755 "$OPT/RaidexKeys"
ln -sf "$OPT/RaidexKeys" "$BIN/raidex-keys"
(cd "$HERE/share/icons/hicolor" && find . -type f) | while read -r icon; do
    mkdir -p "$ICONS/$(dirname "$icon")"
    cp "$HERE/share/icons/hicolor/$icon" "$ICONS/$icon"
done
sed "s|@BIN@|$BIN|" "$HERE/share/raidex-keys.desktop" > "$APPLICATIONS/raidex-keys.desktop"
refresh

echo "Raidex Keys installed: $BIN/raidex-keys"
case ":$PATH:" in
    *":$BIN:"*) ;;
    *) echo "Note: $BIN is not in your PATH - start it from the menu, or add it." ;;
esac
echo "The WoW addon comes separately: raidex-keys-addon-<version>.zip from the same release."
