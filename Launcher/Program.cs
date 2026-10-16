using System;
using System.Net;
using System.Windows.Forms;

namespace Spieleabend.Launcher;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        // Lange Pfade (> 260 Zeichen) erlauben - muss vor jedem Dateizugriff passieren
        AppContext.SetSwitch("Switch.System.IO.UseLegacyPathHandling", false);
        AppContext.SetSwitch("Switch.System.IO.BlockLongPaths", false);

        // GitHub verlangt TLS 1.2
        ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new LauncherForm());
    }
}
