using System.IO;
using Updater.Core.Models;

namespace Updater.Core.Services;

public class RollbackService
{
    private readonly ILogger _logger;
    private readonly FileLockDetector _lockDetector;

    public RollbackService(ILogger logger, FileLockDetector lockDetector)
    {
        _logger = logger;
        _lockDetector = lockDetector;
    }

    public void Rollback(RollbackOptions options)
    {
        _logger.LogInfo("Starting rollback process...");

        ValidateOptions(options);

        // Check if backup root exists and has backups
        if (!Directory.Exists(options.BackupRoot))
        {
            throw new InvalidOperationException(
                $"Backup root directory does not exist: {options.BackupRoot}\n" +
                "No backups are available. You need to perform at least one update before you can rollback.\n" +
                "Use 'list-versions' command to see available backups.");
        }

        // Determine which backup to use
        string backupPath;
        if (options.UseLast)
        {
            backupPath = FindLastBackup(options.BackupRoot);
            if (string.IsNullOrEmpty(backupPath))
            {
                throw new InvalidOperationException(
                    $"No backups found in backup root: {options.BackupRoot}\n" +
                    "The backup directory exists but is empty. You need to perform at least one update before you can rollback.\n" +
                    "Use 'list-versions' command to see available backups.");
            }
            _logger.LogInfo($"Using last backup: {backupPath}");
        }
        else if (!string.IsNullOrEmpty(options.ToVersion))
        {
            backupPath = Path.Combine(options.BackupRoot, options.ToVersion);
            if (!Directory.Exists(backupPath))
            {
                // Try to provide helpful suggestions
                var availableBackups = Directory.GetDirectories(options.BackupRoot)
                    .Select(d => Path.GetFileName(d))
                    .ToList();
                
                var errorMsg = $"Backup version not found: {options.ToVersion}\n" +
                              $"Backup root: {options.BackupRoot}";
                
                if (availableBackups.Count > 0)
                {
                    errorMsg += $"\n\nAvailable backups:\n  - {string.Join("\n  - ", availableBackups.Take(10))}";
                    if (availableBackups.Count > 10)
                    {
                        errorMsg += $"\n  ... and {availableBackups.Count - 10} more";
                    }
                    errorMsg += "\n\nUse 'list-versions' command to see all available backups.";
                }
                else
                {
                    errorMsg += "\n\nNo backups found in the backup root.";
                }
                
                throw new DirectoryNotFoundException(errorMsg);
            }
            _logger.LogInfo($"Using specified backup: {backupPath}");
        }
        else
        {
            throw new ArgumentException("Either --last or --to <version> must be specified");
        }

        // Detect locked files in destination
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

        // Move current destination to backup (so rollback is reversible)
        string? currentBackupPath = null;
        var tempPreserveFolder = Path.Combine(Path.GetTempPath(), $"rollback_preserve_{Guid.NewGuid():N}");
        string? preservedConfigFilePath = null;
        string? preservedSecurFolderPath = null;
        // When not keeping current appsettings: dated archive name in temp, then copied into final destination after restore
        // (must not live only under destination before MoveDirectory or it would be moved away with the old install).
        string? archivedCurrentConfigFileName = null;

        Directory.CreateDirectory(tempPreserveFolder);

        // Preserve selected current artifacts before destination is moved away.
        if (Directory.Exists(options.Destination))
        {
            if (options.KeepCurrentAppSettings)
            {
                var currentConfigPath = Path.Combine(options.Destination, options.ConfigFileName);
                if (File.Exists(currentConfigPath))
                {
                    preservedConfigFilePath = Path.Combine(tempPreserveFolder, options.ConfigFileName);
                    File.Copy(currentConfigPath, preservedConfigFilePath, overwrite: true);
                    _logger.LogInfo($"Preserved current config file (will replace backup copy after restore): {currentConfigPath}");
                }
            }
            else
            {
                // After restore, active config comes from the backup snapshot (under Backup Root / "Secur" store).
                // Save current config to a dated file first; copy into install folder after restore so it survives the move.
                var currentConfigPath = Path.Combine(options.Destination, options.ConfigFileName);
                if (File.Exists(currentConfigPath))
                {
                    var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                    var ext = Path.GetExtension(options.ConfigFileName);
                    var baseName = Path.GetFileNameWithoutExtension(options.ConfigFileName);
                    archivedCurrentConfigFileName = $"{baseName}_{stamp}{ext}";
                    var tempArchive = Path.Combine(tempPreserveFolder, archivedCurrentConfigFileName);
                    File.Copy(currentConfigPath, tempArchive, overwrite: true);
                    _logger.LogInfo($"Archived current config before restore (will place {archivedCurrentConfigFileName} in destination after rollback): {currentConfigPath}");
                }
            }

            // Secur under destination is always preserved (never replaced by backup copy alone).
            var currentSecurPath = Path.Combine(options.Destination, options.SecurFolderName);
            if (Directory.Exists(currentSecurPath))
            {
                preservedSecurFolderPath = Path.Combine(tempPreserveFolder, options.SecurFolderName);
                CopyDirectory(currentSecurPath, preservedSecurFolderPath);
                _logger.LogInfo($"Preserved current folder: {currentSecurPath}");
            }
        }

        if (Directory.Exists(options.Destination))
        {
            currentBackupPath = CreateBackupFromCurrent(options);
        }

        // Move selected backup to destination
        _logger.LogInfo($"Restoring backup to destination: {options.Destination}");
        DirectoryHelper.MoveDirectory(backupPath, options.Destination, _logger);

        // Re-apply preserved artifacts from the previous main/current version.
        if (!string.IsNullOrEmpty(preservedConfigFilePath) && File.Exists(preservedConfigFilePath))
        {
            var targetConfigPath = Path.Combine(options.Destination, options.ConfigFileName);
            File.Copy(preservedConfigFilePath, targetConfigPath, overwrite: true);
            _logger.LogInfo($"Restored preserved config file into rollback destination: {targetConfigPath}");
        }

        if (!string.IsNullOrEmpty(preservedSecurFolderPath) && Directory.Exists(preservedSecurFolderPath))
        {
            var targetSecurPath = Path.Combine(options.Destination, options.SecurFolderName);
            try
            {
                if (Directory.Exists(targetSecurPath))
                {
                    Directory.Delete(targetSecurPath, recursive: true);
                }

                // Copy (not move) so temp cleanup always works and we don't risk losing Secur if a move fails mid-way.
                CopyDirectory(preservedSecurFolderPath, targetSecurPath);
                _logger.LogInfo($"Restored preserved folder into rollback destination: {targetSecurPath}");
            }
            catch (Exception ex)
            {
                _logger.LogWarn($"Failed to restore preserved Secur folder into destination: {ex.Message}");
            }
        }

        // If unchecked, appsettings must come from Secur store/snapshot (not from current main version).
        if (!options.KeepCurrentAppSettings)
        {
            TryRestoreConfigFromSecurStore(options);
        }

        // Place dated archive of previous config next to appsettings.json (when using backup's config).
        if (!options.KeepCurrentAppSettings && !string.IsNullOrEmpty(archivedCurrentConfigFileName))
        {
            var srcArchive = Path.Combine(tempPreserveFolder, archivedCurrentConfigFileName);
            if (File.Exists(srcArchive))
            {
                var destArchive = Path.Combine(options.Destination, archivedCurrentConfigFileName);
                File.Copy(srcArchive, destArchive, overwrite: true);
                _logger.LogInfo($"Placed saved copy of previous config in install folder: {destArchive}");
            }
        }

        try
        {
            if (Directory.Exists(tempPreserveFolder))
            {
                Directory.Delete(tempPreserveFolder, recursive: true);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarn($"Failed to cleanup rollback preserve temp folder: {ex.Message}");
        }

        _logger.LogInfo("Rollback completed successfully!");
        _logger.LogInfo($"  - Destination restored: {options.Destination}");
        _logger.LogInfo($"  - Backup used: {backupPath}");
        if (currentBackupPath != null)
        {
            _logger.LogInfo($"  - Previous version backed up to: {currentBackupPath}");
        }
    }

    private void ValidateOptions(RollbackOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Destination))
        {
            throw new ArgumentException("Destination path cannot be empty");
        }

        if (string.IsNullOrWhiteSpace(options.BackupRoot))
        {
            throw new ArgumentException("Backup root path cannot be empty");
        }

        // Note: We don't check if backup root exists here anymore
        // We check it in Rollback() method to provide better error messages
    }

    private string FindLastBackup(string backupRoot)
    {
        var backups = Directory.GetDirectories(backupRoot)
            .Select(d => new
            {
                Path = d,
                Name = Path.GetFileName(d),
                LastWriteTime = Directory.GetLastWriteTime(d)
            })
            .OrderByDescending(b => b.LastWriteTime)
            .ToList();

        return backups.FirstOrDefault()?.Path ?? string.Empty;
    }

    private string CreateBackupFromCurrent(RollbackOptions options)
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

        _logger.LogInfo($"Backing up current version before rollback: {backupPath}");
        DirectoryHelper.MoveDirectory(options.Destination, backupPath, _logger);
        
        return backupPath;
    }

    private static void CopyDirectory(string sourceDir, string destDir)
    {
        Directory.CreateDirectory(destDir);

        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var destFile = Path.Combine(destDir, Path.GetFileName(file));
            File.Copy(file, destFile, overwrite: true);
        }

        foreach (var dir in Directory.GetDirectories(sourceDir))
        {
            var destSubDir = Path.Combine(destDir, Path.GetFileName(dir));
            CopyDirectory(dir, destSubDir);
        }
    }

    private void TryRestoreConfigFromSecurStore(RollbackOptions options)
    {
        try
        {
            var destinationConfigPath = Path.Combine(options.Destination, options.ConfigFileName);
            var securPath = Path.Combine(options.Destination, options.SecurFolderName);
            if (!Directory.Exists(securPath))
            {
                _logger.LogWarn($"Secur folder not found while restoring config from Secur store: {securPath}");
                return;
            }

            // Prefer exact file name first.
            var exactMatch = Path.Combine(securPath, options.ConfigFileName);
            string? sourceConfigFromSecur = null;
            if (File.Exists(exactMatch))
            {
                sourceConfigFromSecur = exactMatch;
            }
            else
            {
                // Fallback to latest appsettings*.json in Secur tree.
                var baseName = Path.GetFileNameWithoutExtension(options.ConfigFileName);
                var ext = Path.GetExtension(options.ConfigFileName);
                sourceConfigFromSecur = Directory
                    .GetFiles(securPath, $"{baseName}*{ext}", SearchOption.AllDirectories)
                    .OrderByDescending(File.GetLastWriteTime)
                    .FirstOrDefault();
            }

            if (string.IsNullOrEmpty(sourceConfigFromSecur) || !File.Exists(sourceConfigFromSecur))
            {
                _logger.LogWarn($"Could not find {options.ConfigFileName} in Secur folder. Keeping restored backup appsettings as-is.");
                return;
            }

            File.Copy(sourceConfigFromSecur, destinationConfigPath, overwrite: true);
            _logger.LogInfo($"Applied appsettings from Secur store: {sourceConfigFromSecur} -> {destinationConfigPath}");
        }
        catch (Exception ex)
        {
            _logger.LogWarn($"Failed to apply appsettings from Secur store: {ex.Message}");
        }
    }
}

