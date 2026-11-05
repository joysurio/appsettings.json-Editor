namespace AppSettingsEditor.Models;

/// <summary>
/// Settings persisted to ResidentOpenExe <c>Config.xml</c> (sections Parameters and optional CloseExe).
/// </summary>
internal class ResidentOpenExeSettings
{
    public string ExecutableFileName { get; set; } = "LFSMachines.Desktop.exe";

    /// <summary>Folder containing the executable (maps to RutaExe in XML).</summary>
    public string ExecutableDirectory { get; set; } = @"C:\La Fabrica de Software\MaquinasDispensadorasNuevoSoftware";

    public string? Executable2FileName { get; set; }
    public string? Executable2Directory { get; set; }

    /// <summary>Timer interval in seconds (TimerEspera).</summary>
    public int CheckIntervalSeconds { get; set; } = 2;

    /// <summary>SoloUnaVez: start once then exit ResidentOpenExe.</summary>
    public bool RunOnceThenExit { get; set; }

    public bool BringWindowToFront { get; set; }

    public string? TrayDisplayName { get; set; }

    public bool OpenAsAdministrator { get; set; }

    /// <summary>If true, a CloseExe section is written; ResidentOpenExe enables heartbeat logic (requires Funcionamiento.txt where applicable).</summary>
    public bool IncludeCloseExeSection { get; set; }

    public int HeartbeatTimeoutMs { get; set; } = 120_000;

    public string? LogDirectory { get; set; }
}
