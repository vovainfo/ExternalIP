using System.Net;
using System.Net.Sockets;

namespace ExternalIpWidget.Core;

/// <summary>
/// Локальный адрес интерфейса, через который система вышла бы в интернет.
/// Это не внешний IP: за роутером здесь будет адрес вида 192.168.*.
/// </summary>
public static class LocalNicAddress
{
    public static string? TryGetOutbound()
    {
        try
        {
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            // Connect для UDP только выбирает локальный интерфейс и ничего не отправляет.
            socket.Connect(new IPEndPoint(IPAddress.Parse("1.1.1.1"), 65530));
            if (socket.LocalEndPoint is IPEndPoint endPoint && !IPAddress.IsLoopback(endPoint.Address))
                return endPoint.Address.ToString();
        }
        catch (SocketException)
        {
        }

        return null;
    }
}
