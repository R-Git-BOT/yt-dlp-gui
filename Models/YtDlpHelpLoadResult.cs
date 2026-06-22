namespace YtDlpGui.Models;

public sealed record YtDlpHelpLoadResult(
    IReadOnlyList<YtDlpOptionDefinition> Options,
    bool Success,
    string Status);
