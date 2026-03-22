using System.Threading.Channels;
using AINews.Api.Services;

namespace AINews.Api.BackgroundServices;

public class ScanBackgroundService(
    IServiceScopeFactory scopeFactory,
    ILogger<ScanBackgroundService> logger) : BackgroundService
{
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly Channel<int> _requests = Channel.CreateBounded<int>(new BoundedChannelOptions(1)
    {
        FullMode = BoundedChannelFullMode.DropOldest,
    });

    private int? _currentScanRunId;
    private bool _isRunning;

    public bool IsRunning => _isRunning;
    public int? CurrentScanRunId => _currentScanRunId;

    public bool TryEnqueue()
    {
        if (_isRunning) return false;
        _requests.Writer.TryWrite(0);
        return true;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var _ in _requests.Reader.ReadAllAsync(stoppingToken))
        {
            if (!await _lock.WaitAsync(0)) continue; // already running
            _isRunning = true;
            try
            {
                using var scope = scopeFactory.CreateScope();
                var orchestrator = scope.ServiceProvider.GetRequiredService<ScanOrchestrator>();
                _currentScanRunId = await orchestrator.RunAsync(stoppingToken);
                logger.LogInformation("Scan run {Id} completed", _currentScanRunId);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Scan background service error");
            }
            finally
            {
                _isRunning = false;
                _lock.Release();
            }
        }
    }
}
