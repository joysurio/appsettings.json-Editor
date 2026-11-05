using AppSettingsEditor.Models;
using Newtonsoft.Json;
using System.IO;
using System.Net.NetworkInformation;
using System.Security;

namespace AppSettingsEditor.Services;

internal class RemoteMachineAgentService
{
    private readonly string _machinesFilePath;
    private List<RemoteMachine> _machines = new();

    public RemoteMachineAgentService(string? baseDirectory = null)
    {
        var baseDir = baseDirectory ?? Directory.GetCurrentDirectory();
        _machinesFilePath = Path.Combine(baseDir, "remote-machines.json");
        LoadMachines();
    }

    public List<RemoteMachine> GetAllMachines()
    {
        return _machines.ToList();
    }

    public RemoteMachine? GetMachine(string id)
    {
        return _machines.FirstOrDefault(m => m.Id == id);
    }

    public void AddMachine(RemoteMachine machine)
    {
        if (_machines.Any(m => m.Id == machine.Id))
        {
            throw new InvalidOperationException($"Machine with ID {machine.Id} already exists");
        }
        _machines.Add(machine);
        SaveMachines();
    }

    public void UpdateMachine(RemoteMachine machine)
    {
        var existing = _machines.FirstOrDefault(m => m.Id == machine.Id);
        if (existing == null)
        {
            throw new InvalidOperationException($"Machine with ID {machine.Id} not found");
        }
        _machines.Remove(existing);
        _machines.Add(machine);
        SaveMachines();
    }

    public void RemoveMachine(string id)
    {
        var machine = _machines.FirstOrDefault(m => m.Id == id);
        if (machine != null)
        {
            _machines.Remove(machine);
            SaveMachines();
        }
    }

    public ConnectionTestResult TestConnection(RemoteMachine machine)
    {
        machine.LastConnectionAttempt = DateTime.Now;
        
        try
        {
            // Test network connectivity
            if (!string.IsNullOrEmpty(machine.Address))
            {
                var ping = new Ping();
                var reply = ping.Send(machine.Address, 3000);
                if (reply.Status != IPStatus.Success)
                {
                    machine.LastStatus = ConnectionStatus.Error;
                    SaveMachines();
                    return new ConnectionTestResult
                    {
                        Success = false,
                        ErrorMessage = $"Cannot reach {machine.Address}: {reply.Status}"
                    };
                }
            }

            // Test file access based on connection method
            switch (machine.ConnectionMethod.ToLower())
            {
                case "networkshare":
                    return TestNetworkShareConnection(machine);
                case "rustdesk":
                    return TestRustDeskConnection(machine);
                default:
                    machine.LastStatus = ConnectionStatus.Error;
                    SaveMachines();
                    return new ConnectionTestResult
                    {
                        Success = false,
                        ErrorMessage = $"Unsupported connection method: {machine.ConnectionMethod}"
                    };
            }
        }
        catch (Exception ex)
        {
            machine.LastStatus = ConnectionStatus.Error;
            SaveMachines();
            return new ConnectionTestResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    private ConnectionTestResult TestNetworkShareConnection(RemoteMachine machine)
    {
        try
        {
            if (string.IsNullOrEmpty(machine.NetworkSharePath))
            {
                return new ConnectionTestResult
                {
                    Success = false,
                    ErrorMessage = "Network share path is not configured"
                };
            }

            // If username/password are provided, try to map the network drive
            if (!string.IsNullOrEmpty(machine.Username) && !string.IsNullOrEmpty(machine.Password))
            {
                try
                {
                    // Extract server and share from network path (e.g., \\server\share)
                    var pathParts = machine.NetworkSharePath.TrimStart('\\').Split('\\');
                    if (pathParts.Length >= 2)
                    {
                        var server = pathParts[0];
                        var share = pathParts[1];
                        var driveLetter = "Z:"; // Use Z: as temporary drive
                        
                        // Try to map network drive
                        var netUseProcess = new System.Diagnostics.Process
                        {
                            StartInfo = new System.Diagnostics.ProcessStartInfo
                            {
                                FileName = "net.exe",
                                Arguments = $"use {driveLetter} \\\\{server}\\{share} /user:{machine.Username} {machine.Password}",
                                UseShellExecute = false,
                                RedirectStandardOutput = true,
                                RedirectStandardError = true,
                                CreateNoWindow = true
                            }
                        };
                        
                        netUseProcess.Start();
                        netUseProcess.WaitForExit(5000);
                        
                        if (netUseProcess.ExitCode == 0)
                        {
                            // Successfully mapped, now test access
                            var testPath = Path.Combine(driveLetter, "appsettings.json");
                            if (File.Exists(testPath))
                            {
                                // Unmap drive
                                System.Diagnostics.Process.Start("net.exe", $"use {driveLetter} /delete /y").WaitForExit(1000);
                                machine.LastStatus = ConnectionStatus.Connected;
                                SaveMachines();
                                return new ConnectionTestResult { Success = true };
                            }
                            
                            // Unmap drive
                            System.Diagnostics.Process.Start("net.exe", $"use {driveLetter} /delete /y").WaitForExit(1000);
                        }
                    }
                }
                catch
                {
                    // If mapping fails, fall through to direct access attempt
                }
            }

            // Try to access the network share directly
            var appSettingsPath = Path.Combine(machine.NetworkSharePath, "appsettings.json");
            var normalizedPath = appSettingsPath.Replace('/', '\\');

            if (File.Exists(normalizedPath))
            {
                machine.LastStatus = ConnectionStatus.Connected;
                SaveMachines();
                return new ConnectionTestResult { Success = true };
            }
            else
            {
                // Check if directory exists
                var dir = Path.GetDirectoryName(normalizedPath);
                if (Directory.Exists(dir))
                {
                    machine.LastStatus = ConnectionStatus.Connected;
                    SaveMachines();
                    return new ConnectionTestResult { Success = true, Message = "Directory accessible, but appsettings.json not found" };
                }
                else
                {
                    machine.LastStatus = ConnectionStatus.Error;
                    SaveMachines();
                    return new ConnectionTestResult
                    {
                        Success = false,
                        ErrorMessage = $"Directory not accessible: {dir}"
                    };
                }
            }
        }
        catch (Exception ex)
        {
            machine.LastStatus = ConnectionStatus.Error;
            SaveMachines();
            return new ConnectionTestResult
            {
                Success = false,
                ErrorMessage = $"Network share access failed: {ex.Message}"
            };
        }
    }

    private ConnectionTestResult TestRustDeskConnection(RemoteMachine machine)
    {
        // For PoC, we'll just check if RustDesk ID is configured
        // In production, this would actually test RustDesk connectivity
        if (string.IsNullOrEmpty(machine.RustDeskId))
        {
            machine.LastStatus = ConnectionStatus.Error;
            SaveMachines();
            return new ConnectionTestResult
            {
                Success = false,
                ErrorMessage = "RustDesk ID is not configured"
            };
        }

        // TODO: Implement actual RustDesk connection test
        machine.LastStatus = ConnectionStatus.Connected;
        SaveMachines();
        return new ConnectionTestResult 
        { 
            Success = true, 
            Message = "RustDesk connection test (PoC - not fully implemented)" 
        };
    }

    public AppSettingsRootModel? LoadRemoteAppSettings(RemoteMachine machine)
    {
        try
        {
            string filePath;
            
            switch (machine.ConnectionMethod.ToLower())
            {
                case "networkshare":
                    if (string.IsNullOrEmpty(machine.NetworkSharePath))
                        throw new InvalidOperationException("Network share path is not configured");
                    filePath = Path.Combine(machine.NetworkSharePath, "appsettings.json");
                    break;
                default:
                    throw new InvalidOperationException($"Unsupported connection method: {machine.ConnectionMethod}");
            }

            var normalizedPath = filePath.Replace('/', '\\');
            
            if (!File.Exists(normalizedPath))
            {
                return null;
            }

            var json = File.ReadAllText(normalizedPath);
            return JsonConvert.DeserializeObject<AppSettingsRootModel>(json);
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to load remote appsettings.json: {ex.Message}", ex);
        }
    }

    public void SaveRemoteAppSettings(RemoteMachine machine, AppSettingsRootModel model)
    {
        try
        {
            string filePath;
            
            switch (machine.ConnectionMethod.ToLower())
            {
                case "networkshare":
                    if (string.IsNullOrEmpty(machine.NetworkSharePath))
                        throw new InvalidOperationException("Network share path is not configured");
                    filePath = Path.Combine(machine.NetworkSharePath, "appsettings.json");
                    break;
                default:
                    throw new InvalidOperationException($"Unsupported connection method: {machine.ConnectionMethod}");
            }

            var normalizedPath = filePath.Replace('/', '\\');
            var directory = Path.GetDirectoryName(normalizedPath);
            
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                throw new InvalidOperationException($"Directory does not exist: {directory}");
            }

            // Create backup on remote machine
            if (File.Exists(normalizedPath))
            {
                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var backupPath = Path.Combine(directory ?? "", $"appsettings_{timestamp}.bak");
                File.Copy(normalizedPath, backupPath, overwrite: true);
            }

            // Atomic write
            var tempPath = normalizedPath + ".tmp";
            var json = JsonConvert.SerializeObject(model, Formatting.Indented);
            File.WriteAllText(tempPath, json);
            File.Move(tempPath, normalizedPath, overwrite: true);
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to save remote appsettings.json: {ex.Message}", ex);
        }
    }

    private void LoadMachines()
    {
        try
        {
            if (File.Exists(_machinesFilePath))
            {
                var json = File.ReadAllText(_machinesFilePath);
                _machines = JsonConvert.DeserializeObject<List<RemoteMachine>>(json) ?? new List<RemoteMachine>();
            }
        }
        catch
        {
            _machines = new List<RemoteMachine>();
        }
    }

    private void SaveMachines()
    {
        try
        {
            var json = JsonConvert.SerializeObject(_machines, Formatting.Indented);
            File.WriteAllText(_machinesFilePath, json);
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to save remote machines: {ex.Message}", ex);
        }
    }
}

internal class ConnectionTestResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string? Message { get; set; }
}

