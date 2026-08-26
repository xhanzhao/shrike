using System.Text.Json;
using Rhino;

namespace Shrike.Services;

/// <summary>
/// Stores lightweight Shrike user preferences.
///
/// Current setting:
/// - Favorite tool ids
///
/// Saved at:
/// Documents\Shrike\settings.json
/// </summary>
public sealed class UserSettingsService
{
    private sealed class SettingsData
    {
        public List<string> FavoriteToolIds { get; set; } = new();
    }

    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _settingsPath;

    private SettingsData _data;

    public UserSettingsService()
    {
        var ShrikeRoot = Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.MyDocuments),
            "Shrike");

        Directory.CreateDirectory(ShrikeRoot);

        _settingsPath = Path.Combine(
            ShrikeRoot,
            "settings.json");

        _data = Load();
    }

    public bool IsFavorite(string toolId)
    {
        if (string.IsNullOrWhiteSpace(toolId))
            return false;

        return _data.FavoriteToolIds.Contains(
            toolId,
            StringComparer.OrdinalIgnoreCase);
    }

    public void SetFavorite(
        string toolId,
        bool favorite)
    {
        if (string.IsNullOrWhiteSpace(toolId))
            return;

        _data.FavoriteToolIds.RemoveAll(
            id => string.Equals(
                id,
                toolId,
                StringComparison.OrdinalIgnoreCase));

        if (favorite)
            _data.FavoriteToolIds.Add(toolId);

        Save();
    }

    public void ToggleFavorite(string toolId)
    {
        SetFavorite(
            toolId,
            !IsFavorite(toolId));
    }

    private SettingsData Load()
    {
        if (!File.Exists(_settingsPath))
            return new SettingsData();

        try
        {
            var json = File.ReadAllText(
                _settingsPath);

            var data =
                JsonSerializer.Deserialize<SettingsData>(
                    json,
                    _jsonOptions);

            return data ?? new SettingsData();
        }
        catch (Exception ex)
        {
            RhinoApp.WriteLine(
                "Shrike: failed to read settings.json.");

            RhinoApp.WriteLine(ex.Message);

            return new SettingsData();
        }
    }

    private void Save()
    {
        try
        {
            var json =
                JsonSerializer.Serialize(
                    _data,
                    _jsonOptions);

            File.WriteAllText(
                _settingsPath,
                json);
        }
        catch (Exception ex)
        {
            RhinoApp.WriteLine(
                "Shrike: failed to save settings.json.");

            RhinoApp.WriteLine(ex.Message);
        }
    }
}
