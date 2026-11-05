using AppSettingsEditor.Models;
using Newtonsoft.Json;
using System.IO;

namespace AppSettingsEditor.Services;

internal class MachineTypeService
{
    private readonly string _configPath;
    private MachineTypeConfigurations? _configurations;

    public MachineTypeService(string? baseDirectory = null)
    {
        var baseDir = baseDirectory ?? Directory.GetCurrentDirectory();
        _configPath = Path.Combine(baseDir, "machine-types.json");
        LoadConfiguration();
    }

    private void LoadConfiguration()
    {
        try
        {
            if (File.Exists(_configPath))
            {
                var json = File.ReadAllText(_configPath);
                _configurations = JsonConvert.DeserializeObject<MachineTypeConfigurations>(json);
            }
            else
            {
                // Create default configuration
                _configurations = CreateDefaultConfiguration();
                SaveConfiguration();
            }
        }
        catch
        {
            _configurations = CreateDefaultConfiguration();
        }
    }

    private MachineTypeConfigurations CreateDefaultConfiguration()
    {
        return new MachineTypeConfigurations
        {
            MachineTypes = new List<MachineTypeConfiguration>
            {
                new MachineTypeConfiguration
                {
                    MachineType = "dispenser",
                    DisplayName = "Dispenser",
                    Sections = new List<string> { "ConnectionStrings", "AppSettings", "DispenseConfiguration", "AntennaConfiguration", "EmailConfiguration" },
                    Parameters = new Dictionary<string, List<string>>()
                },
                new MachineTypeConfiguration
                {
                    MachineType = "collector",
                    DisplayName = "Collector",
                    Sections = new List<string> { "ConnectionStrings", "AppSettings", "AntennaConfiguration", "EmailConfiguration" },
                    Parameters = new Dictionary<string, List<string>>()
                },
                new MachineTypeConfiguration
                {
                    MachineType = "gravity",
                    DisplayName = "Gravity Machine",
                    Sections = new List<string> { "ConnectionStrings", "AppSettings", "EmailConfiguration" },
                    Parameters = new Dictionary<string, List<string>>()
                },
                new MachineTypeConfiguration
                {
                    MachineType = "locker",
                    DisplayName = "Locker",
                    Sections = new List<string> { "ConnectionStrings", "AppSettings", "AntennaConfiguration", "EmailConfiguration" },
                    Parameters = new Dictionary<string, List<string>>()
                }
            }
        };
    }

    public List<MachineTypeConfiguration> GetAvailableMachineTypes()
    {
        return _configurations?.MachineTypes ?? new List<MachineTypeConfiguration>();
    }

    public MachineTypeConfiguration? GetMachineTypeConfiguration(string machineType)
    {
        return _configurations?.MachineTypes.FirstOrDefault(mt => mt.MachineType == machineType);
    }

    public List<string> GetSectionsForMachineType(string machineType)
    {
        var config = GetMachineTypeConfiguration(machineType);
        return config?.Sections ?? new List<string>();
    }

    public List<string> GetParametersForSection(string machineType, string section)
    {
        var config = GetMachineTypeConfiguration(machineType);
        if (config?.Parameters.ContainsKey(section) == true)
        {
            return config.Parameters[section];
        }
        return new List<string>(); // Empty means all parameters
    }

    private void SaveConfiguration()
    {
        try
        {
            var json = JsonConvert.SerializeObject(_configurations, Formatting.Indented);
            File.WriteAllText(_configPath, json);
        }
        catch
        {
            // Ignore save errors
        }
    }
}

