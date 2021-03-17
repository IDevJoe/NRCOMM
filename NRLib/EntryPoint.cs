using System;
using System.Collections.Generic;
using System.IO;
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

        public IPEndPoint Address { get; }
        public X509Certificate2 Certificate { get; }
        private TcpClient _tcp;
        
        public X509Certificate2 ClientCertificate { get; private set; }

        internal TcpConnection _tcpConnection;

        public Dictionary<string, AppConnection> Connections = new Dictionary<string, AppConnection>();

        internal EntryPoint(IPEndPoint ep, X509Certificate2 cert)
        {
            Address = ep;
            Certificate = cert;
        }

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
                    Close();
                }
            });
        }

        public void Close()
        {
            if (_tcp != null)
            {
                _tcp.Close();
                _tcp = null;
            }
        }

        private async Task BeginLoop()
        {
            _tcpConnection = new TcpConnection()
            {
                Socket = _tcp.Client,
                Ref = this
            };
            using (NetworkStream stream = _tcp.GetStream())
            {
                _tcpConnection.Stream = stream;
                if (Certificate == null)
                {
                    while (true)
                    {
                        Packet pkt = new Packet(stream, _tcpConnection);
                        pkt.ExecuteRoutine();
                    }
                }
                else
                {
                    using (SslStream str2 = new SslStream(stream))
                    {
                        _tcpConnection.Stream = str2;
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
                            Packet pkt = new Packet(str2, _tcpConnection);
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

        public delegate void ConnectionEstablishCallback(AppConnection dataStream);

        public void Publish(string description, ConnectionEstablishCallback callback)
        {
            if (_tcp == null || !_tcp.Connected) return;
            uint n = Packet.WatchNonce(packet =>
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
            var pub = new TcpCPublish(description, n);
            byte[] x = pub.Build();
            _tcpConnection.Stream.Write(x);
        }

        public async Task<byte[][]> DiscoverApps(string description)
        {
            byte[] AppId = new byte[10];
            using (SHA1 sha1 = SHA1.Create())
            {
                AppId = sha1.ComputeHash(Encoding.UTF8.GetBytes(description)).Take(10).ToArray();
            }

            byte[][] reply = null;
            TaskCompletionSource<byte[][]> ss = new TaskCompletionSource<byte[][]>();
            uint nonce = Packet.WatchNonce(packet =>
            {
                var pack = new TcpSAppInstanceReply(packet);
                ss.SetResult(pack.Instances);
            });
            var c = new TcpCDiscoverAppInstances(AppId, nonce);
            _tcpConnection.Stream.Write(c.Build());
            return await ss.Task;
        }

        public static void StandardSocketControlHandler(Packet packet)
        {
            var pa = new TcpCSSocketControl(packet);
            var ep = ((EntryPoint) packet.Connection.Ref);
            Log.Debug("Socket Control Detail for socket {Socket}: [{Flags}] {Extra}", pa.SocketId, pa.ReadableFlags, pa.InstanceId);
            AppConnection x = null;
            ep.Connections.TryGetValue(AppConnection.IdToString(pa.SocketId), out x);
            StoredApp app = null;
            if (pa.CheckFlag(TcpCSSocketControl.OPEN_REQUEST))
            {
                if (x == null)
                {
                    x = new AppConnection(pa.InstanceId, ep);
                    x.SocketId = pa.SocketId;
                }
                else
                {
                    x.Loopback = true;
                    Log.Debug("Connection {SID} switched to loopback mode", pa.SocketId);
                }
                app = ep._registeredApps.FirstOrDefault(e => e.InstanceId.SequenceEqual(x.InstanceId));
                app.Callback(x);
            }
            if (x == null)
            {
                return;
            }

            if (pa.CheckFlag(TcpCSSocketControl.READY))
            {
                x.Open = true;
                x.ConnectCompletionSource.SetResult(true);
            }

            if (pa.CheckFlag(TcpCSSocketControl.CLOSE))
            {
                if (!x.Open)
                {
                    x.ConnectCompletionSource.SetResult(false);
                }
            }
        }

        public static void StandardDataHandler(Packet packet)
        {
            TcpCSSocketData data = new TcpCSSocketData(packet);
            var ep = ((EntryPoint) packet.Connection.Ref);
            AppConnection x = null;
            ep.Connections.TryGetValue(AppConnection.IdToString(data.SocketId), out x);
            if (x == null) return;
            x.Stream._buffer.AddRange(data.SocketData);
        }
    }
}