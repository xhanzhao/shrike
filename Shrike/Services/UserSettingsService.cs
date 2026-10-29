using System.Text.Json;
using Rhino;

namespace Shrike.Services;

/// <summary>
/// Stores lightweight Shrike user preferences at:
/// Documents\Shrike\settings.json
///
/// These settings are UI preferences only. They do not modify tool.json.
/// </summary>
public sealed class UserSettingsService
{
    private sealed class SettingsData
    {
        public List<string> FavoriteToolIds { get; set; } = new();

        public Dictionary<string, string> ToolColors { get; set; } =
            new(StringComparer.OrdinalIgnoreCase);

        public Dictionary<string, string> CategoryColors { get; set; } =
            new(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The panel is black and white, so tool and category
    /// highlights are shades of gray only.
    /// </summary>
    public static IReadOnlyList<(string Name, string Hex)> ColorPresets { get; } =
        new (string Name, string Hex)[]
        {
            ("Light Gray", "#F0F0F0"),
            ("Gray", "#D9D9D9"),
            ("Mid Gray", "#A6A6A6"),
            ("Dark Gray", "#595959"),
            ("Black", "#1F1F1F")
        };

    private readonly JsonSerializerOptions _jsonOptions =
        new()
        {
            WriteIndented = true
        };

    private readonly string _settingsPath;
    private SettingsData _data;

    public UserSettingsService()
    {
        var shrikeRoot =
            Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.MyDocuments),
                "Shrike");

        Directory.CreateDirectory(
            shrikeRoot);

        _settingsPath =
            Path.Combine(
                shrikeRoot,
                "settings.json");

        _data = Load();
    }

    public bool IsFavorite(
        string toolId)
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
            id =>
                string.Equals(
                    id,
                    toolId,
                    StringComparison.OrdinalIgnoreCase));

        if (favorite)
            _data.FavoriteToolIds.Add(toolId);

        Save();
    }

    public void ToggleFavorite(
        string toolId)
    {
        SetFavorite(
            toolId,
            !IsFavorite(toolId));
    }

    public string? GetToolColor(
        string toolId)
    {
        if (string.IsNullOrWhiteSpace(toolId))
            return null;

        return _data.ToolColors.TryGetValue(
            toolId,
            out var color)
                ? color
                : null;
    }

    public void SetToolColor(
        string toolId,
        string? colorHex)
    {
        if (string.IsNullOrWhiteSpace(toolId))
            return;

        _data.ToolColors.Remove(toolId);

        if (!string.IsNullOrWhiteSpace(colorHex))
        {
            _data.ToolColors[toolId] =
                colorHex.Trim();
        }

        Save();
    }

    public string? GetCategoryColor(
        string categoryKey)
    {
        if (string.IsNullOrWhiteSpace(categoryKey))
            return null;

        return _data.CategoryColors.TryGetValue(
            categoryKey,
            out var color)
                ? color
                : null;
    }

    public void SetCategoryColor(
        string categoryKey,
        string? colorHex)
    {
        if (string.IsNullOrWhiteSpace(categoryKey))
            return;

        _data.CategoryColors.Remove(categoryKey);

        if (!string.IsNullOrWhiteSpace(colorHex))
        {
            _data.CategoryColors[categoryKey] =
                colorHex.Trim();
        }

        Save();
    }

    /// <summary>
    /// Keeps category appearance attached when a category is renamed.
    /// If the rename merges into an existing category, the destination's
    /// own color wins. If it has no color, the source color is inherited.
    /// </summary>
    public void RenameCategoryColor(
        string oldCategory,
        string newCategory,
        bool targetCategoryAlreadyExisted)
    {
        if (string.IsNullOrWhiteSpace(oldCategory)
            ||
            string.IsNullOrWhiteSpace(newCategory))
        {
            return;
        }

        var hasOldColor =
            _data.CategoryColors.TryGetValue(
                oldCategory,
                out var oldColor);

        var hasTargetColor =
            targetCategoryAlreadyExisted
            &&
            _data.CategoryColors.TryGetValue(
                newCategory,
                out _);

        _data.CategoryColors.Remove(
            oldCategory);

        if (hasOldColor
            &&
            !hasTargetColor)
        {
            _data.CategoryColors[newCategory] =
                oldColor!;
        }

        Save();
    }

    private SettingsData Load()
    {
        if (!File.Exists(_settingsPath))
            return new SettingsData();

        try
        {
            var json =
                File.ReadAllText(
                    _settingsPath);

            var data =
                JsonSerializer.Deserialize<SettingsData>(
                    json,
                    _jsonOptions)
                ??
                new SettingsData();

            // JsonSerializer creates normal dictionaries when reading.
            // Rebuild them so lookups stay case-insensitive.
            data.ToolColors =
                new Dictionary<string, string>(
                    data.ToolColors
                    ??
                    new Dictionary<string, string>(),
                    StringComparer.OrdinalIgnoreCase);

            data.CategoryColors =
                new Dictionary<string, string>(
                    data.CategoryColors
                    ??
                    new Dictionary<string, string>(),
                    StringComparer.OrdinalIgnoreCase);

            data.FavoriteToolIds ??= new List<string>();

            return data;
        }
        catch (Exception ex)
        {
            RhinoApp.WriteLine(
                "Shrike: failed to read settings.json.");

            RhinoApp.WriteLine(
                ex.Message);

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

            RhinoApp.WriteLine(
                ex.Message);
        }
    }
}
