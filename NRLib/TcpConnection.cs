using System.IO;
using System.Net.Sockets;

namespace NRLib
{
    public struct TcpConnection
    {
        public Socket Socket;
        public Stream Stream;
    }
}