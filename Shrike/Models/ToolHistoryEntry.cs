using System.Text.Json.Serialization;

namespace Shrike.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ToolHistoryReason
{
    Created,
    BeforeReplaceFromFile,
    BeforeRollback
}

public sealed class ToolHistoryEntry
{
    public string FileName { get; set; } = string.Empty;

    public DateTime CreatedUtc { get; set; }

    public ToolHistoryReason Reason { get; set; }

    public int LineCount { get; set; }

    public string Hash { get; set; } = string.Empty;

    public string Note { get; set; } = string.Empty;

    [JsonIgnore]
    public string ReasonText =>
        Reason switch
        {
            ToolHistoryReason.Created =>
                "Initial version",
            ToolHistoryReason.BeforeReplaceFromFile =>
                "Before replace from file",
            ToolHistoryReason.BeforeRollback =>
                "Before rollback",
            _ =>
                Reason.ToString()
        };
}
