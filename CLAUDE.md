# Zoid Gundam Mod — Notes for Claude / Antigravity

Rusted Warfare total conversion mod pack (Gundam Federation, Zeon Amphibious Corps, Zoids Alliance).
Repo: `a6407805-alt/Zoid-Gundam-Mod` (public).

## Layout
- `mods/Zoid Gundam/` — shipped Rusted Warfare mod (`BUILDINGS/`, `units/`, `Multiplayer+FullGame.dll`, `mod-info.txt`, `mod-thumbnail.png`). Installed to `<Rusted Warfare>\mods\units\Zoid Gundam\`.
- `launcher/` — WPF launcher (`ZoidGundamMod.csproj` -> `Zoid-Gundam-Mod.exe`, `roster.json` = unit cards, faction tiles, mode tiles).
- `release/` — installer (`install.ps1`), updater (`update.ps1`), `package.sh`.
- **Steam App ID**: `647960` (`steam://rungameid/647960`). `install.ps1` sets `LaunchOptions` under `UserLocalConfigStore > Software > Valve > Steam > apps > 647960`.
- **Auto-Sync**: `StagingSync.cs` syncs `Desktop\Zoid Gundam` <-> `Rusted Warfare\mods\units\Zoid Gundam`, commits changed files to `main`, and calls `Publisher.RepackageAndUploadReleaseAsync` to immediately update `Zoid-Gundam-Mod.zip` and `release-info.json` on the `latest` release.
