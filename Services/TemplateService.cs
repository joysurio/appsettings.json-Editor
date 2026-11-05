using Newtonsoft.Json;
using AppSettingsEditor.Models;
using System.IO;

namespace AppSettingsEditor.Services;

internal class TemplateService
{
    private readonly string _templatesDirectory;

    public TemplateService(string? baseDirectory = null)
    {
        var baseDir = baseDirectory ?? Directory.GetCurrentDirectory();
        _templatesDirectory = Path.Combine(baseDir, "templates");
        
        if (!Directory.Exists(_templatesDirectory))
            Directory.CreateDirectory(_templatesDirectory);
    }

    public List<string> GetAvailableTemplates()
    {
        if (!Directory.Exists(_templatesDirectory))
            return new List<string>();

        return Directory.GetFiles(_templatesDirectory, "template.*.json")
            .Select(Path.GetFileName)
            .Where(f => f != null)
            .Cast<string>()
            .ToList();
    }

    public AppSettingsRootModel? LoadTemplate(string templateFileName)
    {
        try
        {
            var templatePath = Path.Combine(_templatesDirectory, templateFileName);
            if (!File.Exists(templatePath))
                return null;

            var json = File.ReadAllText(templatePath);
            return JsonConvert.DeserializeObject<AppSettingsRootModel>(json);
        }
        catch
        {
            return null;
        }
    }

    public bool SaveTemplate(string templateName, AppSettingsRootModel model)
    {
        try
        {
            var templatePath = Path.Combine(_templatesDirectory, $"template.{templateName}.json");
            var json = JsonConvert.SerializeObject(model, Formatting.Indented);
            File.WriteAllText(templatePath, json);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public bool DeleteTemplate(string templateFileName)
    {
        try
        {
            var templatePath = Path.Combine(_templatesDirectory, templateFileName);
            if (File.Exists(templatePath))
            {
                File.Delete(templatePath);
                return true;
            }
            return false;
        }
        catch
        {
            return false;
        }
    }
}

