using System.IO;

namespace Updater.Core.Services;

public class FileLockDetector
{
    private readonly ILogger _logger;

    public FileLockDetector(ILogger logger)
    {
        _logger = logger;
    }

    public List<string> DetectLockedFiles(string directoryPath)
    {
        var lockedFiles = new List<string>();
        
        if (!Directory.Exists(directoryPath))
        {
            return lockedFiles;
        }

        try
        {
            var files = Directory.GetFiles(directoryPath, "*", SearchOption.AllDirectories);
            
            foreach (var file in files)
            {
                if (IsFileLocked(file))
                {
                    lockedFiles.Add(file);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error scanning directory for locked files: {ex.Message}");
            throw;
        }

        return lockedFiles;
    }

    private bool IsFileLocked(string filePath)
    {
        try
        {
            using (var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                // File is not locked if we can open it with exclusive access
            }
            return false;
        }
        catch (IOException)
        {
            // File is locked or in use
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            // File might be locked or access denied
            return true;
        }
    }
}

