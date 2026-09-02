using Rhino;
using Rhino.Commands;

namespace Shrike.Services;

/// <summary>
/// Records completed top-level Rhino commands and converts them into
/// a simple Shrike Rhino Macro.
///
/// Important:
/// This records command names only. It does not record mouse points,
/// object picks, command-line option values, or dialog input.
/// </summary>
public sealed class MacroRecorderService : IDisposable
{
    private readonly List<string>
        _recordedCommands =
            new();

    private int
        _commandDepth;

    private string
        _topLevelCommandName =
            string.Empty;

    private bool
        _disposed;

    public bool IsRecording
    {
        get;
        private set;
    }

    public int CommandCount =>
        _recordedCommands.Count;

    public void Start()
    {
        ThrowIfDisposed();

        if (IsRecording)
            return;

        _recordedCommands.Clear();

        _commandDepth = 0;

        _topLevelCommandName =
            string.Empty;

        Command.BeginCommand +=
            OnBeginCommand;

        Command.EndCommand +=
            OnEndCommand;

        IsRecording = true;

        RhinoApp.WriteLine(
            "Shrike Macro Recorder: recording started.");
    }

    public string Stop()
    {
        ThrowIfDisposed();

        if (!IsRecording)
            return string.Empty;

        Unsubscribe();

        IsRecording = false;

        _commandDepth = 0;

        _topLevelCommandName =
            string.Empty;

        RhinoApp.WriteLine(
            $"Shrike Macro Recorder: stopped. " +
            $"{_recordedCommands.Count} command(s) recorded.");

        return string.Join(
            Environment.NewLine,
            _recordedCommands);
    }

    public void Cancel()
    {
        if (_disposed)
            return;

        if (IsRecording)
        {
            Unsubscribe();

            IsRecording = false;
        }

        _recordedCommands.Clear();

        _commandDepth = 0;

        _topLevelCommandName =
            string.Empty;
    }

    private void OnBeginCommand(
        object? sender,
        CommandEventArgs e)
    {
        if (!IsRecording)
            return;

        if (_commandDepth == 0)
        {
            _topLevelCommandName =
                (e.CommandEnglishName
                 ?? string.Empty)
                .Trim();
        }

        _commandDepth++;
    }

    private void OnEndCommand(
        object? sender,
        CommandEventArgs e)
    {
        if (!IsRecording)
            return;

        if (_commandDepth > 0)
        {
            _commandDepth--;
        }

        // Only record the top-level command.
        // This prevents nested/internal commands from becoming
        // separate Shrike Macro lines.
        if (_commandDepth != 0)
            return;

        var commandName =
            _topLevelCommandName;

        _topLevelCommandName =
            string.Empty;

        if (e.CommandResult !=
            Result.Success)
        {
            return;
        }

        if (ShouldIgnore(
                commandName))
        {
            return;
        }

        _recordedCommands.Add(
            "_" + commandName);
    }

    private static bool ShouldIgnore(
        string commandName)
    {
        if (string.IsNullOrWhiteSpace(
                commandName))
        {
            return true;
        }

        // Do not accidentally record Shrike's own commands.
        if (commandName.StartsWith(
                "Shrike",
                StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Undo / Redo are session-history actions rather than
        // useful reusable macro steps for this recorder.
        return string.Equals(
                   commandName,
                   "Undo",
                   StringComparison.OrdinalIgnoreCase)
               ||
               string.Equals(
                   commandName,
                   "Redo",
                   StringComparison.OrdinalIgnoreCase);
    }

    private void Unsubscribe()
    {
        Command.BeginCommand -=
            OnBeginCommand;

        Command.EndCommand -=
            OnEndCommand;
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(
                nameof(MacroRecorderService));
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        Cancel();

        _disposed = true;
    }
}
