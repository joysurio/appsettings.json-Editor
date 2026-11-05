using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace AppSettingsEditor;

public class NetworkAnalysisGenerator
{
    public static void GenerateNetworkAnalysis(AnalysisResult analysis, string outputFile, string dateStr)
    {
        // Check if there are any network-related database errors
        var machinesWithNetworkErrors = analysis.Machines
            .Where(m => m.DatabaseErrors.Any(e => IsNetworkError(e.Type)))
            .ToList();

        // Always generate the file, even if no network errors found
        using (var writer = new StreamWriter(outputFile))
        {
            writer.WriteLine($"# Network Connection Issues Analysis Report");
            writer.WriteLine($"## {FormatDate(dateStr)}");
            writer.WriteLine();
            writer.WriteLine("---");
            writer.WriteLine();

            if (machinesWithNetworkErrors.Count == 0)
            {
                // No network errors found - generate a simple report
                writer.WriteLine("## Executive Summary");
                writer.WriteLine();
                writer.WriteLine($"This report documents network connection analysis for the log files from {FormatDate(dateStr)}.");
                writer.WriteLine();
                writer.WriteLine("**Key Findings:**");
                writer.WriteLine("- ✅ **No Network Connection Issues Detected**");
                writer.WriteLine();
                writer.WriteLine("No network-related database connection errors were found in the analyzed log files.");
                writer.WriteLine("All database connections appear to be functioning normally.");
                writer.WriteLine();
                writer.WriteLine("---");
                writer.WriteLine();
                writer.WriteLine($"*Report Generated: {FormatDate(dateStr)}*");
                writer.WriteLine($"*Analysis Date: {FormatDate(dateStr)}*");
                return;
            }

            // Network errors found - generate detailed report
            writer.WriteLine("## Executive Summary");
            writer.WriteLine();
            writer.WriteLine($"This report documents **network connection issues** found in the log files from {FormatDate(dateStr)}. The analysis reveals network-related failures affecting database connectivity.");
            writer.WriteLine();
            writer.WriteLine("**Key Findings:**");

            var connectionPoolErrors = machinesWithNetworkErrors.SelectMany(m => m.DatabaseErrors)
                .Where(e => e.Type == "ConnectionPoolExhaustion").ToList();
            var dnsErrors = machinesWithNetworkErrors.SelectMany(m => m.DatabaseErrors)
                .Where(e => e.Type == "UnknownHost").ToList();
            var transportErrors = machinesWithNetworkErrors.SelectMany(m => m.DatabaseErrors)
                .Where(e => e.Type == "TransportLevelError").ToList();
            var preLoginErrors = machinesWithNetworkErrors.SelectMany(m => m.DatabaseErrors)
                .Where(e => e.Type == "PreLoginTimeout").ToList();
            var timeoutErrors = machinesWithNetworkErrors.SelectMany(m => m.DatabaseErrors)
                .Where(e => e.Type == "ConnectionTimeout").ToList();

            if (connectionPoolErrors.Count > 0)
                writer.WriteLine($"- 🔴 **Connection Pool Exhaustion** - {connectionPoolErrors.Count} occurrence(s)");
            if (dnsErrors.Count > 0)
                writer.WriteLine($"- 🔴 **DNS Resolution Failures** - \"Host desconocido\" (Unknown host) errors - {dnsErrors.Count} occurrence(s)");
            if (transportErrors.Count > 0)
                writer.WriteLine($"- 🔴 **TCP Transport-Level Errors** - Network timeouts and connection failures - {transportErrors.Count} occurrence(s)");
            if (preLoginErrors.Count > 0)
                writer.WriteLine($"- 🔴 **Pre-Login Handshake Timeouts** - Connection establishment failures - {preLoginErrors.Count} occurrence(s)");
            if (timeoutErrors.Count > 0)
                writer.WriteLine($"- 🔴 **Connection Timeouts** - {timeoutErrors.Count} occurrence(s)");

            writer.WriteLine($"- 🔴 **{machinesWithNetworkErrors.Count} Machine(s) Affected** - Network connectivity problems");
            writer.WriteLine();
            writer.WriteLine("---");
            writer.WriteLine();
            writer.WriteLine("## Database Server Information");
            writer.WriteLine();
            writer.WriteLine("- **Server:** `sqlunidresstelaviv.ch2020gkwvwk.il-central-1.rds.amazonaws.com`");
            writer.WriteLine("- **Port:** `3433`");
            writer.WriteLine("- **Database:** `Laundry_ShaareZedek`");
            writer.WriteLine("- **Region:** `il-central-1` (AWS Israel Central)");
            writer.WriteLine();
            writer.WriteLine("---");
            writer.WriteLine();
            writer.WriteLine("## Network Issues Detected");
            writer.WriteLine();

            int issueNumber = 1;

            // Connection Pool Exhaustion
            if (connectionPoolErrors.Count > 0)
            {
                writer.WriteLine($"### {issueNumber}. Connection Pool Exhaustion 🔴 **CRITICAL**");
                writer.WriteLine();
                writer.WriteLine("**Error Message:**");
                writer.WriteLine("```");
                writer.WriteLine("Timeout expired. The timeout period elapsed prior to obtaining a connection from the pool.");
                writer.WriteLine("This may have occurred because all pooled connections were in use and max pool size was reached.");
                writer.WriteLine("```");
                writer.WriteLine();
                writer.WriteLine("**Affected Machines:**");
                foreach (var machine in machinesWithNetworkErrors.Where(m => m.DatabaseErrors.Any(e => e.Type == "ConnectionPoolExhaustion")))
                {
                    var errors = machine.DatabaseErrors.Where(e => e.Type == "ConnectionPoolExhaustion").ToList();
                    writer.WriteLine($"- **Machine:** `{machine.MachineName}`{(string.IsNullOrEmpty(machine.MachineId) ? "" : $" ({machine.MachineId})")}");
                    writer.WriteLine($"  - **First Occurrence:** {errors.First().Timestamp}");
                    writer.WriteLine($"  - **Total Occurrences:** {errors.Count}");
                }
                writer.WriteLine();
                writer.WriteLine("---");
                writer.WriteLine();
                issueNumber++;
            }

            // DNS Resolution Failure
            if (dnsErrors.Count > 0)
            {
                writer.WriteLine($"### {issueNumber}. DNS Resolution Failure / Unknown Host 🔴 **CRITICAL**");
                writer.WriteLine();
                writer.WriteLine("**Error Message:**");
                writer.WriteLine("```");
                writer.WriteLine("A network-related or instance-specific error occurred while establishing a connection to SQL Server.");
                writer.WriteLine("The server was not found or was not accessible. (provider: TCP Provider, error: 0 - Host desconocido.)");
                writer.WriteLine("```");
                writer.WriteLine();
                writer.WriteLine("**Error Code:** `11001` (Win32Exception)");
                writer.WriteLine();
                writer.WriteLine("**Affected Machines:**");
                foreach (var machine in machinesWithNetworkErrors.Where(m => m.DatabaseErrors.Any(e => e.Type == "UnknownHost")))
                {
                    var errors = machine.DatabaseErrors.Where(e => e.Type == "UnknownHost").ToList();
                    writer.WriteLine($"- **Machine:** `{machine.MachineName}`{(string.IsNullOrEmpty(machine.MachineId) ? "" : $" ({machine.MachineId})")}");
                    writer.WriteLine($"  - **First Occurrence:** {errors.First().Timestamp}");
                    writer.WriteLine($"  - **Total Occurrences:** {errors.Count}");
                }
                writer.WriteLine();
                writer.WriteLine("---");
                writer.WriteLine();
                issueNumber++;
            }

            // TCP Transport-Level Errors
            if (transportErrors.Count > 0)
            {
                writer.WriteLine($"### {issueNumber}. TCP Transport-Level Connection Errors 🔴 **CRITICAL**");
                writer.WriteLine();
                writer.WriteLine("**Error Message:**");
                writer.WriteLine("```");
                writer.WriteLine("A transport-level error has occurred when receiving results from the server.");
                writer.WriteLine("(provider: TCP Provider, error: 0 - Connection timed out)");
                writer.WriteLine("```");
                writer.WriteLine();
                writer.WriteLine("**Error Code:** `10060` (Win32Exception)");
                writer.WriteLine();
                writer.WriteLine("**Affected Machines:**");
                foreach (var machine in machinesWithNetworkErrors.Where(m => m.DatabaseErrors.Any(e => e.Type == "TransportLevelError")))
                {
                    var errors = machine.DatabaseErrors.Where(e => e.Type == "TransportLevelError").ToList();
                    writer.WriteLine($"- **Machine:** `{machine.MachineName}`{(string.IsNullOrEmpty(machine.MachineId) ? "" : $" ({machine.MachineId})")}");
                    writer.WriteLine($"  - **First Occurrence:** {errors.First().Timestamp}");
                    writer.WriteLine($"  - **Total Occurrences:** {errors.Count}");
                }
                writer.WriteLine();
                writer.WriteLine("---");
                writer.WriteLine();
                issueNumber++;
            }

            // Pre-Login Handshake Timeout
            if (preLoginErrors.Count > 0)
            {
                writer.WriteLine($"### {issueNumber}. Connection Timeout - Pre-Login Handshake Failure 🔴 **CRITICAL**");
                writer.WriteLine();
                writer.WriteLine("**Error Message:**");
                writer.WriteLine("```");
                writer.WriteLine("Connection Timeout Expired. The timeout period elapsed while attempting to consume the");
                writer.WriteLine("pre-login handshake acknowledgement.");
                writer.WriteLine("```");
                writer.WriteLine();
                writer.WriteLine("**Error Code:** `-2` (Win32Exception 258)");
                writer.WriteLine();
                writer.WriteLine("**Affected Machines:**");
                foreach (var machine in machinesWithNetworkErrors.Where(m => m.DatabaseErrors.Any(e => e.Type == "PreLoginTimeout")))
                {
                    var errors = machine.DatabaseErrors.Where(e => e.Type == "PreLoginTimeout").ToList();
                    writer.WriteLine($"- **Machine:** `{machine.MachineName}`{(string.IsNullOrEmpty(machine.MachineId) ? "" : $" ({machine.MachineId})")}");
                    writer.WriteLine($"  - **First Occurrence:** {errors.First().Timestamp}");
                    writer.WriteLine($"  - **Total Occurrences:** {errors.Count}");
                }
                writer.WriteLine();
                writer.WriteLine("---");
                writer.WriteLine();
                issueNumber++;
            }

            // Connection Timeouts
            if (timeoutErrors.Count > 0)
            {
                writer.WriteLine($"### {issueNumber}. Connection Timeout Errors 🔴 **CRITICAL**");
                writer.WriteLine();
                writer.WriteLine("**Affected Machines:**");
                foreach (var machine in machinesWithNetworkErrors.Where(m => m.DatabaseErrors.Any(e => e.Type == "ConnectionTimeout")))
                {
                    var errors = machine.DatabaseErrors.Where(e => e.Type == "ConnectionTimeout").ToList();
                    writer.WriteLine($"- **Machine:** `{machine.MachineName}`{(string.IsNullOrEmpty(machine.MachineId) ? "" : $" ({machine.MachineId})")}");
                    writer.WriteLine($"  - **First Occurrence:** {errors.First().Timestamp}");
                    writer.WriteLine($"  - **Total Occurrences:** {errors.Count}");
                }
                writer.WriteLine();
                writer.WriteLine("---");
                writer.WriteLine();
            }

            writer.WriteLine("## Recommendations");
            writer.WriteLine();
            writer.WriteLine("### Immediate Actions:");
            writer.WriteLine("1. Verify network connectivity from affected machines to AWS RDS");
            writer.WriteLine("2. Check DNS resolution for the database server");
            writer.WriteLine("3. Review firewall rules and security group configurations");
            writer.WriteLine("4. Check AWS RDS instance health and performance metrics");
            writer.WriteLine();
            writer.WriteLine("### Short-term Actions:");
            writer.WriteLine("1. Implement retry logic with exponential backoff");
            writer.WriteLine("2. Enable `EnableRetryOnFailure` in Entity Framework configuration");
            writer.WriteLine("3. Add connection monitoring and logging");
            writer.WriteLine();
            writer.WriteLine("---");
            writer.WriteLine();
            writer.WriteLine($"*Report Generated: {FormatDate(dateStr)}*");
            writer.WriteLine($"*Analysis Date: {FormatDate(dateStr)}*");
        }
    }

    private static bool IsNetworkError(string errorType)
    {
        return errorType == "ConnectionPoolExhaustion" ||
               errorType == "UnknownHost" ||
               errorType == "TransportLevelError" ||
               errorType == "PreLoginTimeout" ||
               errorType == "ConnectionTimeout";
    }

    private static string FormatDate(string dateStr)
    {
        if (dateStr.Length == 8)
        {
            int year = int.Parse(dateStr.Substring(0, 4));
            int month = int.Parse(dateStr.Substring(4, 2));
            int day = int.Parse(dateStr.Substring(6, 2));
            return new DateTime(year, month, day).ToString("MMMM dd, yyyy");
        }
        return dateStr;
    }
}
