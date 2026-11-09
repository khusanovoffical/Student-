using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Student.Services;

public sealed class DiscoveryClient
{
    private const int DiscoveryPort = 5124;

    public static async Task<string?> DiscoverServerAsync(TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource(timeout);
        try
        {
            using var udp = new UdpClient();
            udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            udp.Client.Bind(new IPEndPoint(IPAddress.Any, DiscoveryPort));

            var receiveTask = udp.ReceiveAsync(cts.Token);
            var result = await receiveTask;

            var message = Encoding.UTF8.GetString(result.Buffer);
            if (message.StartsWith("SCHOOLCONTROL_BEACON:"))
            {
                var port = message.Split(':')[1];
                var ip = result.RemoteEndPoint.Address.ToString();
                return $"http://{ip}:{port}";
            }
        }
        catch
        {
            // Timeout yoki tarmoq xatosi bo'lsa null qaytaradi
        }
        return null;
    }
}
