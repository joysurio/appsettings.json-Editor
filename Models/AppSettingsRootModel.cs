using Newtonsoft.Json;

namespace AppSettingsEditor.Models;

internal class AppSettingsRootModel
{
    [JsonProperty("ConnectionStrings")]
    public ConnectionStringsModel? ConnectionStrings { get; set; } = new();

    [JsonProperty("AppSettings")]
    public AppSettingsModel? AppSettings { get; set; } = new();

    [JsonProperty("DispenseConfiguration")]
    public DispenseConfigurationModel? DispenseConfiguration { get; set; } = new();

    [JsonProperty("AntennaConfiguration")]
    public AntennaConfigurationModel? AntennaConfiguration { get; set; } = new();

    [JsonProperty("EmailConfiguration")]
    public EmailConfigurationModel? EmailConfiguration { get; set; } = new();
}

