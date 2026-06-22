namespace YtDlpGui.Models;

public sealed record FormatPresetOption(
    string Name,
    string Description,
    string FormatSelector,
    string FormatSort,
    string Resolution = "指定なし",
    string Extension = "指定なし",
    string Size = "指定なし",
    string MergeOutputFormat = "",
    bool FormatSortForce = false,
    bool FormatSortReset = false,
    bool VideoMultistreams = false,
    bool AudioMultistreams = false);
