using Updater.Core.Models;

namespace Updater.Cli;

public class CommandParser
{
    public ParsedCommand? Parse(string[] args)
    {
        if (args.Length == 0)
        {
            return null;
        }

        var command = args[0].ToLowerInvariant();

        // Help triggers
        if (command == "?" || command == "-?" || command == "--help" || command == "help")
        {
            return new ParsedCommand { Command = "help" };
        }

        return command switch
        {
            "update" => ParseUpdateCommand(args.Skip(1).ToArray()),
            "rollback" => ParseRollbackCommand(args.Skip(1).ToArray()),
            "list-versions" => ParseListVersionsCommand(args.Skip(1).ToArray()),
            _ => throw new ArgumentException($"Unknown command: {command}")
        };
    }

    private ParsedCommand ParseUpdateCommand(string[] args)
    {
        var options = new UpdateOptions();

        for (int i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            
            if (arg == "--help" || arg == "-?" || arg == "?")
            {
                return new ParsedCommand { Command = "help", SubCommand = "update" };
            }

            switch (arg.ToLowerInvariant())
            {
                case "--source":
                    if (i + 1 >= args.Length) throw new ArgumentException("--source requires a value");
                    options.Source = args[++i];
                    break;
                case "--dest":
                    if (i + 1 >= args.Length) throw new ArgumentException("--dest requires a value");
                    options.Destination = args[++i];
                    break;
                case "--backup-root":
                    if (i + 1 >= args.Length) throw new ArgumentException("--backup-root requires a value");
                    options.BackupRoot = args[++i];
                    break;
                case "--config-file":
                    if (i + 1 >= args.Length) throw new ArgumentException("--config-file requires a value");
                    options.ConfigFile = args[++i];
                    break;
                case "--inner-folder":
                    if (i + 1 >= args.Length) throw new ArgumentException("--inner-folder requires a value");
                    options.InnerFolder = args[++i];
                    break;
                case "--preserve-config":
                    if (i + 1 >= args.Length) throw new ArgumentException("--preserve-config requires a value");
                    options.PreserveConfig = bool.Parse(args[++i]);
                    break;
                case "--require-config":
                    if (i + 1 >= args.Length) throw new ArgumentException("--require-config requires a value");
                    options.RequireConfig = bool.Parse(args[++i]);
                    break;
                case "--dry-run":
                    options.DryRun = true;
                    break;
                case "--delete-source-archive":
                    if (i + 1 >= args.Length) throw new ArgumentException("--delete-source-archive requires a value");
                    options.DeleteSourceArchive = bool.Parse(args[++i]);
                    break;
                case "--winrar-path":
                    if (i + 1 >= args.Length) throw new ArgumentException("--winrar-path requires a value");
                    options.WinRarPath = args[++i];
                    break;
                default:
                    throw new ArgumentException($"Unknown option: {arg}");
            }
        }

        return new ParsedCommand { Command = "update", UpdateOptions = options };
    }

    private ParsedCommand ParseRollbackCommand(string[] args)
    {
        var options = new RollbackOptions();

        for (int i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            
            if (arg == "--help" || arg == "-?" || arg == "?")
            {
                return new ParsedCommand { Command = "help", SubCommand = "rollback" };
            }

            switch (arg.ToLowerInvariant())
            {
                case "--dest":
                    if (i + 1 >= args.Length) throw new ArgumentException("--dest requires a value");
                    options.Destination = args[++i];
                    break;
                case "--backup-root":
                    if (i + 1 >= args.Length) throw new ArgumentException("--backup-root requires a value");
                    options.BackupRoot = args[++i];
                    break;
                case "--last":
                    options.UseLast = true;
                    break;
                case "--to":
                    if (i + 1 >= args.Length) throw new ArgumentException("--to requires a value");
                    options.ToVersion = args[++i];
                    break;
                case "--keep-current-appsettings":
                    if (i + 1 >= args.Length) throw new ArgumentException("--keep-current-appsettings requires a value");
                    options.KeepCurrentAppSettings = bool.Parse(args[++i]);
                    break;
                default:
                    throw new ArgumentException($"Unknown option: {arg}");
            }
        }

        return new ParsedCommand { Command = "rollback", RollbackOptions = options };
    }

    private ParsedCommand ParseListVersionsCommand(string[] args)
    {
        var options = new ListVersionsOptions();

        for (int i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            
            if (arg == "--help" || arg == "-?" || arg == "?")
            {
                return new ParsedCommand { Command = "help", SubCommand = "list-versions" };
            }

            switch (arg.ToLowerInvariant())
            {
                case "--backup-root":
                    if (i + 1 >= args.Length) throw new ArgumentException("--backup-root requires a value");
                    options.BackupRoot = args[++i];
                    break;
                default:
                    throw new ArgumentException($"Unknown option: {arg}");
            }
        }

        return new ParsedCommand { Command = "list-versions", ListVersionsOptions = options };
    }
}

public class ParsedCommand
{
    public string Command { get; set; } = string.Empty;
    public string? SubCommand { get; set; }
    public UpdateOptions? UpdateOptions { get; set; }
    public RollbackOptions? RollbackOptions { get; set; }
    public ListVersionsOptions? ListVersionsOptions { get; set; }
}

