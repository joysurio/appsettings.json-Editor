using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;

namespace AppSettingsEditor;

public class LogAnalyzer
{
    public static string? ExtractDateFromFilename(string filename)
    {
        // Extract first YYYYMMDD-like token if present
        var match = Regex.Match(filename, @"(\d{8})");
        return match.Success ? match.Groups[1].Value : null;
    }

    public static void ExtractZip(string zipPath, string extractPath)
    {
        if (Directory.Exists(extractPath))
        {
            Directory.Delete(extractPath, true);
        }
        Directory.CreateDirectory(extractPath);
        ZipFile.ExtractToDirectory(zipPath, extractPath);
    }

    public static AnalysisResult AnalyzeLogs(string extractPath, string dateStr)
    {
        var result = new AnalysisResult
        {
            Date = dateStr,
            Machines = new List<MachineAnalysis>()
        };

        // Find all directories containing .log or .txt files
        var logDirs = Directory.GetDirectories(extractPath, "*", SearchOption.AllDirectories)
            .Where(d =>
                Directory.GetFiles(d, "*.log", SearchOption.TopDirectoryOnly).Any() ||
                Directory.GetFiles(d, "*.txt", SearchOption.TopDirectoryOnly).Any())
            .ToList();

        foreach (var logDir in logDirs)
        {
            var machineName = Path.GetFileName(logDir);
            var logFiles = Directory.GetFiles(logDir, "*.*", SearchOption.TopDirectoryOnly)
                .Where(f =>
                    string.Equals(Path.GetExtension(f), ".log", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(Path.GetExtension(f), ".txt", StringComparison.OrdinalIgnoreCase))
                .ToArray();

            if (logFiles.Length == 0) continue;

            var machineAnalysis = new MachineAnalysis
            {
                MachineName = machineName,
                LogFiles = new List<string>(logFiles.Select(f => Path.GetFileName(f)))
            };

            // Analyze each log file
            foreach (var logFile in logFiles)
            {
                AnalyzeLogFile(logFile, machineAnalysis);
            }

            // Determine priority
            DeterminePriority(machineAnalysis);

            result.Machines.Add(machineAnalysis);
            result.TotalLogFiles += logFiles.Length;
        }

        // Count machines by priority
        result.MachinesWithCriticalErrors = result.Machines.Count(m => m.Priority == "P0");
        result.MachinesWithWarnings = result.Machines.Count(m => m.Priority == "P1");
        result.MachinesOperatingNormally = result.Machines.Count(m => m.Priority == "P2");

        return result;
    }

    private static void AnalyzeLogFile(string logFile, MachineAnalysis machine)
    {
        var lines = File.ReadAllLines(logFile);
        var content = File.ReadAllText(logFile);

        // Extract machine ID from filename or content
        var filename = Path.GetFileName(logFile);
        var machineIdMatch = Regex.Match(filename, @"(\d{5}[A-Z]?|\w+-\d+)");
        if (machineIdMatch.Success && string.IsNullOrEmpty(machine.MachineId))
        {
            machine.MachineId = machineIdMatch.Groups[1].Value;
        }

        // Count error types
        foreach (var line in lines)
        {
            if (line.Contains("[Error]"))
            {
                machine.TotalErrors++;

                // Critical errors
                if (line.Contains("HomingKo") || line.Contains("Homing Ko"))
                {
                    machine.CriticalErrors.Add(new ErrorEntry { Type = "HomingKo", Message = line, Timestamp = ExtractTimestamp(line) });
                }
                if (line.Contains("ExcessivePosDeviation") || line.Contains("ErrorX: 0010h") || line.Contains("ErrorY: 0010h"))
                {
                    machine.CriticalErrors.Add(new ErrorEntry { Type = "PositionDeviation", Message = line, Timestamp = ExtractTimestamp(line) });
                }
                if (line.Contains("OverLoad") || line.Contains("ErrorX: 0030h") || line.Contains("ErrorY: 0030h"))
                {
                    machine.CriticalErrors.Add(new ErrorEntry { Type = "OverLoad", Message = line, Timestamp = ExtractTimestamp(line) });
                }
                if (line.Contains("ALARMA") && line.Contains("Averia máquina"))
                {
                    machine.CriticalErrors.Add(new ErrorEntry { Type = "MachineAlarm", Message = line, Timestamp = ExtractTimestamp(line) });
                }

                // Database errors
                if (line.Contains("Connection Timeout Expired") || line.Contains("timeout expired"))
                {
                    machine.DatabaseErrors.Add(new ErrorEntry { Type = "ConnectionTimeout", Message = line, Timestamp = ExtractTimestamp(line) });
                }
                if (line.Contains("Connection pool") && line.Contains("max pool size"))
                {
                    machine.DatabaseErrors.Add(new ErrorEntry { Type = "ConnectionPoolExhaustion", Message = line, Timestamp = ExtractTimestamp(line) });
                }
                if (line.Contains("Host desconocido") || line.Contains("Unknown host"))
                {
                    machine.DatabaseErrors.Add(new ErrorEntry { Type = "UnknownHost", Message = line, Timestamp = ExtractTimestamp(line) });
                }
                if (line.Contains("transport-level error") || (line.Contains("TCP Provider") && line.Contains("error: 0")))
                {
                    machine.DatabaseErrors.Add(new ErrorEntry { Type = "TransportLevelError", Message = line, Timestamp = ExtractTimestamp(line) });
                }
                if (line.Contains("pre-login handshake") || (line.Contains("Pre-Login") && line.Contains("initialization")))
                {
                    machine.DatabaseErrors.Add(new ErrorEntry { Type = "PreLoginTimeout", Message = line, Timestamp = ExtractTimestamp(line) });
                }
                if (line.Contains("FOREIGN KEY constraint"))
                {
                    machine.DatabaseErrors.Add(new ErrorEntry { Type = "ForeignKeyViolation", Message = line, Timestamp = ExtractTimestamp(line) });
                }

                // Picking errors
                if (line.Contains("cogida en el intento 2") || line.Contains("cogida en el intento 3") || line.Contains("cogida en el intento 4"))
                {
                    machine.PickingErrors.Add(new ErrorEntry { Type = "PickingAttempt", Message = line, Timestamp = ExtractTimestamp(line) });
                }
                if (line.Contains("Prenda no detectada") || line.Contains("Garment not detected"))
                {
                    machine.PickingErrors.Add(new ErrorEntry { Type = "GarmentNotDetected", Message = line, Timestamp = ExtractTimestamp(line) });
                }
                if (line.Contains("Imposible recoger prenda") || line.Contains("Impossible to pick"))
                {
                    machine.PickingErrors.Add(new ErrorEntry { Type = "ImpossibleToPick", Message = line, Timestamp = ExtractTimestamp(line) });
                }
                if (line.Contains("מחסן ריק") || line.Contains("Empty storage"))
                {
                    machine.PickingErrors.Add(new ErrorEntry { Type = "EmptyStorage", Message = line, Timestamp = ExtractTimestamp(line) });
                }

                // Sensor errors
                if (line.Contains("sensor bloqueado") || line.Contains("sensor blocked"))
                {
                    machine.SensorErrors.Add(new ErrorEntry { Type = "SensorBlocked", Message = line, Timestamp = ExtractTimestamp(line) });
                }

                // Collection errors
                if (line.Contains("Diferencia al devolver") || line.Contains("Difference when returning"))
                {
                    machine.CollectionErrors.Add(new ErrorEntry { Type = "CollectionDifference", Message = line, Timestamp = ExtractTimestamp(line) });
                }
            }
        }

        // Extract last timestamp
        var lastLine = lines.LastOrDefault();
        if (lastLine != null)
        {
            machine.LastLogTimestamp = ExtractTimestamp(lastLine);
        }
    }

    private static string ExtractTimestamp(string line)
    {
        var match = Regex.Match(line, @"(\d{4}-\d{2}-\d{2}\s+\d{2}:\d{2}:\d{2})");
        return match.Success ? match.Groups[1].Value : "";
    }

    private static void DeterminePriority(MachineAnalysis machine)
    {
        if (machine.CriticalErrors.Count > 0)
        {
            machine.Priority = "P0";
            machine.Status = "CRITICAL";
        }
        else if (machine.DatabaseErrors.Count > 5 || machine.PickingErrors.Count > 10 || 
                 machine.SensorErrors.Count > 5 || machine.CollectionErrors.Count > 10)
        {
            machine.Priority = "P1";
            machine.Status = "WARNING";
        }
        else if (machine.TotalErrors > 0)
        {
            machine.Priority = "P2";
            machine.Status = "OK";
        }
        else
        {
            machine.Priority = "P2";
            machine.Status = "OK";
        }
    }
}

public class AnalysisResult
{
    public string Date { get; set; }
    public List<MachineAnalysis> Machines { get; set; }
    public int TotalLogFiles { get; set; }
    public int MachinesWithCriticalErrors { get; set; }
    public int MachinesWithWarnings { get; set; }
    public int MachinesOperatingNormally { get; set; }
}

public class MachineAnalysis
{
    public string MachineName { get; set; }
    public string MachineId { get; set; }
    public List<string> LogFiles { get; set; }
    public string LastLogTimestamp { get; set; }
    public int TotalErrors { get; set; }
    public List<ErrorEntry> CriticalErrors { get; set; } = new List<ErrorEntry>();
    public List<ErrorEntry> DatabaseErrors { get; set; } = new List<ErrorEntry>();
    public List<ErrorEntry> PickingErrors { get; set; } = new List<ErrorEntry>();
    public List<ErrorEntry> SensorErrors { get; set; } = new List<ErrorEntry>();
    public List<ErrorEntry> CollectionErrors { get; set; } = new List<ErrorEntry>();
    public string Priority { get; set; }
    public string Status { get; set; }
}

public class ErrorEntry
{
    public string Type { get; set; }
    public string Message { get; set; }
    public string Timestamp { get; set; }
}
