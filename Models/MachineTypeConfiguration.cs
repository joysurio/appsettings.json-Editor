using Newtonsoft.Json;

namespace AppSettingsEditor.Models;

internal class MachineTypeConfiguration
{
    [JsonProperty("machineType")]
    public string MachineType { get; set; } = string.Empty;

    [JsonProperty("displayName")]
    public string DisplayName { get; set; } = string.Empty;

    [JsonProperty("sections")]
    public List<string> Sections { get; set; } = new();

    [JsonProperty("parameters")]
    public Dictionary<string, List<string>> Parameters { get; set; } = new();
}

internal class MachineTypeConfigurations
{
    [JsonProperty("machineTypes")]
    public List<MachineTypeConfiguration> MachineTypes { get; set; } = new();
}

