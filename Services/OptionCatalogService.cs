using System.IO;
using System.Text.Json;
using YtDlpGui.Models;

namespace YtDlpGui.Services;

public sealed class OptionCatalogService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public async Task<YtDlpOptionCatalog> LoadAsync(CancellationToken cancellationToken)
    {
        var path = ResolveCatalogPath();
        await using var stream = File.OpenRead(path);
        var catalog = await JsonSerializer.DeserializeAsync<YtDlpOptionCatalog>(stream, JsonOptions, cancellationToken);
        if (catalog is null)
        {
            throw new InvalidOperationException("同梱オプションカタログを読み込めませんでした");
        }

        Validate(catalog, path);
        return catalog;
    }

    private static string ResolveCatalogPath()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "Resources", "yt-dlp-options.ja.json"),
            Path.Combine(AppContext.BaseDirectory, "Resources", "yt-dlp-options.ja.example.json"),
            Path.Combine(Environment.CurrentDirectory, "Resources", "yt-dlp-options.ja.json"),
            Path.Combine(Environment.CurrentDirectory, "Resources", "yt-dlp-options.ja.example.json")
        };

        var path = candidates.FirstOrDefault(File.Exists);
        return path ?? throw new FileNotFoundException("同梱オプションカタログが見つかりません", candidates[0]);
    }

    private static void Validate(YtDlpOptionCatalog catalog, string path)
    {
        if (catalog.SchemaVersion < 1)
        {
            throw new InvalidOperationException($"{path} の schemaVersion が不正です");
        }

        if (catalog.Options.Count == 0)
        {
            throw new InvalidOperationException($"{path} にオプションが定義されていません");
        }

        var categories = catalog.Categories.Select(category => category.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var option in catalog.Options)
        {
            if (string.IsNullOrWhiteSpace(option.PrimarySwitch))
            {
                throw new InvalidOperationException($"{path} に primarySwitch が空のオプションがあります");
            }

            if (!categories.Contains(option.CategoryId))
            {
                throw new InvalidOperationException($"{path} の {option.PrimarySwitch} が未定義カテゴリ {option.CategoryId} を参照しています");
            }
        }
    }
}
