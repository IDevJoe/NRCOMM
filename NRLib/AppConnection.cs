using System;
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
        public TaskCompletionSource<bool> ConnectCompletionSource;
        public NRStream Stream { get; private set; }
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
            ConnectCompletionSource = new TaskCompletionSource<bool>();
            uint nonce = Packet.WatchNonce(pk =>
            {
                TcpCSSocketControl control = new TcpCSSocketControl(pk);
                SocketId = control.SocketId;
                EP.Connections.Add(IdToString(control.SocketId), this);
                Requestor = true;
                Stream = new NRStream();
                Stream._onSend += StreamOn_onSend;
                if (control.CheckFlag(TcpCSSocketControl.CLOSE))
                {
                    ConnectCompletionSource.SetResult(false);
                }
            });
            byte[] bt = new TcpCOpenSocket(InstanceId, nonce).Build();
            await EP._tcpConnection.Stream.WriteAsync(bt);
            bool success = await ConnectCompletionSource.Task;
            if (!success)
            {
                Close(false);
                throw new Exception("Connect to distant end was refused.");
            }
        }

        private void StreamOn_onSend(byte[] bytes)
        {
            var pa = new TcpCSSocketData(SocketId, bytes);
            EP._tcpConnection.Stream.Write(pa.Build());
        }

        public void Accept()
        {
            if ((Requestor && !Loopback) || Open) return;
            TcpCSSocketControl control = new TcpCSSocketControl(SocketId, TcpCSSocketControl.ACCEPT_CONNECTION);
            EP._tcpConnection.Stream.Write(control.Build());
            Open = true;
        }

        public void Refuse()
        {
            if ((Requestor && !Loopback) || Open) return;
            TcpCSSocketControl control = new TcpCSSocketControl(SocketId, TcpCSSocketControl.REFUSE_CONNECTION);
            EP._tcpConnection.Stream.Write(control.Build());
            Close(false);
        }

        public void Close(bool sendPack = true)
        {
            if (!Open) return;
            if (sendPack)
            {
                TcpCSSocketControl control = new TcpCSSocketControl(SocketId, TcpCSSocketControl.CLOSE);
                EP._tcpConnection.Stream.Write(control.Build());
            }

            Stream.Close();
            EP.Connections.Remove(IdToString(SocketId));
        }
    }
}