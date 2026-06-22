using System.Text.Json;
using YtDlpGui.Models;

namespace YtDlpGui.Services;

public sealed class OptionMergeService
{
    public OptionMergeResult Merge(YtDlpOptionCatalog catalog, IReadOnlyList<YtDlpOptionDefinition> helpOptions)
    {
        var categoryMap = catalog.Categories.ToDictionary(category => category.Id, StringComparer.OrdinalIgnoreCase);
        var helpLookup = BuildHelpLookup(helpOptions);
        var usedHelp = new HashSet<YtDlpOptionDefinition>();
        var merged = new List<YtDlpOptionDefinition>();
        var matchedCount = 0;
        var catalogOnlyCount = 0;

        foreach (var catalogOption in catalog.Options)
        {
            var helpOption = FindHelpOption(catalogOption, helpLookup);
            if (helpOption is null)
            {
                catalogOnlyCount++;
            }
            else
            {
                matchedCount++;
                usedHelp.Add(helpOption);
            }

            merged.Add(BuildCatalogBackedOption(catalogOption, categoryMap, helpOption));
        }

        foreach (var helpOnly in helpOptions.Where(option => !usedHelp.Contains(option)))
        {
            merged.Add(BuildHelpOnlyOption(helpOnly));
        }

        var helpOnlyCount = helpOptions.Count - usedHelp.Count;
        return new OptionMergeResult(
            merged,
            catalog.Options.Count,
            helpOptions.Count,
            matchedCount,
            helpOnlyCount,
            catalogOnlyCount);
    }

    public OptionMergeResult FromCatalogOnly(YtDlpOptionCatalog catalog)
    {
        return Merge(catalog, Array.Empty<YtDlpOptionDefinition>());
    }

    private static Dictionary<string, YtDlpOptionDefinition> BuildHelpLookup(IEnumerable<YtDlpOptionDefinition> helpOptions)
    {
        var lookup = new Dictionary<string, YtDlpOptionDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (var option in helpOptions)
        {
            Add(option.PrimarySwitch, option);
            foreach (var switchName in option.Switches)
            {
                Add(switchName, option);
            }
        }

        return lookup;

        void Add(string key, YtDlpOptionDefinition option)
        {
            if (!string.IsNullOrWhiteSpace(key) && !lookup.ContainsKey(key))
            {
                lookup.Add(key, option);
            }
        }
    }

    private static YtDlpOptionDefinition? FindHelpOption(CatalogOption catalogOption, IReadOnlyDictionary<string, YtDlpOptionDefinition> helpLookup)
    {
        if (helpLookup.TryGetValue(catalogOption.PrimarySwitch, out var byPrimary))
        {
            return byPrimary;
        }

        foreach (var alias in catalogOption.Aliases)
        {
            if (helpLookup.TryGetValue(alias, out var byAlias))
            {
                return byAlias;
            }
        }

        return null;
    }

    private static YtDlpOptionDefinition BuildCatalogBackedOption(
        CatalogOption catalogOption,
        IReadOnlyDictionary<string, CatalogCategory> categoryMap,
        YtDlpOptionDefinition? helpOption)
    {
        var switches = catalogOption.Aliases
            .Concat(helpOption?.Switches ?? Array.Empty<string>())
            .Prepend(catalogOption.PrimarySwitch)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var catalogArgument = catalogOption.Argument.HasArgument
            ? string.IsNullOrWhiteSpace(catalogOption.Argument.Name) ? "VALUE" : catalogOption.Argument.Name
            : null;
        var argumentName = catalogOption.Merge.PreferCatalogArgument
            ? catalogArgument
            : helpOption?.ArgumentName ?? catalogArgument;
        var choices = ReadChoices(catalogOption).ToList();
        if (choices.Count == 0 && helpOption is not null)
        {
            choices.AddRange(helpOption.Choices);
        }

        var category = categoryMap.TryGetValue(catalogOption.CategoryId, out var catalogCategory)
            ? catalogCategory.Name
            : catalogOption.CategoryId;

        return new YtDlpOptionDefinition(
            category,
            switches,
            catalogOption.PrimarySwitch,
            catalogOption.DisplayName,
            argumentName,
            catalogOption.Description,
            choices);
    }

    private static YtDlpOptionDefinition BuildHelpOnlyOption(YtDlpOptionDefinition helpOption)
    {
        var category = string.IsNullOrWhiteSpace(helpOption.Category) ? "未分類・新規" : helpOption.Category;
        return helpOption with
        {
            Category = category,
            DisplayName = string.IsNullOrWhiteSpace(helpOption.DisplayName)
                ? Humanize(helpOption.PrimarySwitch)
                : helpOption.DisplayName
        };
    }

    private static IEnumerable<string> ReadChoices(CatalogOption option)
    {
        foreach (var choice in option.Choices)
        {
            if (choice.ValueKind == JsonValueKind.String)
            {
                yield return choice.GetString() ?? "";
                continue;
            }

            if (choice.ValueKind == JsonValueKind.Object &&
                choice.TryGetProperty("value", out var value) &&
                value.ValueKind == JsonValueKind.String)
            {
                yield return value.GetString() ?? "";
            }
        }
    }

    private static string Humanize(string primarySwitch)
    {
        return primarySwitch.TrimStart('-').Replace('-', ' ');
    }
}
