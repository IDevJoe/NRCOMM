using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using NRLib.Packets;

namespace NRLib
{
    public class AppConnection
    {
        public byte[] InstanceId { get; }
        public byte[] SocketId;
        public EntryPoint EP { get; }
        public bool Open = false;
        public bool Requestor = false;
        public bool Loopback = false;
        public AppConnection(byte[] instanceId, EntryPoint ep)
        {
            InstanceId = instanceId;
            EP = ep;
        }
        
        public static string IdToString(byte[] id)
        {
            string s = "";
            foreach (var b in id)
            {
                s += b.ToString("X");
            }

            return s;
        }

        public async Task Connect()
        {
            if (SocketId != null) return;
            TaskCompletionSource<int> src = new TaskCompletionSource<int>();
            uint nonce = Packet.WatchNonce(pk =>
            {
                src.SetResult(1);
                TcpCSSocketControl control = new TcpCSSocketControl(pk);
                SocketId = control.SocketId;
                EP.Connections.Add(IdToString(control.SocketId), this);
                Requestor = true;
            });
            byte[] bt = new TcpCOpenSocket(InstanceId, nonce).Build();
            await EP._tcpConnection.Stream.WriteAsync(bt);
            //await src.Task;
        }

        public void Accept()
        {
            if (Requestor) return;
            
        }

        public void Close()
        {
            
        }
    }
}