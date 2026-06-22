using System.IO;
using System.Text.Json;
using YtDlpGui.Models;

namespace YtDlpGui.Services;

public sealed class SettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public string AutoSettingsPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "YtDlpGui",
        "autosave-settings.json");

    public async Task SaveAsync(string path, AppSettings settings)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, settings, JsonOptions).ConfigureAwait(false);
    }

    public async Task<AppSettings> LoadAsync(string path)
    {
        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<AppSettings>(stream, JsonOptions).ConfigureAwait(false) ?? new AppSettings();
    }

    public Task SaveAutoAsync(AppSettings settings)
    {
        return SaveAsync(AutoSettingsPath, settings);
    }

    public async Task<AppSettings?> TryLoadAutoAsync()
    {
        if (!File.Exists(AutoSettingsPath))
        {
            return null;
        }

        return await LoadAsync(AutoSettingsPath).ConfigureAwait(false);
    }
}
