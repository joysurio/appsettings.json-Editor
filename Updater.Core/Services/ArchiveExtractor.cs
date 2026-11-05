using System.Diagnostics;
using System.IO.Compression;

namespace Updater.Core.Services;

public class ArchiveExtractor
{
    private readonly ILogger _logger;

    public ArchiveExtractor(ILogger logger)
    {
        _logger = logger;
    }

    public string Extract(string sourcePath, string destinationPath, string? winRarPath = null)
    {
        if (Directory.Exists(sourcePath))
        {
            // Source is already a folder - copy it to destination
            _logger.LogInfo($"Copying folder: {sourcePath}");
            
            if (Directory.Exists(destinationPath))
            {
                Directory.Delete(destinationPath, true);
            }
            Directory.CreateDirectory(destinationPath);

            // Copy all files and subdirectories
            CopyDirectory(sourcePath, destinationPath);
            
            _logger.LogInfo($"Folder copy completed to: {destinationPath}");
            return destinationPath;
        }

        if (!File.Exists(sourcePath))
        {
            throw new FileNotFoundException($"Source not found: {sourcePath}");
        }

        var extension = Path.GetExtension(sourcePath).ToLowerInvariant();

        return extension switch
        {
            ".zip" => ExtractZip(sourcePath, destinationPath),
            ".rar" => ExtractRar(sourcePath, destinationPath, winRarPath),
            _ => throw new NotSupportedException($"Unsupported archive format: {extension}")
        };
    }

    private string ExtractZip(string zipPath, string destinationPath)
    {
        _logger.LogInfo($"Extracting ZIP archive: {zipPath}");
        
        if (Directory.Exists(destinationPath))
        {
            Directory.Delete(destinationPath, true);
        }
        Directory.CreateDirectory(destinationPath);

        ZipFile.ExtractToDirectory(zipPath, destinationPath);
        
        _logger.LogInfo($"ZIP extraction completed to: {destinationPath}");
        return destinationPath;
    }

    private string ExtractRar(string rarPath, string destinationPath, string? winRarPath)
    {
        _logger.LogInfo($"Extracting RAR archive: {rarPath}");

        // Try to find WinRAR.exe
        var winRarExe = FindWinRar(winRarPath);
        if (string.IsNullOrEmpty(winRarExe))
        {
            throw new FileNotFoundException(
                "WinRAR.exe not found. Please install WinRAR and ensure WinRAR.exe is in PATH, " +
                "or specify --winrar-path option.");
        }

        if (Directory.Exists(destinationPath))
        {
            Directory.Delete(destinationPath, true);
        }
        Directory.CreateDirectory(destinationPath);

        // Ensure destination path ends with backslash for WinRAR
        var destinationPathWithBackslash = destinationPath.TrimEnd('\\', '/') + "\\";
        
        var processStartInfo = new ProcessStartInfo
        {
            FileName = winRarExe,
            Arguments = $"x \"{rarPath}\" \"{destinationPathWithBackslash}\" -y",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = Process.Start(processStartInfo);
        if (process == null)
        {
            throw new InvalidOperationException("Failed to start WinRAR.exe process");
        }

        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            var error = process.StandardError.ReadToEnd();
            throw new InvalidOperationException($"WinRAR.exe extraction failed with exit code {process.ExitCode}: {error}");
        }

        _logger.LogInfo($"RAR extraction completed to: {destinationPath}");
        return destinationPath;
    }

    private string? FindWinRar(string? customPath)
    {
        if (!string.IsNullOrEmpty(customPath) && File.Exists(customPath))
        {
            return customPath;
        }

        // Check PATH
        var pathDirs = Environment.GetEnvironmentVariable("PATH")?.Split(Path.PathSeparator) ?? Array.Empty<string>();
        foreach (var dir in pathDirs)
        {
            var winRarPath = Path.Combine(dir, "WinRAR.exe");
            if (File.Exists(winRarPath))
            {
                return winRarPath;
            }
        }

        // Check common installation locations
        var commonPaths = new[]
        {
            @"C:\Program Files\WinRAR\WinRAR.exe",
            @"C:\Program Files (x86)\WinRAR\WinRAR.exe"
        };

        foreach (var path in commonPaths)
        {
            if (File.Exists(path))
            {
                return path;
            }
        }

        return null;
    }

    private void CopyDirectory(string sourceDir, string destDir)
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
}

