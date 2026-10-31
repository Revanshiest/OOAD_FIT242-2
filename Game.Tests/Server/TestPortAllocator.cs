using System.Net;
using System.Net.Sockets;

namespace Game.Tests.Server;

public static class TestPortAllocator
{
    public static int GetFreePort()
    {
        var portProbeListener = new TcpListener(IPAddress.Loopback, 0);
        portProbeListener.Start();
        var allocatedPort = ((IPEndPoint)portProbeListener.LocalEndpoint).Port;
        portProbeListener.Stop();

        return allocatedPort;
    }
}
