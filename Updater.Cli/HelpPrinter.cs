namespace Updater.Cli;

public class HelpPrinter
{
    public void PrintHelp(string? subCommand = null)
    {
        if (!string.IsNullOrEmpty(subCommand))
        {
            PrintCommandHelp(subCommand);
            return;
        }

        PrintFullHelp();
    }

    private void PrintFullHelp()
    {
        Console.WriteLine("Updater.Cli - Console Updater with Versioning, Locked-File Detection, and Rollback");
        Console.WriteLine();
        Console.WriteLine("DESCRIPTION");
        Console.WriteLine("  Updates an installation folder from a package source (ZIP, RAR, or folder).");
        Console.WriteLine("  Supports versioned backups, locked-file detection, and rollback functionality.");
        Console.WriteLine();
        Console.WriteLine("USAGE");
        Console.WriteLine("  Updater.Cli <command> [options]");
        Console.WriteLine();
        Console.WriteLine("COMMANDS");
        Console.WriteLine("  update              Deploy a package to the destination folder, creating a backup first");
        Console.WriteLine("  rollback            Restore destination folder from backups");
        Console.WriteLine("  list-versions       List available backup versions");
        Console.WriteLine("  help, --help, ?     Show this help message");
        Console.WriteLine();
        Console.WriteLine("GLOBAL OPTIONS");
        Console.WriteLine("  --help, -?, ?       Show help for the command");
        Console.WriteLine();
        Console.WriteLine("UPDATE COMMAND OPTIONS");
        Console.WriteLine("  --source <path>           Source package (ZIP, RAR, or folder)");
        Console.WriteLine("                            Default: .\\net7.0-windows.rar");
        Console.WriteLine("  --dest <path>             Destination folder to update");
        Console.WriteLine("                            Default: ..\\maquinasdispensadorasnuevosoftware");
        Console.WriteLine("  --backup-root <path>      Root folder for backups");
        Console.WriteLine("                            Default: .\\secur");
        Console.WriteLine("  --config-file <name>      Config file name to preserve");
        Console.WriteLine("                            Default: appsettings.json");
        Console.WriteLine("  --inner-folder <name>     Inner folder to deploy (auto-detect if not specified)");
        Console.WriteLine("  --preserve-config <bool>  Preserve config file from destination");
        Console.WriteLine("                            Default: true");
        Console.WriteLine("  --require-config <bool>  Fail if config file is missing");
        Console.WriteLine("                            Default: false");
        Console.WriteLine("  --dry-run                Show what would be done without making changes");
        Console.WriteLine("  --winrar-path <path>     Path to WinRAR.exe (for RAR extraction)");
        Console.WriteLine();
        Console.WriteLine("ROLLBACK COMMAND OPTIONS");
        Console.WriteLine("  --dest <path>             Destination folder to restore");
        Console.WriteLine("                            Default: ..\\maquinasdispensadorasnuevosoftware");
        Console.WriteLine("  --backup-root <path>      Root folder for backups");
        Console.WriteLine("                            Default: .\\secur");
        Console.WriteLine("  --last                    Rollback to most recent backup");
        Console.WriteLine("  --to <versionId>          Rollback to specific backup version");
        Console.WriteLine();
        Console.WriteLine("LIST-VERSIONS COMMAND OPTIONS");
        Console.WriteLine("  --backup-root <path>      Root folder for backups");
        Console.WriteLine("                            Default: .\\secur");
        Console.WriteLine();
        Console.WriteLine("DEFAULTS");
        Console.WriteLine("  --source:        .\\net7.0-windows.rar");
        Console.WriteLine("  --dest:          ..\\maquinasdispensadorasnuevosoftware");
        Console.WriteLine("  --backup-root:   .\\secur");
        Console.WriteLine("  --config-file:   appsettings.json");
        Console.WriteLine("  --inner-folder:  auto-detect");
        Console.WriteLine();
        Console.WriteLine("EXIT CODES");
        Console.WriteLine("  0  Success");
        Console.WriteLine("  1  Invalid arguments / validation error");
        Console.WriteLine("  2  IO error / locked files");
        Console.WriteLine("  3  Extraction error (zip/rar/folder expansion)");
        Console.WriteLine("  4  Deploy/backup failure");
        Console.WriteLine("  5  Rollback failure");
        Console.WriteLine();
        Console.WriteLine("EXAMPLES");
        Console.WriteLine("  Updater.Cli ?");
        Console.WriteLine("  Updater.Cli --help");
        Console.WriteLine();
        Console.WriteLine("  Updater.Cli update");
        Console.WriteLine("  Updater.Cli update --source update.zip");
        Console.WriteLine("  Updater.Cli update --source C:\\releases\\build.rar --inner-folder net7.0-windows");
        Console.WriteLine("  Updater.Cli update --source C:\\releases\\staging\\ --dest C:\\App\\Install");
        Console.WriteLine("  Updater.Cli update --dry-run --source update.zip");
        Console.WriteLine();
        Console.WriteLine("  Updater.Cli list-versions --backup-root .\\secur");
        Console.WriteLine();
        Console.WriteLine("  Updater.Cli rollback --last");
        Console.WriteLine("  Updater.Cli rollback --to maquinasdispensadorasnuevosoftware_20251225_221501");
        Console.WriteLine();
        Console.WriteLine("RAR EXTRACTION");
        Console.WriteLine("  RAR extraction requires WinRAR. The tool will look for WinRAR.exe in:");
        Console.WriteLine("    - PATH environment variable");
        Console.WriteLine("    - C:\\Program Files\\WinRAR\\WinRAR.exe");
        Console.WriteLine("    - C:\\Program Files (x86)\\WinRAR\\WinRAR.exe");
        Console.WriteLine("  Or specify --winrar-path option.");
    }

    private void PrintCommandHelp(string subCommand)
    {
        switch (subCommand.ToLowerInvariant())
        {
            case "update":
                Console.WriteLine("UPDATE COMMAND");
                Console.WriteLine("  Deploy a package to the destination folder, creating a backup first.");
                Console.WriteLine();
                Console.WriteLine("USAGE");
                Console.WriteLine("  Updater.Cli update [options]");
                Console.WriteLine();
                Console.WriteLine("OPTIONS");
                Console.WriteLine("  --source <path>           Source package (ZIP, RAR, or folder)");
                Console.WriteLine("                            Default: .\\net7.0-windows.rar");
                Console.WriteLine("  --dest <path>             Destination folder to update");
                Console.WriteLine("                            Default: ..\\maquinasdispensadorasnuevosoftware");
                Console.WriteLine("  --backup-root <path>      Root folder for backups");
                Console.WriteLine("                            Default: .\\secur");
                Console.WriteLine("  --config-file <name>      Config file name to preserve");
                Console.WriteLine("                            Default: appsettings.json");
                Console.WriteLine("  --inner-folder <name>     Inner folder to deploy (auto-detect if not specified)");
                Console.WriteLine("  --preserve-config <bool>  Preserve config file from destination");
                Console.WriteLine("                            Default: true");
                Console.WriteLine("  --require-config <bool>  Fail if config file is missing");
                Console.WriteLine("                            Default: false");
                Console.WriteLine("  --dry-run                Show what would be done without making changes");
                Console.WriteLine("  --winrar-path <path>     Path to WinRAR.exe (for RAR extraction)");
                Console.WriteLine();
                Console.WriteLine("EXAMPLES");
                Console.WriteLine("  Updater.Cli update");
                Console.WriteLine("  Updater.Cli update --source update.zip");
                Console.WriteLine("  Updater.Cli update --source C:\\releases\\build.rar --inner-folder net7.0-windows");
                Console.WriteLine("  Updater.Cli update --dry-run --source update.zip");
                break;

            case "rollback":
                Console.WriteLine("ROLLBACK COMMAND");
                Console.WriteLine("  Restore destination folder from backups.");
                Console.WriteLine();
                Console.WriteLine("USAGE");
                Console.WriteLine("  Updater.Cli rollback [options]");
                Console.WriteLine();
                Console.WriteLine("OPTIONS");
                Console.WriteLine("  --dest <path>             Destination folder to restore");
                Console.WriteLine("                            Default: ..\\maquinasdispensadorasnuevosoftware");
                Console.WriteLine("  --backup-root <path>      Root folder for backups");
                Console.WriteLine("                            Default: .\\secur");
                Console.WriteLine("  --last                    Rollback to most recent backup");
                Console.WriteLine("  --to <versionId>          Rollback to specific backup version");
                Console.WriteLine();
                Console.WriteLine("EXAMPLES");
                Console.WriteLine("  Updater.Cli rollback --last");
                Console.WriteLine("  Updater.Cli rollback --to maquinasdispensadorasnuevosoftware_20251225_221501");
                break;

            case "list-versions":
                Console.WriteLine("LIST-VERSIONS COMMAND");
                Console.WriteLine("  List available backup versions found in backup root, sorted by date.");
                Console.WriteLine();
                Console.WriteLine("USAGE");
                Console.WriteLine("  Updater.Cli list-versions [options]");
                Console.WriteLine();
                Console.WriteLine("OPTIONS");
                Console.WriteLine("  --backup-root <path>      Root folder for backups");
                Console.WriteLine("                            Default: .\\secur");
                Console.WriteLine();
                Console.WriteLine("EXAMPLES");
                Console.WriteLine("  Updater.Cli list-versions --backup-root .\\secur");
                break;

            default:
                PrintFullHelp();
                break;
        }
    }
}

