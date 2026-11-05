using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace AppSettingsEditor;

public class MarkdownGenerator
{
    public static void GenerateSummary(AnalysisResult analysis, string outputFile, string dateStr)
    {
        using (var writer = new StreamWriter(outputFile))
        {
            writer.WriteLine($"# Log Files Summary - {FormatDate(dateStr)}");
            writer.WriteLine();
            writer.WriteLine("## Zip File Information");
            writer.WriteLine($"- **Date:** {FormatDate(dateStr)}");
            writer.WriteLine($"- **Total Machines:** {analysis.Machines.Count} machine locations");
            writer.WriteLine($"- **Total Log Files:** {analysis.TotalLogFiles} error log files");
            writer.WriteLine();

            writer.WriteLine("## Machine List");
            writer.WriteLine();

            // Machines with logs
            var machinesWithLogs = analysis.Machines.Where(m => m.LogFiles.Count > 0).ToList();
            var machinesWithoutLogs = analysis.Machines.Where(m => m.LogFiles.Count == 0).ToList();

            writer.WriteLine("### Machines WITH Logs:");
            writer.WriteLine();

            foreach (var machine in machinesWithLogs)
            {
                writer.WriteLine($"{machinesWithLogs.IndexOf(machine) + 1}. **{machine.MachineName}**{(string.IsNullOrEmpty(machine.MachineId) ? "" : $" (Machine: {machine.MachineId})")}");
                writer.WriteLine($"   - Logs: {dateStr}");
                
                if (machine.CriticalErrors.Count > 0 || machine.DatabaseErrors.Count > 0 || 
                    machine.PickingErrors.Count > 0 || machine.SensorErrors.Count > 0 || 
                    machine.CollectionErrors.Count > 0)
                {
                    writer.WriteLine("   - **Critical Errors Found:**");
                    
                    if (machine.CriticalErrors.Count > 0)
                    {
                        var homingErrors = machine.CriticalErrors.Where(e => e.Type == "HomingKo").ToList();
                        var posErrors = machine.CriticalErrors.Where(e => e.Type == "PositionDeviation").ToList();
                        var overloadErrors = machine.CriticalErrors.Where(e => e.Type == "OverLoad").ToList();
                        
                        if (homingErrors.Count > 0)
                            writer.WriteLine($"     - **HomingKo error** at {homingErrors.First().Timestamp} - Machine alarm causing application restart");
                        if (posErrors.Count > 0)
                            writer.WriteLine($"     - **Excessive position deviation** at {posErrors.First().Timestamp} - Position deviation error");
                        if (overloadErrors.Count > 0)
                            writer.WriteLine($"     - **Overload error** at {overloadErrors.First().Timestamp} - Motor overload condition");
                    }
                    
                    if (machine.DatabaseErrors.Count > 0)
                    {
                        writer.WriteLine($"     - **Database connection issues** - {machine.DatabaseErrors.Count} occurrences");
                    }
                    
                    if (machine.PickingErrors.Count > 0)
                    {
                        writer.WriteLine($"     - **Garment picking errors** - {machine.PickingErrors.Count} occurrences");
                    }
                    
                    if (machine.SensorErrors.Count > 0)
                    {
                        writer.WriteLine($"     - **Sensor errors** - {machine.SensorErrors.Count} occurrences");
                    }
                    
                    if (machine.CollectionErrors.Count > 0)
                    {
                        writer.WriteLine($"     - **Collection/return errors** - {machine.CollectionErrors.Count} occurrences");
                    }
                }
                else
                {
                    writer.WriteLine("   - **Errors Found:**");
                    writer.WriteLine("     - Application closure events (normal operation)");
                }
                
                writer.WriteLine($"   - **Status:** {GetStatusEmoji(machine.Status)} **{machine.Status}**");
                writer.WriteLine();
            }

            // Machines without logs
            if (machinesWithoutLogs.Count > 0)
            {
                writer.WriteLine("### Machines WITHOUT Logs:");
                writer.WriteLine();
                foreach (var machine in machinesWithoutLogs)
                {
                    writer.WriteLine($"- **{machine.MachineName}** - No error logs found");
                }
                writer.WriteLine();
            }

            // Error types summary
            writer.WriteLine("---");
            writer.WriteLine();
            writer.WriteLine("## Error Types Observed");
            writer.WriteLine();

            var allCritical = analysis.Machines.SelectMany(m => m.CriticalErrors).ToList();
            var allDb = analysis.Machines.SelectMany(m => m.DatabaseErrors).ToList();
            var allPicking = analysis.Machines.SelectMany(m => m.PickingErrors).ToList();

            if (allCritical.Count > 0)
            {
                writer.WriteLine("### 1. **Critical Machine Alarms** 🔴");
                var homing = allCritical.Where(e => e.Type == "HomingKo").ToList();
                var pos = allCritical.Where(e => e.Type == "PositionDeviation").ToList();
                var overload = allCritical.Where(e => e.Type == "OverLoad").ToList();
                
                if (homing.Count > 0)
                    writer.WriteLine($"- **HomingKo Errors:** {homing.Count} occurrence(s)");
                if (pos.Count > 0)
                    writer.WriteLine($"- **Position Deviation Errors:** {pos.Count} occurrence(s)");
                if (overload.Count > 0)
                    writer.WriteLine($"- **Overload Errors:** {overload.Count} occurrence(s)");
                writer.WriteLine();
            }

            if (allDb.Count > 0)
            {
                writer.WriteLine("### 2. **Database Connectivity Issues** 🔴");
                writer.WriteLine($"- **Total Database Errors:** {allDb.Count} occurrences");
                writer.WriteLine($"- **Affected Machines:** {analysis.Machines.Count(m => m.DatabaseErrors.Count > 0)}");
                writer.WriteLine();
            }

            if (allPicking.Count > 0)
            {
                writer.WriteLine("### 3. **Garment Picking Errors** ⚠️");
                writer.WriteLine($"- **Total Picking Errors:** {allPicking.Count} occurrences");
                writer.WriteLine($"- **Affected Machines:** {analysis.Machines.Count(m => m.PickingErrors.Count > 0)}");
                writer.WriteLine();
            }

            // Priority summary
            writer.WriteLine("---");
            writer.WriteLine();
            writer.WriteLine("## Priority Summary");
            writer.WriteLine();

            var p0Machines = analysis.Machines.Where(m => m.Priority == "P0").ToList();
            var p1Machines = analysis.Machines.Where(m => m.Priority == "P1").ToList();
            var p2Machines = analysis.Machines.Where(m => m.Priority == "P2").ToList();

            if (p0Machines.Count > 0)
            {
                writer.WriteLine("### 🔴 **P0 - CRITICAL (Immediate Action Required)**");
                writer.WriteLine();
                foreach (var machine in p0Machines)
                {
                    writer.WriteLine($"{p0Machines.IndexOf(machine) + 1}. **{machine.MachineName}**{(string.IsNullOrEmpty(machine.MachineId) ? "" : $" ({machine.MachineId})")}");
                    writer.WriteLine("   - **Issues:**");
                    if (machine.CriticalErrors.Count > 0)
                        writer.WriteLine($"     - {machine.CriticalErrors.Count} critical machine errors");
                    if (machine.DatabaseErrors.Count > 0)
                        writer.WriteLine($"     - {machine.DatabaseErrors.Count} database connection errors");
                    if (machine.PickingErrors.Count > 0)
                        writer.WriteLine($"     - {machine.PickingErrors.Count} picking errors");
                    writer.WriteLine("   - **Recommended Actions:**");
                    writer.WriteLine("     - Check machine hardware and sensors");
                    writer.WriteLine("     - Verify database connectivity");
                    writer.WriteLine("     - Review error logs for specific issues");
                    writer.WriteLine();
                }
            }

            if (p1Machines.Count > 0)
            {
                writer.WriteLine("### ⚠️ **P1 - HIGH PRIORITY (Address Soon)**");
                writer.WriteLine();
                foreach (var machine in p1Machines)
                {
                    writer.WriteLine($"{p1Machines.IndexOf(machine) + 1}. **{machine.MachineName}**{(string.IsNullOrEmpty(machine.MachineId) ? "" : $" ({machine.MachineId})")}");
                    writer.WriteLine("   - **Issues:**");
                    if (machine.DatabaseErrors.Count > 0)
                        writer.WriteLine($"     - {machine.DatabaseErrors.Count} database errors");
                    if (machine.PickingErrors.Count > 0)
                        writer.WriteLine($"     - {machine.PickingErrors.Count} picking errors");
                    writer.WriteLine();
                }
            }

            // Summary statistics
            writer.WriteLine("---");
            writer.WriteLine();
            writer.WriteLine("## Summary Statistics");
            writer.WriteLine();
            writer.WriteLine($"- **Total Error Log Files:** {analysis.TotalLogFiles}");
            writer.WriteLine($"- **Machines with Critical Errors:** {analysis.MachinesWithCriticalErrors} (P0)");
            writer.WriteLine($"- **Machines with Warnings:** {analysis.MachinesWithWarnings} (P1)");
            writer.WriteLine($"- **Machines Operating Normally:** {analysis.MachinesOperatingNormally} (P2)");
            writer.WriteLine();

            writer.WriteLine("---");
            writer.WriteLine();
            writer.WriteLine($"*Report generated: {FormatDate(dateStr)}*");
            writer.WriteLine($"*Log Date Range: {FormatDate(dateStr)}*");
        }
    }

    private static string FormatDate(string dateStr)
    {
        if (DateTime.TryParseExact(dateStr, "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out DateTime date))
        {
            return date.ToString("MMMM dd, yyyy");
        }
        return dateStr;
    }

    private static string GetStatusEmoji(string status)
    {
        return status switch
        {
            "CRITICAL" => "🔴",
            "WARNING" => "⚠️",
            "OK" => "✅",
            _ => "✅"
        };
    }
}
