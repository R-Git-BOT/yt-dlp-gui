# yt-dlp GUI

Lightweight Windows-only WPF GUI for `yt-dlp`.

## Run

```powershell
dotnet run
```

The app defaults to `yt-dlp` on `PATH`. If it is elsewhere, set the `yt-dlp:` field at the bottom of the window to the full path of `yt-dlp.exe`.

## Current Features

- Multiple URL input, one URL per line
- Sequential download queue
- Start and stop controls near the output path field
- Settings save/load/reset
- Command preview
- Output path support through `--paths`
- Log display and log export
- Bundled Japanese option catalog generated from the official `yt-dlp` Usage and Options documentation
- Format selector presets and direct edit mode
- Output filename template helper
- Automatic restore of the previous app state on startup
- Option search, enabled-only filter, and category accordions
- Manual option refresh from `yt-dlp --help`

## Notes

The app loads `Resources/yt-dlp-options.ja.json` at startup so the option list appears immediately. The installed `yt-dlp --help` output is not read automatically at startup; use the refresh button when you want to compare with the local `yt-dlp` version.

See `docs/option-catalog-merge-design.md` for the bundled Japanese option catalog and `yt-dlp --help` merge design.
