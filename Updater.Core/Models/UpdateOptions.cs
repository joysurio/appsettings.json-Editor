namespace Updater.Core.Models;

public class UpdateOptions
{
    public string Source { get; set; } = @"C:\La Fabrica de Software\net7.0-windows.rar";
    public string Destination { get; set; } = @"C:\La Fabrica de Software\MaquinasDispensadorasNuevoSoftware";
    public string BackupRoot { get; set; } = @"C:\La Fabrica de Software\Secur";
    public string ConfigFile { get; set; } = "appsettings.json";
    public string? InnerFolder { get; set; }
    public bool PreserveConfig { get; set; } = true;
    public bool RequireConfig { get; set; } = false;
    public bool DryRun { get; set; } = false;
    public bool DeleteSourceArchive { get; set; } = true;
    public string? WinRarPath { get; set; }
}

