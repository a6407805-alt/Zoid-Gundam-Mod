#!/usr/bin/env bash
# Packages the Zoid Gundam Mod zip on GitHub's build servers.
set -euo pipefail
mod="$(cd "$(dirname "$0")/.." && pwd)"
stage="$mod/release/stage"
rm -rf "$stage" "$mod/Zoid-Gundam-Mod.zip"
mkdir -p "$stage/Mods"
[ -d "$mod/mods" ] && cp -r "$mod/mods/"* "$stage/Mods/"
[ -d "$mod/maps" ] && cp -r "$mod/maps" "$stage/Maps"
for f in INSTALL.bat UNINSTALL.bat "Restore Steam Play.bat" install.ps1 README.txt manifest.json "Update Zoid Gundam.bat" update.ps1; do
  [ -f "$mod/release/$f" ] && cp "$mod/release/$f" "$stage/"
done
(cd "$stage" && zip -qr9 "$mod/Zoid-Gundam-Mod.zip" .)
echo "Packaged $(du -h "$mod/Zoid-Gundam-Mod.zip" | cut -f1) -> Zoid-Gundam-Mod.zip"
