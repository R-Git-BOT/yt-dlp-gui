using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using YtDlpGui.Models;

namespace YtDlpGui.Services;

public sealed class YtDlpHelpParser
{
    private static readonly Regex OptionLineRegex = new(@"^\s{2,}(?<options>(?:-[A-Za-z0-9?],?\s*)?(?:--[A-Za-z0-9][A-Za-z0-9\-]*(?:[=\s][A-Z0-9_<>\[\]\-|,/.:]+)?(?:,\s*)?)+)(?<description>.*)$", RegexOptions.Compiled);
    private static readonly Regex SectionRegex = new(@"^\s{0,2}(?<name>[A-Z][A-Za-z0-9 /,()&+\-]+):\s*$", RegexOptions.Compiled);
    private static readonly Regex LongSwitchRegex = new(@"--[A-Za-z0-9][A-Za-z0-9\-]*", RegexOptions.Compiled);
    private static readonly Regex ArgumentRegex = new(@"(?<switch>--[A-Za-z0-9][A-Za-z0-9\-]*)(?:[=\s](?<arg>[A-Z0-9_<>\[\]\-|,/.:]+))?", RegexOptions.Compiled);

    public async Task<(IReadOnlyList<YtDlpOptionDefinition> Options, string Status)> LoadOptionsAsync(string ytDlpPath, CancellationToken cancellationToken)
    {
        var helpResult = await LoadHelpOptionsAsync(ytDlpPath, cancellationToken);
        if (helpResult.Success)
        {
            return (helpResult.Options, helpResult.Status);
        }

        return (FallbackOptions(), helpResult.Status);
    }

    public async Task<YtDlpHelpLoadResult> LoadHelpOptionsAsync(string ytDlpPath, CancellationToken cancellationToken)
    {
        try
        {
            var helpText = await ReadHelpAsync(ytDlpPath, cancellationToken);
            var parsed = Parse(helpText).ToList();
            if (parsed.Count > 0)
            {
                return new YtDlpHelpLoadResult(parsed, true, $"{parsed.Count} 個のオプションを yt-dlp --help から読み込みました");
            }

            return new YtDlpHelpLoadResult(Array.Empty<YtDlpOptionDefinition>(), false, "yt-dlp --help の解析結果が空でした");
        }
        catch (Exception ex)
        {
            return new YtDlpHelpLoadResult(Array.Empty<YtDlpOptionDefinition>(), false, $"yt-dlp --help を読み込めませんでした: {ex.Message}");
        }
    }

    public IEnumerable<YtDlpOptionDefinition> Parse(string helpText)
    {
        var category = "General";
        YtDlpOptionDefinition? current = null;

        foreach (var rawLine in helpText.Replace("\r\n", "\n").Split('\n'))
        {
            var line = rawLine.TrimEnd();
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var section = SectionRegex.Match(line);
            if (section.Success && !line.TrimStart().StartsWith("-"))
            {
                category = LocalizeCategory(section.Groups["name"].Value.Trim());
                current = null;
                continue;
            }

            var option = OptionLineRegex.Match(line);
            if (option.Success)
            {
                current = BuildOption(category, option.Groups["options"].Value.Trim(), option.Groups["description"].Value.Trim());
                yield return current;
                continue;
            }

            if (current is not null && char.IsWhiteSpace(rawLine.FirstOrDefault()))
            {
                // Continuation descriptions are intentionally ignored for command generation.
            }
        }
    }

    private static async Task<string> ReadHelpAsync(string ytDlpPath, CancellationToken cancellationToken)
    {
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = string.IsNullOrWhiteSpace(ytDlpPath) ? "yt-dlp" : ytDlpPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        process.StartInfo.ArgumentList.Add("--help");

        process.Start();
        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        var output = await outputTask;
        var error = await errorTask;
        if (process.ExitCode != 0 && string.IsNullOrWhiteSpace(output))
        {
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(error) ? $"exit code {process.ExitCode}" : error.Trim());
        }

        return string.IsNullOrWhiteSpace(output) ? error : output;
    }

    private static YtDlpOptionDefinition BuildOption(string category, string optionText, string description)
    {
        var switches = LongSwitchRegex.Matches(optionText).Select(m => m.Value).Distinct().ToList();
        var primary = switches.FirstOrDefault() ?? optionText.Split(' ', ',').First();
        var arg = DetectArgument(optionText, primary);
        var display = Humanize(primary);
        var choices = KnownChoices(primary, arg).ToList();

        return new YtDlpOptionDefinition(category, switches, primary, display, arg, description, choices);
    }

    private static string? DetectArgument(string optionText, string primarySwitch)
    {
        foreach (Match match in ArgumentRegex.Matches(optionText))
        {
            if (match.Groups["switch"].Value != primarySwitch)
            {
                continue;
            }

            var arg = match.Groups["arg"].Value.Trim();
            if (!string.IsNullOrWhiteSpace(arg))
            {
                return arg.Trim('[', ']');
            }
        }

        return null;
    }

    private static IEnumerable<string> KnownChoices(string primarySwitch, string? arg)
    {
        if (arg is null)
        {
            yield break;
        }

        var map = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["--merge-output-format"] = new[] { "", "mp4", "mkv", "webm", "mov", "flv" },
            ["--audio-format"] = new[] { "", "best", "aac", "alac", "flac", "m4a", "mp3", "opus", "vorbis", "wav" },
            ["--audio-quality"] = new[] { "", "0", "1", "2", "3", "4", "5", "128K", "192K", "256K", "320K" },
            ["--recode-video"] = new[] { "", "mp4", "mkv", "webm", "avi", "flv", "mov" },
            ["--remux-video"] = new[] { "", "mp4", "mkv", "webm", "avi", "flv", "mov" },
            ["--sub-langs"] = new[] { "", "ja", "en", "all", "ja,en", "live_chat" },
            ["--convert-subs"] = new[] { "", "srt", "vtt", "ass", "lrc" },
            ["--cookies-from-browser"] = new[] { "", "firefox", "chrome", "chromium", "edge", "opera", "brave", "vivaldi", "safari" },
            ["--compat-options"] = new[] { "", "youtube-dl", "2021", "2022", "no-youtube-unavailable-videos" }
        };

        if (map.TryGetValue(primarySwitch, out var values))
        {
            foreach (var value in values)
            {
                yield return value;
            }
        }
    }

    private static string Humanize(string primarySwitch)
    {
        return primarySwitch.TrimStart('-').Replace('-', ' ');
    }

    private static string LocalizeCategory(string category)
    {
        var normalized = category.Replace("Options", "", StringComparison.OrdinalIgnoreCase).Trim();
        return normalized.ToLowerInvariant() switch
        {
            "general" => "一般",
            "network" => "ネットワーク",
            "geo restriction" => "地域制限",
            "video selection" => "動画選択",
            "download" => "ダウンロード",
            "filesystem" => "ファイル名・出力",
            "thumbnail" => "サムネイル",
            "internet shortcut" => "ショートカット",
            "verbosity and simulation" => "デバッグ・上級者向け",
            "workarounds" => "回避策",
            "video format" => "形式・動画",
            "subtitle" => "字幕",
            "authentication" => "認証",
            "post-processing" => "後処理",
            "sponsorblock" => "SponsorBlock",
            "extractor" => "抽出",
            _ => normalized
        };
    }

    private static IReadOnlyList<YtDlpOptionDefinition> FallbackOptions()
    {
        return new[]
        {
            new YtDlpOptionDefinition("形式・動画", new[] {"--format"}, "--format", "特定のフォーマットを指定", "FORMAT", "Video format code, such as bestvideo+bestaudio/best", Array.Empty<string>()),
            new YtDlpOptionDefinition("形式・動画", new[] {"--merge-output-format"}, "--merge-output-format", "結合後の形式", "FORMAT", "Container used after merging video and audio", new[] {"", "mp4", "mkv", "webm"}),
            new YtDlpOptionDefinition("形式・動画", new[] {"--remux-video"}, "--remux-video", "動画をリマックス", "FORMAT", "Remux the video into another container", new[] {"", "mp4", "mkv", "webm", "mov"}),
            new YtDlpOptionDefinition("音声", new[] {"--extract-audio"}, "--extract-audio", "音声のみ抽出", null, "Convert video files to audio-only files", Array.Empty<string>()),
            new YtDlpOptionDefinition("音声", new[] {"--audio-format"}, "--audio-format", "音声形式", "FORMAT", "Audio format", new[] {"", "best", "m4a", "mp3", "opus", "wav", "flac"}),
            new YtDlpOptionDefinition("字幕", new[] {"--write-subs"}, "--write-subs", "字幕を書き出す", null, "Write subtitle file", Array.Empty<string>()),
            new YtDlpOptionDefinition("字幕", new[] {"--sub-langs"}, "--sub-langs", "字幕言語", "LANGS", "Languages of subtitles to download", new[] {"", "ja", "en", "all", "ja,en"}),
            new YtDlpOptionDefinition("プレイリスト", new[] {"--yes-playlist"}, "--yes-playlist", "プレイリストを許可", null, "Download playlist when URL refers to video and playlist", Array.Empty<string>()),
            new YtDlpOptionDefinition("ネットワーク", new[] {"--proxy"}, "--proxy", "プロキシ", "URL", "Use the specified HTTP/HTTPS/SOCKS proxy", Array.Empty<string>()),
            new YtDlpOptionDefinition("認証", new[] {"--cookies-from-browser"}, "--cookies-from-browser", "ブラウザCookieを使用", "BROWSER", "Load cookies from browser", new[] {"", "firefox", "chrome", "edge", "brave", "vivaldi"}),
            new YtDlpOptionDefinition("後処理", new[] {"--embed-thumbnail"}, "--embed-thumbnail", "サムネイルを埋め込む", null, "Embed thumbnail in the video as cover art", Array.Empty<string>()),
            new YtDlpOptionDefinition("ファイル名・出力", new[] {"--output"}, "--output", "出力テンプレート", "TEMPLATE", "Output filename template", Array.Empty<string>()),
            new YtDlpOptionDefinition("デバッグ・上級者向け", new[] {"--verbose"}, "--verbose", "詳細ログ", null, "Print various debugging information", Array.Empty<string>())
        };
    }
}
