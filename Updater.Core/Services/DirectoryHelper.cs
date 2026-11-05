using System;
using System.IO;

namespace Updater.Core.Services;

public static class DirectoryHelper
{
    private const int RetryCount = 5;
    private const int RetryDelayMs = 300;

    /// <summary>
    /// Moves a directory from source to destination, handling cross-volume moves by copying and deleting.
    /// </summary>
    public static void MoveDirectory(string sourcePath, string destinationPath, ILogger? logger = null)
    {
        if (!Directory.Exists(sourcePath))
        {
            throw new DirectoryNotFoundException($"Source directory not found: {sourcePath}");
        }

        // Normalize paths - remove trailing separators and get full paths
        var normalizedSource = Path.GetFullPath(sourcePath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var normalizedDest = Path.GetFullPath(destinationPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        // Check if destination is a subdirectory of source (would cause infinite loop)
        if (normalizedDest.StartsWith(normalizedSource + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
            normalizedDest.StartsWith(normalizedSource + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Destination cannot be a subdirectory of source: {normalizedDest}");
        }

        if (Directory.Exists(normalizedDest))
        {
            throw new InvalidOperationException($"Destination directory already exists: {normalizedDest}");
        }

        // Ensure destination parent directory exists
        var destParent = Path.GetDirectoryName(normalizedDest);
        if (!string.IsNullOrEmpty(destParent) && !Directory.Exists(destParent))
        {
            Directory.CreateDirectory(destParent);
        }

        // Check if source and destination are on the same volume
        var sourceRoot = Path.GetPathRoot(normalizedSource);
        var destRoot = Path.GetPathRoot(normalizedDest);

        if (string.Equals(sourceRoot, destRoot, StringComparison.OrdinalIgnoreCase))
        {
            // Same volume - use fast move operation
            try
            {
                ExecuteWithRetry(
                    () => Directory.Move(normalizedSource, normalizedDest),
                    logger,
                    $"Directory.Move {normalizedSource} -> {normalizedDest}");
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                // Fallback to copy+delete when move fails (access denied, locked files, etc.)
                logger?.LogWarn($"Directory.Move failed, falling back to copy+delete: {ex.Message}");
                CopyDirectory(normalizedSource, normalizedDest, logger);
                TryDeleteSourceDirectory(normalizedSource, logger);
            }
        }
        else
        {
            // Different volumes - copy then delete
            logger?.LogInfo($"Moving across volumes: {sourceRoot} -> {destRoot} (using copy + delete)");
            CopyDirectory(normalizedSource, normalizedDest, logger);
            TryDeleteSourceDirectory(normalizedSource, logger);
        }
    }

    private static void CopyDirectory(string sourceDir, string destDir, ILogger? logger = null)
    {
        Directory.CreateDirectory(destDir);

        // Copy all files
        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var destFile = Path.Combine(destDir, Path.GetFileName(file));
            ExecuteWithRetry(
                () => File.Copy(file, destFile, overwrite: true),
                logger,
                $"File.Copy {file} -> {destFile}");
        }

        // Recursively copy subdirectories
        foreach (var dir in Directory.GetDirectories(sourceDir))
        {
            var destSubDir = Path.Combine(destDir, Path.GetFileName(dir));
            CopyDirectory(dir, destSubDir, logger);
        }
    }

    private static void TryDeleteSourceDirectory(string sourceDir, ILogger? logger)
    {
        try
        {
            if (Directory.Exists(sourceDir))
            {
                Directory.Delete(sourceDir, true);
            }
        }
        catch (Exception ex)
        {
            // Deployment already succeeded; keep running and let temp cleanup handle leftovers later.
            logger?.LogWarn($"Could not delete source directory after copy fallback: {sourceDir}. {ex.Message}");
        }
    }

    private static void ExecuteWithRetry(Action action, ILogger? logger, string operation)
    {
        for (var attempt = 1; attempt <= RetryCount; attempt++)
        {
            try
            {
                action();
                return;
            }
            catch (Exception ex) when ((ex is IOException || ex is UnauthorizedAccessException) && attempt < RetryCount)
            {
                logger?.LogWarn($"{operation} failed (attempt {attempt}/{RetryCount}): {ex.Message}. Retrying...");
                Thread.Sleep(RetryDelayMs * attempt);
            }
        }

        // Last attempt lets the original exception bubble with accurate context.
        action();
    }
}

