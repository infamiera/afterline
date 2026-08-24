using Afterline.Models;

namespace Afterline.Services;

public sealed class BackgroundProcessor : IAsyncDisposable
{
    private readonly ArchiveService _archive;
    private readonly Func<AppSettings> _settings;
    private readonly CancellationTokenSource _cts = new();
    private Task? _worker;

    public DateTime? LastProcessedAt { get; private set; }
    public event EventHandler? Processed;

    public BackgroundProcessor(ArchiveService archive, Func<AppSettings> settings)
    {
        _archive = archive;
        _settings = settings;
    }

    public void Start()
    {
        _worker ??= Task.Run(WorkerAsync);
    }

    public async Task ProcessNowAsync(CancellationToken cancellationToken = default)
    {
        // Routine processing only needs to discover today's and yesterday's
        // sessions. Older files are indexed on demand when their Archive filter
        // makes them visible, preventing large libraries from being rescanned
        // every minute.
        await _archive.RebuildIndexAsync(
            _settings().ArchiveRoot,
            cancellationToken,
            DateTime.Today.AddDays(-1),
            DateTime.Today);
        LastProcessedAt = DateTime.Now;
        Processed?.Invoke(this, EventArgs.Empty);
    }

    private async Task WorkerAsync()
    {
        while (!_cts.IsCancellationRequested)
        {
            try
            {
                int minutes = Math.Clamp(_settings().ProcessingIntervalMinutes, 1, 60);
                await Task.Delay(TimeSpan.FromMinutes(minutes), _cts.Token);
                await ProcessNowAsync(_cts.Token);
            }
            catch (OperationCanceledException) when (_cts.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                DiagnosticLogger.Error("Background archive processing failed.", ex);
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        if (_worker is not null)
        {
            try { await _worker; } catch { }
        }
        _cts.Dispose();
    }
}
