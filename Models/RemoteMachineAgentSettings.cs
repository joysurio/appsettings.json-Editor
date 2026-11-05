namespace AppSettingsEditor.Models;

/// <summary>
/// Must stay in sync with RemoteMachineAgent <c>AppConfiguration</c> (config.json).
/// </summary>
internal class RemoteMachineAgentSettings
{
    public string DatabaseConnectionString { get; set; } = string.Empty;

    public string MachineId { get; set; } = Environment.MachineName;

    public int HeartbeatIntervalSeconds { get; set; } = 60;

    public int CommandCheckIntervalSeconds { get; set; } = 30;

    public bool MinimizeToTray { get; set; } = true;

    public bool StartWithWindows { get; set; }
}
