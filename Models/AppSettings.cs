namespace YtDlpGui.Models;

public sealed class AppSettings
{
    public string YtDlpPath { get; set; } = "yt-dlp";
    public string OutputPath { get; set; } = "";
    public string UrlText { get; set; } = "";
    public string SearchText { get; set; } = "";
    public string ActiveSection { get; set; } = "ダウンロード";
    public bool ShowEnabledOnly { get; set; }
    public SavedFormatSettings Format { get; set; } = new();
    public SavedOutputTemplateSettings OutputTemplate { get; set; } = new();
    public Dictionary<string, SavedOptionState> Options { get; set; } = new();
    public Dictionary<string, bool> ExpandedCategories { get; set; } = new();
}

public sealed class SavedOptionState
{
    public bool IsSelected { get; set; }
    public string Value { get; set; } = "";
}

public sealed class SavedFormatSettings
{
    public bool IsEnabled { get; set; }
    public bool IsDirectEdit { get; set; }
    public string PresetName { get; set; } = "";
    public string FormatSelector { get; set; } = "";
    public string FormatSort { get; set; } = "";
    public string Resolution { get; set; } = "指定なし";
    public string Extension { get; set; } = "指定なし";
    public string Size { get; set; } = "指定なし";
    public bool FormatSortForce { get; set; }
    public bool FormatSortReset { get; set; }
    public bool VideoMultistreams { get; set; }
    public bool AudioMultistreams { get; set; }
    public string MergeOutputFormat { get; set; } = "";
}

public sealed class SavedOutputTemplateSettings
{
    public bool IsEnabled { get; set; }
    public string Template { get; set; } = "";
}
