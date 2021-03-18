using System;
using System.Threading.Tasks;
using NRLib.Packets;

namespace NRLib
{
    /// <summary>
    /// Represents a live connection to an app
    /// </summary>
    public class AppConnection
    {
        /// <summary>
        /// The application instance ID
        /// </summary>
        public byte[] InstanceId { get; }
        
        /// <summary>
        /// The socket ID
        /// </summary>
        public byte[] SocketId { get; internal set; }
        
        /// <summary>
        /// The associated entry point
        /// </summary>
        public EntryPoint EP { get; }
        
        /// <summary>
        /// True if the socket is open
        /// </summary>
        public bool Open { get; internal set; }
        
        /// <summary>
        /// True if the instance is the initial requestor
        /// </summary>
        public bool Requestor { get; private set; }
        
        /// <summary>
        /// True if the connection has been detected as a loopback
        /// </summary>
        public bool Loopback { get; internal set; }
        internal TaskCompletionSource<bool> ConnectCompletionSource { get; private set; }
        
        /// <summary>
        /// The data stream
        /// </summary>
        public NRStream Stream { get; private set; }
        
        /// <summary>
        /// Defines a new app connection
        /// </summary>
        /// <param name="instanceId">The instance ID of the app to connect to</param>
        /// <param name="ep">The entry point to connect through</param>
        public AppConnection(byte[] instanceId, EntryPoint ep)
        {
            if (instanceId.Length != 10) throw new ArgumentException("InstanceId is not 10 bytes");
            InstanceId = instanceId;
            EP = ep;
        }
        
        /// <summary>
        /// Converts a byte ID to a string ID
        /// </summary>
        /// <param name="id">The byte ID</param>
        /// <returns>A string version of the ID</returns>
        public static string IdToString(byte[] id)
        {
            string s = "";
            foreach (var b in id)
            {
                s += b.ToString("X");
            }

            return s;
        }

        /// <summary>
        /// Initiates the connection to the app
        /// </summary>
        /// <returns>An async Task</returns>
        /// <exception cref="Exception">Thrown if the connection is refused</exception>
        public async Task Connect()
        {
            if (SocketId != null) return;
            ConnectCompletionSource = new TaskCompletionSource<bool>();
            uint nonce = Packet.WatchNonce(async pk =>
            {
                await Task.Run(() =>
                {
                    TcpCSSocketControl control = new TcpCSSocketControl(pk);
                    SocketId = control.SocketId;
                    EP.Connections.Add(IdToString(control.SocketId), this);
                    Requestor = true;
                    Stream = new NRStream();
                    Stream.OnSend += StreamOn_onSend;
                    if (control.CheckFlag(TcpCSSocketControl.Close))
                    {
                        ConnectCompletionSource.SetResult(false);
                    }
                });
            });
            byte[] bt = new TcpCOpenSocket(InstanceId, nonce).Build();
            await EP.TCPConnection.Stream.WriteAsync(bt);
            bool success = await ConnectCompletionSource.Task;
            if (!success)
            {
                await Close(false);
                throw new Exception("Connect to distant end was refused.");
            }
        }

        private void StreamOn_onSend(byte[] bytes)
        {
            var pa = new TcpCSSocketData(SocketId, bytes);
            EP.TCPConnection.Stream.Write(pa.Build());
        }

        /// <summary>
        /// Accepts the connection
        /// </summary>
        public async Task Accept()
        {
            await Task.Run(() =>
            {
                if ((Requestor && !Loopback) || Open) return;
                TcpCSSocketControl control = new TcpCSSocketControl(SocketId, TcpCSSocketControl.AcceptConnection);
                EP.TCPConnection.Stream.Write(control.Build());
                Open = true;
            });
        }

        /// <summary>
        /// Refuses the connection
        /// </summary>
        public async Task Refuse()
        {
            if ((Requestor && !Loopback) || Open) return;
            TcpCSSocketControl control = new TcpCSSocketControl(SocketId, TcpCSSocketControl.RefuseConnection);
            EP.TCPConnection.Stream.Write(control.Build());
            await Close(false);
        }

        /// <summary>
        /// Closes the connection
        /// </summary>
        /// <param name="sendPack">Whether to send the payload. Usually this is true.</param>
        public async Task Close(bool sendPack = true)
        {
            await Task.Run(() =>
            {
                if (!Open) return;
                if (sendPack)
                {
                    TcpCSSocketControl control = new TcpCSSocketControl(SocketId, TcpCSSocketControl.Close);
                    EP.TCPConnection.Stream.Write(control.Build());
                }

                Stream.Close();
                EP.Connections.Remove(IdToString(SocketId));
            });
        }
    }
}