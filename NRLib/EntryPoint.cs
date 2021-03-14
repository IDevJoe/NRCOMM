using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
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

        private TcpConnection _tcpConnection;

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
                Socket = _tcp.Client
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

        public delegate void ConnectionEstablishCallback(Stream dataStream);

        public void Publish(string description, ConnectionEstablishCallback callback)
        {
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
    }
}