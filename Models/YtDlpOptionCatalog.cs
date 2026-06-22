using System.Text.Json;
using System.Text.Json.Serialization;

namespace YtDlpGui.Models;

public sealed class YtDlpOptionCatalog
{
    public int SchemaVersion { get; set; }
    public string CatalogLanguage { get; set; } = "ja-JP";
    public string CatalogRevision { get; set; } = "";
    public CatalogSource Source { get; set; } = new();
    public List<CatalogCategory> Categories { get; set; } = new();
    public List<CatalogOption> Options { get; set; } = new();
}

public sealed class CatalogSource
{
    public string YtDlpVersion { get; set; } = "";
    public string HelpCapturedAt { get; set; } = "";
    public string Notes { get; set; } = "";
}

public sealed class CatalogCategory
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public int Order { get; set; }
    public string Description { get; set; } = "";
}

public sealed class CatalogOption
{
    public string PrimarySwitch { get; set; } = "";
    public List<string> Aliases { get; set; } = new();
    public string CategoryId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Description { get; set; } = "";
    public CatalogArgument Argument { get; set; } = new();
    public List<JsonElement> Choices { get; set; } = new();
    public List<string> Tags { get; set; } = new();
    public CatalogUi Ui { get; set; } = new();
    public CatalogMerge Merge { get; set; } = new();
    public List<string> Replaces { get; set; } = new();
}

public sealed class CatalogArgument
{
    public string Name { get; set; } = "";
    public string Kind { get; set; } = "none";
    public bool Required { get; set; }
    public string Placeholder { get; set; } = "";

    [JsonIgnore]
    public bool HasArgument => !string.Equals(Kind, "none", StringComparison.OrdinalIgnoreCase);
}

public sealed class CatalogUi
{
    public string Control { get; set; } = "checkbox";
    public bool IsAdvanced { get; set; }
    public string Group { get; set; } = "";
}

public sealed class CatalogMerge
{
    public bool KeepWhenMissingFromHelp { get; set; } = true;
    public bool PreferCatalogArgument { get; set; }
}
