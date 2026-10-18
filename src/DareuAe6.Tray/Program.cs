using System.Threading;

namespace DareuAe6.Tray;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        using var mutex = new Mutex(true, "Local\\DareuAe6Battery", out var createdNew);
        if (!createdNew) return;
        ApplicationConfiguration.Initialize();
        Application.Run(new TrayApplicationContext());
    }
}
