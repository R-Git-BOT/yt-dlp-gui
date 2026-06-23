# yt-dlp GUI

`yt-dlp GUI` is a lightweight Windows app for downloading videos with [`yt-dlp`](https://github.com/yt-dlp/yt-dlp) through a graphical interface.

## Download

Download the latest Windows build from the Releases page.

[Download from Releases](https://github.com/R-Git-BOT/yt-dlp-gui/releases)

1. Open the latest release.
2. Download `yt-dlp-gui-*-win-x64.zip` from Assets.
3. Extract the zip file.
4. Run `YtDlpGui.exe`.

The app uses `yt-dlp` from `PATH` by default. If `yt-dlp.exe` is in another location, enter its full path in the `yt-dlp:` field at the bottom of the window.

## Basic Usage

1. Paste one or more video URLs into the URL box.
2. Set the save folder in the output path field.
3. Open option categories and enable only the options you need.
4. Use the Format section when you want to choose quality, extension, size, or a custom format selector.
5. Use the Output filename section when you want to customize the saved file name.
6. Press `DL開始` to start downloading.
7. Press `停止` to cancel the current download.

Multiple URLs can be entered at once. The app queues them and downloads them sequentially.

## Useful Controls

- `一覧形式に整形`: Formats pasted URLs into a clean list.
- `キューをクリア`: Clears the current download queue.
- `設定を保存`: Saves the current option settings to a file.
- `設定を読み込み`: Loads option settings from a file.
- `リセット`: Resets the current option settings.
- `アップデート確認`: Checks version information.
- `バージョン情報`: Shows the app version and `yt-dlp` version.
- `ヘルプ`: Opens the official `yt-dlp` options documentation.

The app also restores the previous state automatically when it starts.

## Notes

- This app is Windows-only.
- The option list is loaded from a bundled Japanese option catalog, so startup is quick.
- `yt-dlp --help` is not executed automatically at startup.
- Some advanced options may require knowledge of `yt-dlp` itself.

## Development

To run from source:

```powershell
dotnet run
```

To create a release, push a version tag:

```powershell
git tag v1.0.0
git push origin v1.0.0
```

GitHub Actions will build a Windows x64 package and attach it to the GitHub Release.

## Tech Stack

- C#
- .NET 7
- WPF
- XAML
- GitHub Actions
- `yt-dlp`
