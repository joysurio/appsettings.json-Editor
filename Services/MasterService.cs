using Newtonsoft.Json;
using AppSettingsEditor.Models;
using System.IO;

namespace AppSettingsEditor.Services;

internal class MasterService
{
    private readonly string _mastersDirectory;

    public MasterService(string? baseDirectory = null)
    {
        var baseDir = baseDirectory ?? Directory.GetCurrentDirectory();
        _mastersDirectory = Path.Combine(baseDir, "masters");
        
        if (!Directory.Exists(_mastersDirectory))
            Directory.CreateDirectory(_mastersDirectory);
    }

    public List<string> GetAvailableMasters()
    {
        if (!Directory.Exists(_mastersDirectory))
            return new List<string>();

        return Directory.GetFiles(_mastersDirectory, "master.*.json")
            .Select(Path.GetFileName)
            .Where(f => f != null)
            .Cast<string>()
            .ToList();
    }

    public AppSettingsRootModel? LoadMaster(string masterFileName)
    {
        try
        {
            var masterPath = Path.Combine(_mastersDirectory, masterFileName);
            if (!File.Exists(masterPath))
                return null;

            var json = File.ReadAllText(masterPath);
            return JsonConvert.DeserializeObject<AppSettingsRootModel>(json);
        }
        catch
        {
            return null;
        }
    }

    public AppSettingsRootModel LoadMergedMasters(string? machineType = null, string? customer = null, string? machineId = null)
    {
        var result = new AppSettingsRootModel();

        // Merge order: default, type, customer, machine
        var masters = new List<string?> { "master.default.json" };
        
        if (!string.IsNullOrEmpty(machineType))
            masters.Add($"master.{machineType}.json");
        
        if (!string.IsNullOrEmpty(customer))
            masters.Add($"master.{customer}.json");
        
        if (!string.IsNullOrEmpty(machineId))
            masters.Add($"master.{machineId}.json");

        foreach (var master in masters)
        {
            var masterModel = LoadMaster(master!);
            if (masterModel != null)
            {
                result = MergeModels(result, masterModel);
            }
        }

        return result;
    }

    private AppSettingsRootModel MergeModels(AppSettingsRootModel target, AppSettingsRootModel source)
    {
        // Merge ConnectionStrings
        if (source.ConnectionStrings != null)
        {
            target.ConnectionStrings ??= new ConnectionStringsModel();
            MergeObject(source.ConnectionStrings, target.ConnectionStrings);
        }

        // Merge AppSettings
        if (source.AppSettings != null)
        {
            target.AppSettings ??= new AppSettingsModel();
            MergeObject(source.AppSettings, target.AppSettings);
        }

        // Merge DispenseConfiguration
        if (source.DispenseConfiguration != null)
        {
            target.DispenseConfiguration ??= new DispenseConfigurationModel();
            MergeObject(source.DispenseConfiguration, target.DispenseConfiguration);
        }

        // Merge AntennaConfiguration
        if (source.AntennaConfiguration != null)
        {
            target.AntennaConfiguration ??= new AntennaConfigurationModel();
            MergeObject(source.AntennaConfiguration, target.AntennaConfiguration);
        }

        // Merge EmailConfiguration
        if (source.EmailConfiguration != null)
        {
            target.EmailConfiguration ??= new EmailConfigurationModel();
            MergeObject(source.EmailConfiguration, target.EmailConfiguration);
        }

        return target;
    }

    private void MergeObject(object source, object target)
    {
        var sourceProps = source.GetType().GetProperties();
        var targetProps = target.GetType().GetProperties();

        foreach (var sourceProp in sourceProps)
        {
            var targetProp = targetProps.FirstOrDefault(p => p.Name == sourceProp.Name);
            if (targetProp != null && targetProp.CanWrite)
            {
                var value = sourceProp.GetValue(source);
                if (value != null)
                {
                    targetProp.SetValue(target, value);
                }
            }
        }
    }
}

