namespace DareuAe6.Core;

public sealed class PollCoordinator : IAsyncDisposable
{
    private readonly IBatteryTransport _transport;
    private readonly TimeSpan _interval;
    private readonly Func<BatteryState, ValueTask> _onState;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly CancellationTokenSource _stop = new();
    private BatteryState _state = BatteryState.Unknown();
    private Task? _loop;

    public PollCoordinator(IBatteryTransport transport, Func<BatteryState, ValueTask> onState, TimeSpan? interval = null)
    {
        _transport = transport;
        _onState = onState;
        _interval = interval ?? TimeSpan.FromMinutes(5);
    }

    public BatteryState State => _state;
    public void Start() => _loop ??= Task.Run(RunAsync);
    public Task RefreshAsync(CancellationToken cancellationToken = default) => PollOnceAsync(cancellationToken);

    private async Task RunAsync()
    {
        try
        {
            await PollOnceAsync(_stop.Token).ConfigureAwait(false);
            using var timer = new PeriodicTimer(_interval);
            while (await timer.WaitForNextTickAsync(_stop.Token).ConfigureAwait(false))
                await PollOnceAsync(_stop.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (_stop.IsCancellationRequested) { }
    }

    private async Task PollOnceAsync(CancellationToken cancellationToken)
    {
        if (!await _gate.WaitAsync(0, cancellationToken).ConfigureAwait(false)) return;
        try
        {
            try
            {
                var reading = await _transport.ReadBatteryAsync(cancellationToken).ConfigureAwait(false);
                _state = reading is { } value
                    ? new BatteryState(BatteryAvailability.Current, value, DateTimeOffset.UtcNow, null)
                    : new BatteryState(BatteryAvailability.Unavailable, null, null, "Device not found or no response");
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (InvalidDataException ex) { _state = new BatteryState(BatteryAvailability.InvalidReport, null, null, ex.Message); }
            catch (Exception ex) when (ex is IOException or InvalidOperationException) { _state = new BatteryState(BatteryAvailability.Unavailable, null, null, ex.Message); }
            await _onState(_state).ConfigureAwait(false);
        }
        finally { _gate.Release(); }
    }

    public async ValueTask DisposeAsync()
    {
        _stop.Cancel();
        if (_loop is not null) try { await _loop.ConfigureAwait(false); } catch (OperationCanceledException) { }
        _stop.Dispose();
        _gate.Dispose();
    }
}
