using System.IO;
using System.Linq;
using Updater.Core.Models;

namespace Updater.Core.Services;

public class UpdateService
{
    private readonly ILogger _logger;
    private readonly ArchiveExtractor _extractor;
    private readonly FileLockDetector _lockDetector;

    public UpdateService(ILogger logger, ArchiveExtractor extractor, FileLockDetector lockDetector)
    {
        _logger = logger;
        _extractor = extractor;
        _lockDetector = lockDetector;
    }

    public void Update(UpdateOptions options)
    {
        _logger.LogInfo("Starting update process...");

        // Validate inputs
        ValidateOptions(options);

        // Create unique temp folder
        var tempFolder = Path.Combine(Path.GetTempPath(), $"updater_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempFolder);
        string? backupPath = null;

        try
        {
            // Expand source
            _logger.LogInfo($"Expanding source: {options.Source}");
            var expandedRoot = _extractor.Extract(options.Source, tempFolder, options.WinRarPath);

            // Resolve inner folder
            var innerFolder = ResolveInnerFolder(expandedRoot, options.InnerFolder);
            var newBuildPath = string.IsNullOrEmpty(innerFolder) 
                ? expandedRoot 
                : Path.Combine(expandedRoot, innerFolder);

            if (!Directory.Exists(newBuildPath))
            {
                throw new DirectoryNotFoundException($"Inner folder not found: {innerFolder}");
            }

            // Preserve config
            if (options.PreserveConfig && Directory.Exists(options.Destination))
            {
                PreserveConfigFile(options, newBuildPath);
            }

            // Detect locked files
            if (Directory.Exists(options.Destination))
            {
                _logger.LogInfo("Checking for locked files in destination...");
                var lockedFiles = _lockDetector.DetectLockedFiles(options.Destination);
                
                if (lockedFiles.Count > 0)
                {
                    _logger.LogError("Locked files detected:");
                    foreach (var file in lockedFiles)
                    {
                        _logger.LogError($"  - {file}");
                    }
                    throw new InvalidOperationException($"Cannot proceed: {lockedFiles.Count} file(s) are locked or in use.");
                }
            }

            if (options.DryRun)
            {
                _logger.LogInfo("[DRY RUN] Would perform update:");
                _logger.LogInfo($"  - Backup current: {options.Destination} -> {GetBackupPath(options)}");
                _logger.LogInfo($"  - Deploy new: {newBuildPath} -> {options.Destination}");
                return;
            }

            // Create backup
            if (Directory.Exists(options.Destination))
            {
                backupPath = CreateBackup(options);
            }

            // Deploy new build
            DeployNewBuild(newBuildPath, options.Destination);

            // Cleanup temp
            CleanupTemp(tempFolder);

            // Success summary
            _logger.LogInfo("Update completed successfully!");
            _logger.LogInfo($"  - Destination updated: {options.Destination}");
            if (backupPath != null)
            {
                _logger.LogInfo($"  - Backup created: {backupPath}");
            }
            _logger.LogInfo($"  - Source used: {options.Source}");

            // Optional cleanup: delete source archive after successful deployment.
            TryDeleteSourceArchive(options);
        }
        catch
        {
            TryRestoreDestinationFromBackup(options, backupPath);
            CleanupTemp(tempFolder);
            throw;
        }
    }

    private void ValidateOptions(UpdateOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Source))
        {
            throw new ArgumentException("Source path cannot be empty");
        }

        if (string.IsNullOrWhiteSpace(options.Destination))
        {
            throw new ArgumentException("Destination path cannot be empty");
        }

        if (string.IsNullOrWhiteSpace(options.BackupRoot))
        {
            throw new ArgumentException("Backup root path cannot be empty");
        }

        // Backup root must not be the same as or inside the destination (would make backup path a subfolder of destination and break MoveDirectory)
        var normalizedDest = Path.GetFullPath(options.Destination).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var normalizedBackupRoot = Path.GetFullPath(options.BackupRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (string.Equals(normalizedDest, normalizedBackupRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Backup root cannot be the same folder as the destination. Choose a different backup location (e.g. a parent or sibling folder).");
        }
        if (normalizedBackupRoot.StartsWith(normalizedDest + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
            normalizedBackupRoot.StartsWith(normalizedDest + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Backup root cannot be inside the destination folder. Choose a backup location outside the app folder (e.g. C:\\La Fabrica de Software\\backups).");
        }
    }

    private string ResolveInnerFolder(string expandedRoot, string? specifiedInnerFolder)
    {
        if (!string.IsNullOrEmpty(specifiedInnerFolder))
        {
            var path = Path.Combine(expandedRoot, specifiedInnerFolder);
            if (!Directory.Exists(path))
            {
                throw new DirectoryNotFoundException($"Specified inner folder not found: {specifiedInnerFolder}");
            }
            return specifiedInnerFolder;
        }

        // Auto-detect: find directories in expanded root
        var directories = Directory.GetDirectories(expandedRoot);
        var files = Directory.GetFiles(expandedRoot);

        // If there are files in root and no subdirectories, the root itself is the inner folder
        // In this case, we'll deploy the entire expanded root
        if (files.Length > 0 && directories.Length == 0)
        {
            // Return empty string to indicate the root itself
            return string.Empty;
        }

        if (directories.Length == 0)
        {
            throw new InvalidOperationException("No directories found in expanded package");
        }

        if (directories.Length == 1)
        {
            return Path.GetFileName(directories[0]);
        }

        // Multiple directories - try to auto-detect the main application folder
        _logger.LogInfo($"Multiple directories found. Attempting to auto-detect main application folder...");
        var folderNames = directories.Select(d => Path.GetFileName(d)).ToList();
        _logger.LogInfo($"Found directories: {string.Join(", ", folderNames)}");

        // Strategy 1: Look for folder containing .exe files (main application)
        var exeFolders = directories.Where(d => 
            Directory.GetFiles(d, "*.exe", SearchOption.AllDirectories).Length > 0
        ).ToList();

        if (exeFolders.Count == 1)
        {
            var selectedFolder = Path.GetFileName(exeFolders[0]);
            _logger.LogInfo($"Auto-selected folder with .exe files: {selectedFolder}");
            return selectedFolder;
        }

        // Strategy 2: Exclude common dependency/reference folders and pick the first remaining
        var excludePatterns = new[] { "reference", "references", "lib", "libs", "packages", "bin", "obj" };
        var candidateFolders = directories.Where(d =>
        {
            var folderName = Path.GetFileName(d).ToLowerInvariant();
            return !excludePatterns.Any(pattern => folderName.Contains(pattern));
        }).ToList();

        if (candidateFolders.Count == 1)
        {
            var selectedFolder = Path.GetFileName(candidateFolders[0]);
            _logger.LogInfo($"Auto-selected folder (excluding dependency folders): {selectedFolder}");
            return selectedFolder;
        }

        // Strategy 3: Look for folder with most files (likely the main application)
        var folderWithMostFiles = directories
            .Select(d => new
            {
                Path = d,
                Name = Path.GetFileName(d),
                FileCount = Directory.GetFiles(d, "*", SearchOption.AllDirectories).Length
            })
            .OrderByDescending(f => f.FileCount)
            .First();

        _logger.LogInfo($"Auto-selected folder with most files: {folderWithMostFiles.Name} ({folderWithMostFiles.FileCount} files)");
        return folderWithMostFiles.Name;
    }

    private void PreserveConfigFile(UpdateOptions options, string newBuildPath)
    {
        var sourceConfig = Path.Combine(options.Destination, options.ConfigFile);
        var targetConfig = Path.Combine(newBuildPath, options.ConfigFile);

        if (File.Exists(sourceConfig))
        {
            _logger.LogInfo($"Preserving config file: {options.ConfigFile}");
            File.Copy(sourceConfig, targetConfig, overwrite: true);
        }
        else
        {
            if (options.RequireConfig)
            {
                throw new FileNotFoundException(
                    $"Required config file not found: {sourceConfig}. " +
                    "Set --require-config=false to allow missing config.");
            }
            _logger.LogWarn($"Config file not found in destination: {sourceConfig}. Continuing without it.");
        }
    }

    private string CreateBackup(UpdateOptions options)
    {
        if (!Directory.Exists(options.BackupRoot))
        {
            Directory.CreateDirectory(options.BackupRoot);
        }

        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var backupName = $"{Path.GetFileName(options.Destination.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))}_{timestamp}";
        var backupPath = Path.Combine(options.BackupRoot, backupName);

        if (Directory.Exists(backupPath))
        {
            throw new InvalidOperationException($"Backup folder already exists: {backupPath}");
        }

        _logger.LogInfo($"Creating backup: {backupPath}");
        DirectoryHelper.MoveDirectory(options.Destination, backupPath, _logger);
        
        return backupPath;
    }

    private void DeployNewBuild(string newBuildPath, string destination)
    {
        _logger.LogInfo($"Deploying new build to: {destination}");
        
        if (Directory.Exists(destination))
        {
            throw new InvalidOperationException($"Destination already exists (should have been moved to backup): {destination}");
        }

        DirectoryHelper.MoveDirectory(newBuildPath, destination, _logger);
    }

    private string GetBackupPath(UpdateOptions options)
    {
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var backupName = $"{Path.GetFileName(options.Destination.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))}_{timestamp}";
        return Path.Combine(options.BackupRoot, backupName);
    }

    private void CleanupTemp(string tempFolder)
    {
        try
        {
            if (Directory.Exists(tempFolder))
            {
                Directory.Delete(tempFolder, true);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarn($"Failed to cleanup temp folder: {ex.Message}");
        }
    }

    private void TryRestoreDestinationFromBackup(UpdateOptions options, string? backupPath)
    {
        if (string.IsNullOrWhiteSpace(backupPath) || !Directory.Exists(backupPath))
            return;

        try
        {
            _logger.LogWarn("Update failed after destination backup. Attempting automatic destination restore...");

            if (Directory.Exists(options.Destination))
            {
                // Remove partial destination before restoring original folder.
                Directory.Delete(options.Destination, recursive: true);
            }

            DirectoryHelper.MoveDirectory(backupPath, options.Destination, _logger);
            _logger.LogInfo($"Destination restored from backup: {options.Destination}");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Automatic destination restore failed. Manual recovery may be required. Backup location: {backupPath}. Error: {ex.Message}");
        }
    }

    private void TryDeleteSourceArchive(UpdateOptions options)
    {
        if (!options.DeleteSourceArchive)
        {
            _logger.LogInfo("Source archive deletion is disabled. Keeping source file.");
            return;
        }

        if (!File.Exists(options.Source))
        {
            _logger.LogInfo("Source archive file not found after update. Nothing to delete.");
            return;
        }

        var extension = Path.GetExtension(options.Source).ToLowerInvariant();
        if (extension != ".rar" && extension != ".zip")
        {
            _logger.LogInfo("Source is not a .rar/.zip archive. Skipping deletion.");
            return;
        }

        try
        {
            File.Delete(options.Source);
            _logger.LogInfo($"Deleted source archive: {options.Source}");
        }
        catch (Exception ex)
        {
            // Do not fail a successful deployment because archive cleanup failed.
            _logger.LogWarn($"Could not delete source archive '{options.Source}': {ex.Message}");
        }
    }
}

