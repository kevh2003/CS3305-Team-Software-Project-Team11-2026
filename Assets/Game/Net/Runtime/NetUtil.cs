using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;

// Local IP Helper, used in LAN hosting currently

public static class NetUtil
{
    public static string GetLocalIPv4()
    {
        foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (ni.OperationalStatus != OperationalStatus.Up) continue;
            if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;

            var props = ni.GetIPProperties();
            var addr = props.UnicastAddresses
                .FirstOrDefault(a => a.Address.AddressFamily == AddressFamily.InterNetwork);

            if (addr != null)
                return addr.Address.ToString();
        }

        return "127.0.0.1";
    }
}