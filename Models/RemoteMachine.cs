using System.ComponentModel;

namespace AppSettingsEditor.Models;

internal class RemoteMachine
{
    [Description("Unique identifier for the remote machine")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Description("Display name for the remote machine")]
    public string Name { get; set; } = "";

    [Description("IP address or hostname of the remote machine")]
    public string Address { get; set; } = "";

    [Description("Connection method (NetworkShare, RustDesk, RDP, etc.)")]
    public string ConnectionMethod { get; set; } = "NetworkShare";

    [Description("Network share path (e.g., \\\\192.168.1.100\\C$\\Path\\To\\App)")]
    public string? NetworkSharePath { get; set; }

    [Description("Username for authentication (if required)")]
    public string? Username { get; set; }

    [Description("Password for authentication (if required) - stored encrypted")]
    public string? Password { get; set; }

    [Description("RustDesk ID (if using RustDesk connection method)")]
    public string? RustDeskId { get; set; }

    [Description("RustDesk password (if using RustDesk connection method)")]
    public string? RustDeskPassword { get; set; }

    [Description("Path to appsettings.json on remote machine")]
    public string RemoteAppSettingsPath { get; set; } = @"C:\Path\To\appsettings.json";

    [Description("Machine type identifier")]
    public string? MachineType { get; set; }

    [Description("Last connection status")]
    public ConnectionStatus LastStatus { get; set; } = ConnectionStatus.Unknown;

    [Description("Last connection attempt timestamp")]
    public DateTime? LastConnectionAttempt { get; set; }

    [Description("Additional notes or description")]
    public string? Notes { get; set; }
}

internal enum ConnectionStatus
{
    Unknown,
    Connected,
    Disconnected,
    Error,
    Connecting
}

