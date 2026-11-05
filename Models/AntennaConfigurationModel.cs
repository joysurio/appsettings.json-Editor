using System.ComponentModel;

namespace AppSettingsEditor.Models;

internal class AntennaConfigurationModel
{
    [Description("Antenna power. CAEN range: 0–200; Chainway range: 0–30. Default: 20")]
    [DefaultValue(20)]
    public int? PowerAntenna { get; set; } = 20;

    [Description("Impinj antenna sensitivity (document indicates -71). Default: -71")]
    [DefaultValue("-71")]
    public string? AntennaSensitivity { get; set; } = "-71";

    [Description("Antenna 2 power (Impinj). Default: 20")]
    [DefaultValue(20)]
    public int? PowerAntenna2 { get; set; } = 20;

    [Description("Antenna 2 sensitivity (document indicates -71). Default: -71")]
    [DefaultValue("-71")]
    public string? AntennaSensitivity2 { get; set; } = "-71";

    [Description("Wait time between CAEN reads when not auto-reading. Default: 100 ms")]
    [DefaultValue(100)]
    public int? TimeWaitSlate { get; set; } = 100;

    [Description("Customer-specific Impinj configuration flag. Default: False")]
    [DefaultValue(false)]
    public bool? ConfigurationImpinj2 { get; set; } = false;

    [Description("RSSI threshold / distance-intensity parameter for chip reading. Default: -999")]
    [DefaultValue("-999")]
    public string? RSSIAntenna { get; set; } = "-999";

    [Description("Number of read attempts. Default: 10")]
    [DefaultValue(10)]
    public int? AmountOfReadsChainway { get; set; } = 10;

    [Description("Chainway session (S0..S3). Allowed: 0..3. Default: 1")]
    [DefaultValue(1)]
    public int? SesionAntena { get; set; } = 1;

    [Description("Chainway region. Allowed: China1, China2, Europe, USA, Korea, Japan, Taiwan, South Africa. Default: Europe")]
    [DefaultValue("Europe")]
    public string? RegionChainway { get; set; } = "Europe";
}

