using Newtonsoft.Json;
using AppSettingsEditor.Models;
using System.IO;

namespace AppSettingsEditor.Services;

internal class JsonService
{
    private readonly string _configPath;
    private readonly string _backupDirectory;

    public JsonService(string? configPath = null)
    {
        _configPath = configPath ?? Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json");
        _backupDirectory = Path.GetDirectoryName(_configPath) ?? Directory.GetCurrentDirectory();
    }

    public bool FileExists()
    {
        return File.Exists(_configPath);
    }

    public AppSettingsRootModel? Load()
    {
        try
        {
            if (!File.Exists(_configPath))
                return null;

            var json = File.ReadAllText(_configPath);
            return JsonConvert.DeserializeObject<AppSettingsRootModel>(json);
        }
        catch
        {
            return null;
        }
    }

    public void Save(AppSettingsRootModel model)
    {
        // Create backup
        CreateBackup();

        // Atomic write
        var tempPath = _configPath + ".tmp";
        var json = JsonConvert.SerializeObject(model, Formatting.Indented);
        File.WriteAllText(tempPath, json);
        
        // Replace original
        File.Move(tempPath, _configPath, overwrite: true);
    }

    private void CreateBackup()
    {
        if (!File.Exists(_configPath))
            return;

        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var backupPath = Path.Combine(_backupDirectory, $"appsettings_{timestamp}.bak");
        File.Copy(_configPath, backupPath, overwrite: true);
    }

    public List<string> GetBackupFiles()
    {
        var directory = Path.GetDirectoryName(_configPath) ?? Directory.GetCurrentDirectory();
        return Directory.GetFiles(directory, "appsettings_*.bak")
            .OrderByDescending(f => File.GetCreationTime(f))
            .Select(Path.GetFileName)
            .Where(f => f != null)
            .Cast<string>()
            .ToList();
    }

    public bool RestoreBackup(string backupFileName)
    {
        try
        {
            var directory = Path.GetDirectoryName(_configPath) ?? Directory.GetCurrentDirectory();
            var backupPath = Path.Combine(directory, backupFileName);
            
            if (!File.Exists(backupPath))
                return false;

            File.Copy(backupPath, _configPath, overwrite: true);
            return true;
        }
        catch
        {
            return false;
        }
    }
}

