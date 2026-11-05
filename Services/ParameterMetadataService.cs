using AppSettingsEditor.Models;
using Newtonsoft.Json;
using System.IO;

namespace AppSettingsEditor.Services;

internal class ParameterMetadataService
{
    private readonly string _metadataPath;
    private ParameterMetadataCollection? _metadata;

    public ParameterMetadataService(string? baseDirectory = null)
    {
        var baseDir = baseDirectory ?? Directory.GetCurrentDirectory();
        _metadataPath = Path.Combine(baseDir, "parameter-metadata.json");
        LoadMetadata();
    }

    private void LoadMetadata()
    {
        try
        {
            if (File.Exists(_metadataPath))
            {
                var json = File.ReadAllText(_metadataPath);
                _metadata = JsonConvert.DeserializeObject<ParameterMetadataCollection>(json);
            }
            else
            {
                _metadata = new ParameterMetadataCollection { Parameters = new List<ParameterMetadata>() };
                SaveMetadata();
            }
        }
        catch
        {
            _metadata = new ParameterMetadataCollection { Parameters = new List<ParameterMetadata>() };
        }
    }

    public List<ParameterMetadata> GetAllMetadata()
    {
        return _metadata?.Parameters ?? new List<ParameterMetadata>();
    }

    public ParameterMetadata? GetMetadata(string jsonPath)
    {
        return _metadata?.Parameters.FirstOrDefault(p => p.JsonPath == jsonPath);
    }

    public void AddMetadata(ParameterMetadata metadata)
    {
        if (_metadata == null)
            _metadata = new ParameterMetadataCollection { Parameters = new List<ParameterMetadata>() };

        // Remove existing if present
        _metadata.Parameters.RemoveAll(p => p.JsonPath == metadata.JsonPath);
        
        // Add new
        _metadata.Parameters.Add(metadata);
        SaveMetadata();
    }

    public void RemoveMetadata(string jsonPath)
    {
        if (_metadata?.Parameters != null)
        {
            _metadata.Parameters.RemoveAll(p => p.JsonPath == jsonPath);
            SaveMetadata();
        }
    }

    private void SaveMetadata()
    {
        try
        {
            var json = JsonConvert.SerializeObject(_metadata, Formatting.Indented);
            File.WriteAllText(_metadataPath, json);
        }
        catch
        {
            // Ignore save errors
        }
    }
}

