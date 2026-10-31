#!/bin/sh
# SPDX-FileCopyrightText: 2026 lasse tech, Lars Gossard
# SPDX-License-Identifier: MIT
# Raidex Keys - builds the .deb from the Linux payload, the files the .tar.gz
# carries, so both install the same bytes. Called by the release build.
#
#   build-deb.sh <stage> <source> <version> <out dir>
#
#   stage    the linux-x64 payload: RaidexKeys, LICENSE.txt,
#            THIRD-PARTY-NOTICES.txt, licenses/, share/icons,
#            share/raidex-keys.desktop
#   source   the source tree it was built from (CHANGELOG.md, AppStream metadata)
#   version  the app's version, e.g. 0.12.0
#
# SOURCE_DATE_EPOCH, if set, dates the release in the AppStream metadata and
# clamps every file time in the package: one commit, one .deb.
#
# The layout follows a user install (~/.local/opt/raidex-keys): the app in
# /opt/raidex-keys, /usr/bin/raidex-keys pointing at it, starter, icons and
# AppStream metadata under /usr/share. It refreshes the icon cache and the menu
# itself: dpkg's triggers do that on Debian and Ubuntu, but a stale
# icon-theme.cache hides a new icon, and not every desktop ships the packages
# those triggers come with.
set -eu

[ $# -eq 4 ] || { echo "usage: $0 <stage> <source> <version> <out dir>" >&2; exit 2; }
STAGE=$1
SRC=$2
VERSION=$3
OUT=$4

PKG=raidex-keys
ARCH=amd64
APPDIR=/opt/$PKG
METAINFO=app.raidex.Keys.metainfo.xml
DEB=$OUT/${PKG}_${VERSION}_$ARCH.deb
ROOT=$OUT/.deb-root
DATE=$(date -u -d "@${SOURCE_DATE_EPOCH:-$(date +%s)}" +%F)

rm -rf "$ROOT"
mkdir -p "$ROOT/DEBIAN" "$ROOT$APPDIR" "$ROOT/usr/bin" "$ROOT/usr/share/applications" \
    "$ROOT/usr/share/metainfo" "$ROOT/usr/share/doc/$PKG"

cp "$STAGE/RaidexKeys" "$STAGE/LICENSE.txt" "$STAGE/THIRD-PARTY-NOTICES.txt" "$ROOT$APPDIR/"
cp -r "$STAGE/licenses" "$ROOT$APPDIR/"
ln -s "$APPDIR/RaidexKeys" "$ROOT/usr/bin/$PKG"
cp -r "$STAGE/share/icons" "$ROOT/usr/share/icons"
sed "s|@BIN@|/usr/bin|" "$STAGE/share/$PKG.desktop" > "$ROOT/usr/share/applications/$PKG.desktop"
sed -e "s|@VERSION@|$VERSION|g" -e "s|@DATE@|$DATE|g" \
    "$SRC/desktop/packaging/linux/$METAINFO" > "$ROOT/usr/share/metainfo/$METAINFO"

# Debian's place for the terms and the history of changes.
{
    cat "$STAGE/LICENSE.txt"
    printf '\n\n'
    cat "$STAGE/THIRD-PARTY-NOTICES.txt"
} > "$ROOT/usr/share/doc/$PKG/copyright"
gzip -9n < "$SRC/CHANGELOG.md" > "$ROOT/usr/share/doc/$PKG/changelog.gz"

chmod -R u=rwX,go=rX "$ROOT"
chmod 755 "$ROOT$APPDIR/RaidexKeys"

# Linked: libc6, libgcc-s1, libstdc++6 (.NET), libfontconfig1 (Skia) - as
# dpkg-shlibdeps finds them. Loaded at runtime, so shlibdeps can't see them:
# the X11 libraries (Avalonia), ICU (globalisation - .NET takes any version,
# newest listed first) and OpenSSL with the CA store (HTTPS to Raidex).
# Recommended: xdg-open for links, secret-tool for the Raidex session (else a
# 0600 file), a portal or GTK for the folder picker, libGL (else software).
cat > "$ROOT/DEBIAN/control" <<EOF
Package: $PKG
Version: $VERSION
Architecture: $ARCH
Maintainer: lasse tech, Lars Gossard <lars@lasse-tech.de>
Installed-Size: $(du -sk --apparent-size --exclude=DEBIAN "$ROOT" | cut -f1)
Depends: libc6 (>= 2.34), libgcc-s1, libstdc++6, libfontconfig1,
 libx11-6, libice6, libsm6, libxcursor1, libxext6, libxi6, libxrandr2,
 libicu78 | libicu77 | libicu76 | libicu74 | libicu72 | libicu70,
 libssl3t64 | libssl3, ca-certificates
Recommends: xdg-utils, libsecret-tools, xdg-desktop-portal | libgtk-3-0t64 | libgtk-3-0, libgl1
Section: games
Priority: optional
Homepage: https://raidex.app/keys/
Description: Mythic+ keystones, rating, vault and routes on the desktop
 Raidex Keys shows the Mythic+ week of all your World of Warcraft characters
 outside the game: keystone, rating, best runs per dungeon and Great Vault
 progress, a rating calculator, your Mythic Dungeon Tools routes drawn on the
 dungeon's map, and your guild's keystones.
 .
 It reads what its WoW addon saves - the addon is a separate download - and
 finds WoW in Lutris, Wine, Steam/Proton and Bottles prefixes.
 .
 The app is open source under the MIT License.
EOF

# The icon cache and the menu, refreshed after installing and after removing.
for script in postinst postrm; do
    cat > "$ROOT/DEBIAN/$script" <<'SCRIPT'
#!/bin/sh
set -e
case "$1" in
    configure|abort-upgrade|remove|purge|abort-install)
        if command -v gtk-update-icon-cache >/dev/null 2>&1; then
            gtk-update-icon-cache -q -f -t /usr/share/icons/hicolor || true
        fi
        if command -v update-desktop-database >/dev/null 2>&1; then
            update-desktop-database -q /usr/share/applications || true
        fi
        ;;
esac
SCRIPT
    chmod 755 "$ROOT/DEBIAN/$script"
done

(cd "$ROOT" && find . -type f ! -path './DEBIAN/*' -printf '%P\0' | sort -z | xargs -0 md5sum) \
    > "$ROOT/DEBIAN/md5sums"
chmod 644 "$ROOT/DEBIAN/control" "$ROOT/DEBIAN/md5sums"

rm -f "$DEB"
dpkg-deb --root-owner-group -Zxz --build "$ROOT" "$DEB" >/dev/null
rm -rf "$ROOT"
echo "built: $DEB"
