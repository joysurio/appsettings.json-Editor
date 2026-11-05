using Newtonsoft.Json;

namespace AppSettingsEditor.Models;

internal enum ParameterDataType
{
    String,
    Int,
    Bool,
    Double
}

internal class ParameterMetadata
{
    [JsonProperty("jsonPath")]
    public string JsonPath { get; set; } = string.Empty; // e.g., "AppSettings.CustomParameter"

    [JsonProperty("dataType")]
    public ParameterDataType DataType { get; set; } = ParameterDataType.String;

    [JsonProperty("defaultValue")]
    public object? DefaultValue { get; set; }

    [JsonProperty("allowedValues")]
    public List<string>? AllowedValues { get; set; }

    [JsonProperty("description")]
    public string Description { get; set; } = string.Empty;

    [JsonProperty("helpText")]
    public string HelpText { get; set; } = string.Empty;

    [JsonProperty("minValue")]
    public double? MinValue { get; set; }

    [JsonProperty("maxValue")]
    public double? MaxValue { get; set; }
}

internal class ParameterMetadataCollection
{
    [JsonProperty("parameters")]
    public List<ParameterMetadata> Parameters { get; set; } = new();
}

