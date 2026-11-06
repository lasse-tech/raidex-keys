#!/usr/bin/env bash
# Upload the addon zip of release $TAG to CurseForge and Wago.
# Same API calls as BigWigsMods/packager (release.sh); the zip is the one make release built.
set -euo pipefail

repo=${GITHUB_REPOSITORY:?}
tag=${TAG:?}
: "${CF_API_KEY:?secret CF_API_KEY is not set}"
: "${WAGO_API_TOKEN:?secret WAGO_API_TOKEN is not set}"

work=$(mktemp -d)
gh release download "$tag" -R "$repo" -p 'raidex-keys-addon-*.zip' -D "$work"
zip=$(find "$work" -name 'raidex-keys-addon-*.zip' | head -n 1)
[ -n "$zip" ] || { echo "release $tag has no raidex-keys-addon-*.zip"; exit 1; }
gh release view "$tag" -R "$repo" --json body,isPrerelease > "$work/release.json"
jq -r .body "$work/release.json" > "$work/changelog.md"

toc=$(unzip -p "$zip" RaidexKeys/RaidexKeys.toc | tr -d '\r')
field() { sed -n "s/^## $1: *//p" <<< "$toc" | head -n 1; }
# Project ids from the checkout, so a release made before they existed can be sent later.
repo_toc=$(tr -d '\r' < addon/RaidexKeys/RaidexKeys.toc)
cf_id=$(sed -n 's/^## X-Curse-Project-ID: *//p' <<< "$repo_toc")
wago_id=$(sed -n 's/^## X-Wago-ID: *//p' <<< "$repo_toc")
version=$(field Version)
if [ -z "$cf_id" ] || [ -z "$wago_id" ] || [ -z "$version" ]; then
    echo "TOC lacks X-Curse-Project-ID, X-Wago-ID or Version"; exit 1
fi

# 120105 -> 12.1.5
patches=$(field Interface | tr ',' '\n' | tr -d ' ' | awk 'NF { printf "%d.%d.%d\n", int($1 / 10000), int($1 / 100) % 100, $1 % 100 }')
[ -n "$patches" ] || { echo "TOC has no ## Interface:"; exit 1; }
patches_json=$(jq -R . <<< "$patches" | jq -s -c .)

if [ "$(jq -r .isPrerelease "$work/release.json")" = true ]; then
    cf_type=beta; wago_stability=beta
else
    cf_type=release; wago_stability=stable
fi
label="Raidex Keys $version"
echo "$label ($(tr '\n' ' ' <<< "$patches")$cf_type) from $tag"

# CurseForge: game version ids of type 517 (retail)
cf_versions=$(curl -sS --fail -H "x-api-token: $CF_API_KEY" https://wow.curseforge.com/api/game/wow/versions)
cf_ids=$(jq -c --argjson want "$patches_json" '[.[] | select(.gameVersionTypeID == 517 and (.name as $n | $want | index($n))) | .id]' <<< "$cf_versions")
[ "$cf_ids" != "[]" ] || { echo "CurseForge knows none of $patches_json"; exit 1; }
cf_payload=$(jq -n -c --arg name "$label" --argjson ids "$cf_ids" --arg type "$cf_type" --rawfile log "$work/changelog.md" \
    '{displayName: $name, gameVersions: $ids, releaseType: $type, changelog: $log, changelogType: "markdown"}')
# From a file: curl -F cuts an inline value at the first ';'
printf '%s' "$cf_payload" > "$work/cf-metadata.json"
code=$(curl -sS --retry 3 --retry-delay 10 -o "$work/cf.json" -w '%{http_code}' \
    -H "x-api-token: $CF_API_KEY" -F "metadata=<$work/cf-metadata.json" -F "file=@$zip" \
    "https://wow.curseforge.com/api/projects/$cf_id/upload-file")
[ "$code" = 200 ] || { echo "CurseForge: HTTP $code"; cat "$work/cf.json"; exit 1; }
echo "CurseForge: uploaded to project $cf_id"

# Wago: patches it knows for retail
wago_known=$(curl -sS --fail https://addons.wago.io/api/data/game | jq -c '.patches.retail')
wago_patches=$(jq -c --argjson known "$wago_known" '[.[] | select(. as $p | $known | index($p))]' <<< "$patches_json")
[ "$wago_patches" != "[]" ] || { echo "Wago knows none of $patches_json"; exit 1; }
wago_payload=$(jq -n -c --arg label "$label" --argjson p "$wago_patches" --arg s "$wago_stability" --rawfile log "$work/changelog.md" \
    '{label: $label, supported_retail_patches: $p, stability: $s, changelog: $log}')
printf '%s' "$wago_payload" > "$work/wago-metadata.json"
code=$(curl -sS --retry 3 --retry-delay 10 -o "$work/wago.json" -w '%{http_code}' \
    -H "authorization: Bearer $WAGO_API_TOKEN" -H "accept: application/json" \
    -F "metadata=<$work/wago-metadata.json" -F "file=@$zip" \
    "https://addons.wago.io/api/projects/$wago_id/version")
case $code in 200|201) echo "Wago: uploaded to project $wago_id" ;; *) echo "Wago: HTTP $code"; cat "$work/wago.json"; exit 1 ;; esac
