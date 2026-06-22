namespace YtDlpGui.Models;

public sealed record YtDlpOptionDefinition(
    string Category,
    IReadOnlyList<string> Switches,
    string PrimarySwitch,
    string DisplayName,
    string? ArgumentName,
    string Description,
    IReadOnlyList<string> Choices)
{
    public bool RequiresArgument => !string.IsNullOrWhiteSpace(ArgumentName);
}
