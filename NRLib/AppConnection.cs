using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using NRLib.Packets;

namespace NRLib
{
    public class AppConnection
    {
        public byte[] InstanceId { get; }
        public EntryPoint EP { get; }
        public AppConnection(byte[] instanceId, EntryPoint ep)
        {
            InstanceId = instanceId;
            EP = ep;
        }

        public async Task Connect()
        {
            TaskCompletionSource<int> src = new TaskCompletionSource<int>();
            uint nonce = Packet.WatchNonce(pk =>
            {
                src.SetResult(1);
            });
            byte[] bt = new TcpCOpenSocket(InstanceId, nonce).Build();
            await EP._tcpConnection.Stream.WriteAsync(bt);
            //await src.Task;
        }
    }
}