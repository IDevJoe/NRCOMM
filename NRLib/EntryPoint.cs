using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using NRLib.Packets;
using Serilog;

namespace NRLib
{
    /// <summary>
    /// Represents an entry point
    /// </summary>
    public class EntryPoint
    {
        public class StoredApp
        {
            public string Description;
            public byte[] AppId;
            public byte[] InstanceId;
            public ConnectionEstablishCallback Callback;
        }

        private List<StoredApp> _registeredApps = new List<StoredApp>();

        /// <summary>
        /// The remote address
        /// </summary>
        public IPEndPoint Address { get; }
        /// <summary>
        /// The remote X509 Certificate
        /// </summary>
        public X509Certificate2 Certificate { get; }
        private TcpClient _tcp;
        
        /// <summary>
        /// The client certificate used to authenticate
        /// </summary>
        public X509Certificate2 ClientCertificate { get; private set; }

        internal TcpConnection TCPConnection;

        internal Dictionary<string, AppConnection> Connections = new Dictionary<string, AppConnection>();

        internal EntryPoint(IPEndPoint ep, X509Certificate2 cert)
        {
            Address = ep;
            Certificate = cert;
        }

        /// <summary>
        /// Initiates a connection to the entry point
        /// </summary>
        /// <param name="cert">An X509 certificate to authenticate with</param>
        /// <returns>An async task</returns>
        public async Task Connect(X509Certificate2 cert)
        {
            if (_tcp != null) return;
            _tcp = new TcpClient();
            ClientCertificate = cert;
            await _tcp.ConnectAsync(Address.Address, Address.Port);
            _ = Task.Run(async () =>
            {
                try
                {
                    await BeginLoop();
                }
                catch (Exception e)
                {
                    Log.Error(e, "An error occurred in the TCP Client loop");
                    await Close();
                }
            });
        }

        /// <summary>
        /// Closes the connection
        /// </summary>
        /// <returns></returns>
        public async Task Close()
        {
            await Task.Run(() =>
            {
                if (_tcp != null)
                {
                    _tcp.Close();
                    _tcp = null;
                }
            });
        }

        private async Task BeginLoop()
        {
            TCPConnection = new TcpConnection()
            {
                Socket = _tcp.Client,
                Ref = this
            };
            using (NetworkStream stream = _tcp.GetStream())
            {
                TCPConnection.Stream = stream;
                if (Certificate == null)
                {
                    while (true)
                    {
                        Packet pkt = new Packet(stream, TCPConnection);
                        pkt.ExecuteRoutine();
                    }
                }
                else
                {
                    using (SslStream str2 = new SslStream(stream))
                    {
                        TCPConnection.Stream = str2;
                        X509Certificate2Collection coll = null;
                        if(ClientCertificate != null)
                            coll = new X509Certificate2Collection(ClientCertificate);
                        SslClientAuthenticationOptions authOpt = new SslClientAuthenticationOptions()
                        {
                            TargetHost = Address.Address.ToString(),
                            CertificateRevocationCheckMode = X509RevocationMode.NoCheck,
                            ClientCertificates = coll,
                            RemoteCertificateValidationCallback = RemoteCertificateValidationCallback
                        };
                        await str2.AuthenticateAsClientAsync(authOpt);
                        if (!str2.IsAuthenticated)
                        {
                            Log.Error("Authentication to entry point failed");
                            return;
                        }
                        while (true)
                        {
                            Packet pkt = new Packet(str2, TCPConnection);
                            pkt.ExecuteRoutine();
                        }
                    }
                }
            }
        }

        private bool RemoteCertificateValidationCallback(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslpolicyerrors)
        {
            return certificate.Equals(Certificate);
        }

        public delegate Task ConnectionEstablishCallback(AppConnection dataStream);

        /// <summary>
        /// Publishes a new app to the entry point
        /// </summary>
        /// <param name="description">The app description</param>
        /// <param name="callback">A callback for when connections become established</param>
        /// <returns>A task</returns>
        public async Task Publish(string description, ConnectionEstablishCallback callback)
        {
            if (_tcp == null || !_tcp.Connected) return;
            uint n = Packet.WatchNonce(async packet =>
            {
                await Task.Run(() =>
                {
                    TcpSPublishReply repl = new TcpSPublishReply(packet);
                    _registeredApps.Add(new StoredApp()
                    {
                        AppId = repl.AppId,
                        Callback = callback,
                        Description = description,
                        InstanceId = repl.InstanceId
                    });
                    Log.Debug("App {Description} ({AppId}) registered as instance {InstanceId}", description, repl.ReadableAppId, repl.ReadableInstanceId);
                });
            });
            var pub = new TcpCPublish(description, n);
            byte[] x = pub.Build();
            await TCPConnection.Stream.WriteAsync(x);
        }

        /// <summary>
        /// Discovers apps on the network
        /// </summary>
        /// <param name="description">The app description to search for</param>
        /// <returns>An array of instance IDs for running apps</returns>
        public async Task<byte[][]> DiscoverApps(string description)
        {
            byte[] appId = new byte[10];
            using (SHA1 sha1 = SHA1.Create())
            {
                appId = sha1.ComputeHash(Encoding.UTF8.GetBytes(description)).Take(10).ToArray();
            }
            
            TaskCompletionSource<byte[][]> ss = new TaskCompletionSource<byte[][]>();
            uint nonce = Packet.WatchNonce(async packet =>
            {
                await Task.Run(() =>
                {
                    var pack = new TcpSAppInstanceReply(packet);
                    ss.SetResult(pack.Instances);
                });
            });
            var c = new TcpCDiscoverAppInstances(appId, nonce);
            TCPConnection.Stream.Write(c.Build());
            return await ss.Task;
        }

        public static async Task StandardSocketControlHandler(Packet packet)
        {
            var pa = new TcpCSSocketControl(packet);
            var ep = ((EntryPoint) packet.Connection.Ref);
            Log.Debug("Socket Control Detail for socket {Socket}: [{Flags}] {Extra}", pa.SocketId, pa.ReadableFlags, pa.InstanceId);
            AppConnection x = null;
            ep.Connections.TryGetValue(AppConnection.IdToString(pa.SocketId), out x);
            StoredApp app = null;
            if (pa.CheckFlag(TcpCSSocketControl.OpenRequest))
            {
                if (x == null)
                {
                    x = new AppConnection(pa.InstanceId, ep);
                    x.SocketId = pa.SocketId;
                }
                else
                {
                    x.Loopback = true;
                    Log.Debug("Connection {Sid} switched to loopback mode", pa.SocketId);
                }
                app = ep._registeredApps.FirstOrDefault(e => e.InstanceId.SequenceEqual(x.InstanceId));
                await app.Callback(x);
            }
            if (x == null)
            {
                return;
            }

            if (pa.CheckFlag(TcpCSSocketControl.Ready))
            {
                x.Open = true;
                x.ConnectCompletionSource.SetResult(true);
            }

            if (pa.CheckFlag(TcpCSSocketControl.Close))
            {
                if (!x.Open)
                {
                    x.ConnectCompletionSource.SetResult(false);
                }
            }
        }

        public static async Task StandardDataHandler(Packet packet)
        {
            await Task.Run(() =>
            {
                TcpCSSocketData data = new TcpCSSocketData(packet);
                var ep = ((EntryPoint) packet.Connection.Ref);
                AppConnection x = null;
                ep.Connections.TryGetValue(AppConnection.IdToString(data.SocketId), out x);
                if (x == null) return;
                x.Stream.Buffer.AddRange(data.SocketData);
            });
        }
    }
}