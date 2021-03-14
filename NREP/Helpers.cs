using System.IO;
using System.Net.Sockets;
using NREP.Managers;
using NRLib;

namespace NREP
{
    public static class Helpers
    {
        public static Packet NextPacket(this Stream str, TcpConnection connection)
        {
            return new Packet(str, connection);
        }
    }
}