using System.IO;
using AppSettingsEditor.Models;
using Newtonsoft.Json;

namespace AppSettingsEditor.Services;

internal class RemoteMachineAgentConfigService
{
    public static string GetDefaultConfigPath() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "RemoteMachineAgent",
        "config.json");

    public RemoteMachineAgentSettings Load(string configFilePath)
    {
        if (!File.Exists(configFilePath))
            return new RemoteMachineAgentSettings();

        var json = File.ReadAllText(configFilePath);
        return JsonConvert.DeserializeObject<RemoteMachineAgentSettings>(json) ?? new RemoteMachineAgentSettings();
    }

    public void Save(string configFilePath, RemoteMachineAgentSettings settings)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(configFilePath));
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var json = JsonConvert.SerializeObject(settings, Formatting.Indented);
        File.WriteAllText(configFilePath, json);
    }
}
