using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using NRLib;
using NRLib.Packets;

namespace NREP
{
    public class AppConnection
    {
        public AppManager.PublishedApp App;
        public TcpConnection Connection;
        public bool Accepted;
        public byte[] SocketId;

        public static Dictionary<string, AppConnection> Connections = new Dictionary<string, AppConnection>();
        public AppConnection(TcpConnection connection, AppManager.PublishedApp app)
        {
            Connection = connection;
            App = app;
            using (SHA1 sha1 = SHA1.Create())
            {
                SocketId = sha1.ComputeHash(Encoding.UTF8.GetBytes(DateTime.Now + "-" +
                                                                   connection.Socket.RemoteEndPoint + "-" +
                                                                   app.Description)).Take(10).ToArray();
            }
            Connections.Add(IdToString(SocketId), this);
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

        public async Task SendInitialState(Packet requestPacket)
        {
            Connection.Stream.Write((new TcpCSSocketControl(SocketId,
                TcpCSSocketControl.OpenAck, requestPacket.Nonce)).Build());
            await App.Connection.Stream.WriteAsync((new TcpCSSocketControl(SocketId, TcpCSSocketControl.OpenRequest, 0, App.InstanceId)).Build());
        }

        public async Task ProcessControl(Packet packet)
        {
            TcpCSSocketControl control = new TcpCSSocketControl(packet);
            if(App.Connection.Socket == packet.Connection.Socket) await ProcessRecControl(control);
            else await ProcessReqControl(control);
        }

        public async Task Send(Socket source, byte[] data)
        {
            if (!Accepted) return;
            TcpCSSocketData da1 = new TcpCSSocketData(SocketId, data);
            if (source == Connection.Socket)
            {
                await App.Connection.Stream.WriteAsync(da1.Build());
            } else if (source == App.Connection.Socket)
            {
                await Connection.Stream.WriteAsync(da1.Build());
            }
        }

        public async Task ProcessReqControl(TcpCSSocketControl control)
        {
            byte newControl = 0;
            if (control.CheckFlag(TcpCSSocketControl.Close))
            {
                newControl |= TcpCSSocketControl.Close;
                Connections.Remove(IdToString(SocketId));
            }
            await App.Connection.Stream.WriteAsync((new TcpCSSocketControl(SocketId, newControl)).Build());
        }

        public async Task ProcessRecControl(TcpCSSocketControl control)
        {
            byte newControl = 0;
            bool close = false;
            if (!Accepted)
            {
                if (control.CheckFlag(TcpCSSocketControl.AcceptConnection))
                {
                    Accepted = true;
                    newControl |= TcpCSSocketControl.Ready;
                } else if (control.CheckFlag(TcpCSSocketControl.RefuseConnection))
                {
                    newControl |= TcpCSSocketControl.Close;
                    close = true;
                }
            }

            if (control.CheckFlag(TcpCSSocketControl.Close))
            {
                newControl |= TcpCSSocketControl.Close;
                close = true;
            }
            await Connection.Stream.WriteAsync((new TcpCSSocketControl(SocketId, newControl)).Build());
            if (close)
            {
                Connections.Remove(IdToString(SocketId));
            }
        }

        public async Task ForceClose()
        {
            byte[] control = (new TcpCSSocketControl(SocketId, TcpCSSocketControl.Close)).Build();
            if(Connection.Socket.Connected)
                await Connection.Stream.WriteAsync(control);
            if(App.Connection.Socket.Connected)
                await App.Connection.Stream.WriteAsync(control);
        }
    }
}