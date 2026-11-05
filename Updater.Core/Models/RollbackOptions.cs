namespace Updater.Core.Models;

public class RollbackOptions
{
    public string Destination { get; set; } = @"C:\La Fabrica de Software\MaquinasDispensadorasNuevoSoftware";
    public string BackupRoot { get; set; } = @"C:\La Fabrica de Software\Secur";
    public bool UseLast { get; set; } = false;
    public string? ToVersion { get; set; }
    public bool KeepCurrentAppSettings { get; set; } = true;
    public string ConfigFileName { get; set; } = "appsettings.json";
    public string SecurFolderName { get; set; } = "Secur";
}

