using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace AppSettingsEditor.Services;

/// <summary>Mirrors RemoteMachineAgent <see cref="NetworkService"/> for diagnostics in the editor.</summary>
internal static class RemoteMachineAgentNetworkHelper
{
    public static string GetLocalIPAddress()
    {
        try
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                    return ip.ToString();
            }
        }
        catch
        {
            // ignored
        }

        return "127.0.0.1";
    }

    public static bool IsNetworkAvailable() => NetworkInterface.GetIsNetworkAvailable();

    public static string GetPrimaryNetworkInterface()
    {
        try
        {
            var ni = NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == OperationalStatus.Up &&
                            n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .OrderByDescending(n => n.Speed)
                .FirstOrDefault();
            return ni?.Name ?? "Unknown";
        }
        catch
        {
            return "Unknown";
        }
    }
}
