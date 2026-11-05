using System.IO;
using Updater.Core.Models;

namespace Updater.Core.Services;

public class VersionLister
{
    private readonly ILogger _logger;

    public VersionLister(ILogger logger)
    {
        _logger = logger;
    }

    public void ListVersions(ListVersionsOptions options)
    {
        if (!Directory.Exists(options.BackupRoot))
        {
            _logger.LogWarn($"Backup root not found: {options.BackupRoot}");
            Console.WriteLine("No backups found.");
            return;
        }

        var backups = Directory.GetDirectories(options.BackupRoot)
            .Select(d => new
            {
                Path = d,
                Name = Path.GetFileName(d),
                LastWriteTime = Directory.GetLastWriteTime(d)
            })
            .OrderByDescending(b => b.LastWriteTime)
            .ToList();

        if (backups.Count == 0)
        {
            Console.WriteLine("No backups found.");
            return;
        }

        Console.WriteLine($"Backups in: {options.BackupRoot}");
        Console.WriteLine();
        Console.WriteLine($"{"Version",-50} {"Date",-20} {"Status"}");
        Console.WriteLine(new string('-', 75));

        var lastBackup = backups.First();
        foreach (var backup in backups)
        {
            var status = backup.Path == lastBackup.Path ? "(latest)" : "";
            Console.WriteLine($"{backup.Name,-50} {backup.LastWriteTime:yyyy-MM-dd HH:mm:ss} {status}");
        }

        Console.WriteLine();
        Console.WriteLine($"Total backups: {backups.Count}");
        Console.WriteLine($"Latest backup: {lastBackup.Name}");
    }
}

