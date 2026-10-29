using System.Threading;

namespace Shrike.Services;

/// <summary>
/// Watches the user's Shrike tool library and emits one debounced
/// notification after file-system activity settles.
/// </summary>
public sealed class ToolWatchService : IDisposable
{
    private const int DebounceMilliseconds = 400;

    private readonly object
        _syncRoot =
            new();

    private FileSystemWatcher?
        _watcher;

    private Timer?
        _debounceTimer;

    private bool
        _disposed;

    public event EventHandler?
        ToolsChanged;

    public void Start()
    {
        lock (_syncRoot)
        {
            ThrowIfDisposed();

            if (_watcher is not null)
                return;

            var root =
                ToolPathService.GetUserToolsRoot();

            Directory.CreateDirectory(
                root);

            var watcher =
                new FileSystemWatcher(
                    root)
                {
                    IncludeSubdirectories = true,

                    NotifyFilter =
                        NotifyFilters.FileName
                        |
                        NotifyFilters.DirectoryName
                        |
                        NotifyFilters.LastWrite
                        |
                        NotifyFilters.CreationTime
                        |
                        NotifyFilters.Size,

                    Filter = "*",

                    EnableRaisingEvents = false
                };

            watcher.Created +=
                OnFileSystemChanged;

            watcher.Changed +=
                OnFileSystemChanged;

            watcher.Deleted +=
                OnFileSystemChanged;

            watcher.Renamed +=
                OnFileSystemRenamed;

            watcher.Error +=
                OnWatcherError;

            _watcher =
                watcher;

            watcher.EnableRaisingEvents =
                true;
        }
    }

    private void OnFileSystemChanged(
        object sender,
        FileSystemEventArgs e)
    {
        // Writing version history must not reload the tool list.
        if (ToolHistoryService.IsInternalPath(
                e.FullPath))
        {
            return;
        }

        ScheduleNotification();
    }

    private void OnFileSystemRenamed(
        object sender,
        RenamedEventArgs e)
    {
        if (ToolHistoryService.IsInternalPath(
                e.FullPath)
            &&
            ToolHistoryService.IsInternalPath(
                e.OldFullPath))
        {
            return;
        }

        ScheduleNotification();
    }

    private void OnWatcherError(
        object sender,
        ErrorEventArgs e)
    {
        // If Windows reports a watcher problem (for example buffer
        // overflow), trigger one refresh. Manual Refresh still remains
        // available as a fallback.
        ScheduleNotification();
    }

    private void ScheduleNotification()
    {
        lock (_syncRoot)
        {
            if (_disposed)
                return;

            _debounceTimer ??=
                new Timer(
                    OnDebounceElapsed,
                    null,
                    Timeout.Infinite,
                    Timeout.Infinite);

            _debounceTimer.Change(
                DebounceMilliseconds,
                Timeout.Infinite);
        }
    }

    private void OnDebounceElapsed(
        object? state)
    {
        EventHandler?
            handler;

        lock (_syncRoot)
        {
            if (_disposed)
                return;

            handler =
                ToolsChanged;
        }

        handler?.Invoke(
            this,
            EventArgs.Empty);
    }

    public void Dispose()
    {
        lock (_syncRoot)
        {
            if (_disposed)
                return;

            _disposed =
                true;

            if (_watcher is not null)
            {
                _watcher.EnableRaisingEvents =
                    false;

                _watcher.Created -=
                    OnFileSystemChanged;

                _watcher.Changed -=
                    OnFileSystemChanged;

                _watcher.Deleted -=
                    OnFileSystemChanged;

                _watcher.Renamed -=
                    OnFileSystemRenamed;

                _watcher.Error -=
                    OnWatcherError;

                _watcher.Dispose();

                _watcher =
                    null;
            }

            _debounceTimer?.Dispose();

            _debounceTimer =
                null;

            ToolsChanged =
                null;
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(
                nameof(ToolWatchService));
        }
    }
}
