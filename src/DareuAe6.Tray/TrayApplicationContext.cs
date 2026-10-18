using System.Runtime.InteropServices;
using DareuAe6.Core;
using DareuAe6.Windows;

namespace DareuAe6.Tray;

internal sealed class TrayApplicationContext : ApplicationContext
{
    private readonly NotifyIcon _notifyIcon;
    private readonly ToolStripMenuItem _refreshItem;
    private readonly PollCoordinator _coordinator;
    private readonly CancellationTokenSource _shutdown = new();
    private readonly SynchronizationContext _uiContext;
    private Icon _icon;
    private byte? _shownLevel;
    private bool _disposed;

    public TrayApplicationContext()
    {
        _uiContext = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();
        _icon = SystemIcons.Information;
        _refreshItem = new ToolStripMenuItem("Refresh", null, (_, _) => _ = RefreshAsync());
        var exitItem = new ToolStripMenuItem("Exit", null, (_, _) => Exit());
        var menu = new ContextMenuStrip();
        menu.Items.Add(_refreshItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(exitItem);
        _notifyIcon = new NotifyIcon { Icon = _icon, Text = "DAREU AE6 V2: reading…", Visible = true, ContextMenuStrip = menu };
        _notifyIcon.DoubleClick += (_, _) => _ = RefreshAsync();
        _coordinator = new PollCoordinator(new NativeHidTransport(), PublishStateAsync);
        _coordinator.Start();
    }

    private async Task RefreshAsync()
    {
        _refreshItem.Enabled = false;
        try { await _coordinator.RefreshAsync(_shutdown.Token).ConfigureAwait(true); }
        catch (OperationCanceledException) when (_shutdown.IsCancellationRequested) { }
        finally { _refreshItem.Enabled = !_disposed; }
    }

    private ValueTask PublishStateAsync(BatteryState state)
    {
        if (_disposed) return ValueTask.CompletedTask;
        _uiContext.Post(_ => ApplyState(state), null);
        return ValueTask.CompletedTask;
    }

    private void ApplyState(BatteryState state)
    {
        if (_disposed) return;
        var text = state.Availability == BatteryAvailability.Current && state.Reading is { } reading ? $"DAREU AE6 V2: {reading.Level}%" : state.Availability == BatteryAvailability.InvalidReport ? "DAREU AE6 V2: invalid report" : "DAREU AE6 V2: unavailable";
        if (_notifyIcon.Text != text) _notifyIcon.Text = text;
        if (state.Availability == BatteryAvailability.Current && state.Reading is { } current && _shownLevel != current.Level)
        {
            var nextIcon = CreateBatteryIcon(current.Level);
            var oldIcon = _icon;
            _icon = nextIcon;
            _shownLevel = current.Level;
            _notifyIcon.Icon = nextIcon;
            oldIcon.Dispose();
        }
    }

    private static Icon CreateBatteryIcon(byte level)
    {
        using var bitmap = new Bitmap(32, 32);
        using (var graphics = Graphics.FromImage(bitmap))
        using (var brush = new SolidBrush(level <= 20 ? Color.Firebrick : level <= 50 ? Color.DarkOrange : Color.ForestGreen))
        using (var font = new Font(FontFamily.GenericSansSerif, 8, FontStyle.Bold, GraphicsUnit.Pixel))
        {
            graphics.Clear(Color.Transparent);
            graphics.DrawString(level.ToString(), font, brush, new PointF(1, 10));
        }
        var iconHandle = bitmap.GetHicon();
        try { using var icon = Icon.FromHandle(iconHandle); return (Icon)icon.Clone(); }
        finally { DeleteObject(iconHandle); }
    }

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteObject(IntPtr objectHandle);

    private void Exit()
    {
        if (_disposed) return;
        _disposed = true;
        _shutdown.Cancel();
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _icon.Dispose();
        _ = DisposeCoordinatorAsync();
        ExitThread();
    }

    private async Task DisposeCoordinatorAsync()
    {
        await _coordinator.DisposeAsync().ConfigureAwait(false);
        _shutdown.Dispose();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_disposed) Exit();
        base.Dispose(disposing);
    }
}
