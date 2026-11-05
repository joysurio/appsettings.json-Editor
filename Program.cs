using System;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using AppSettingsEditor.Forms;

namespace AppSettingsEditor;

static class Program
{
    /// <summary>
    /// Load Updater assemblies from bytes so the build can overwrite modules\*.dll while the app is running.
    /// </summary>
    private static Assembly? ResolveUpdaterModules(object? sender, ResolveEventArgs args)
    {
        var name = new AssemblyName(args.Name).Name;
        if (name is not ("Updater.Cli" or "Updater.Core"))
            return null;

        var path = Path.Combine(AppContext.BaseDirectory, "modules", name + ".dll");
        if (!File.Exists(path))
            return null;

        return Assembly.Load(File.ReadAllBytes(path));
    }

    /// <summary>
    /// The main entry point for the application.
    /// </summary>
    [STAThread]
    static void Main()
    {
        AppDomain.CurrentDomain.AssemblyResolve += ResolveUpdaterModules;

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        // No password at startup: run minimized to tray so ResidentOpenExe watchdog can start immediately.
        // Password is required when opening the UI (tray) or when exiting (tray Exit).
        Application.Run(new MainMenuForm());
    }
}

