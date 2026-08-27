using System.Text.Json.Serialization;

namespace Shrike.Models;

public sealed class ToolDefinition
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Category { get; set; } = "Uncategorized";

    public string Type { get; set; } = "python3";

    public string Entry { get; set; } = "main.py";

    public string Command { get; set; } = string.Empty;

    public string Icon { get; set; } = string.Empty;

    public string Tooltip { get; set; } = string.Empty;

    [JsonIgnore]
    public string ToolDirectory { get; set; } = string.Empty;

    [JsonIgnore]
    public string ManifestPath { get; set; } = string.Empty;
}
