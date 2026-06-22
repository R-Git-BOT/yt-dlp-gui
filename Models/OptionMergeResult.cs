namespace YtDlpGui.Models;

public sealed record OptionMergeResult(
    IReadOnlyList<YtDlpOptionDefinition> Options,
    int CatalogCount,
    int HelpCount,
    int MatchedCount,
    int HelpOnlyCount,
    int CatalogOnlyCount)
{
    public string Summary =>
        $"同梱カタログ {CatalogCount} 件、help {HelpCount} 件、照合 {MatchedCount} 件、新規 {HelpOnlyCount} 件、未検出 {CatalogOnlyCount} 件";
}
