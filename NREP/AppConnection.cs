using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using NRLib;
using NRLib.Packets;

namespace NREP
{
    public class AppConnection
    {
        public AppManager.PublishedApp App = null;
        public TcpConnection Connection;
        public bool Accepted = false;
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

        public void SendInitialState(Packet requestPacket)
        {
            Connection.Stream.Write((new TcpCSSocketControl(SocketId,
                TcpCSSocketControl.OPEN_ACK, requestPacket.Nonce)).Build());
            App.Connection.Stream.Write((new TcpCSSocketControl(SocketId, TcpCSSocketControl.OPEN_REQUEST, 0, App.InstanceId)).Build());
        }

        public void ProcessControl(Packet packet)
        {
            TcpCSSocketControl control = new TcpCSSocketControl(packet);
            if(Connection.Socket == packet.Connection.Socket) ProcessReqControl(control);
            else ProcessRecControl(control);
        }

        public void Send(Socket source, byte[] data)
        {
            if (Accepted) return;
            TcpCSSocketData da1 = new TcpCSSocketData(SocketId, data);
            if (source == Connection.Socket)
            {
                App.Connection.Stream.Write(da1.Build());
            } else if (source == App.Connection.Socket)
            {
                Connection.Stream.Write(da1.Build());
            }
        }

        public void ProcessReqControl(TcpCSSocketControl control)
        {
            byte newControl = 0;
            if (control.CheckFlag(TcpCSSocketControl.CLOSE))
            {
                newControl |= TcpCSSocketControl.CLOSE;
                Connections.Remove(IdToString(SocketId));
            }
            Connection.Stream.Write((new TcpCSSocketControl(SocketId, newControl)).Build());
        }

        public void ProcessRecControl(TcpCSSocketControl control)
        {
            byte newControl = 0;
            bool close = false;
            if (!Accepted)
            {
                if (control.CheckFlag(TcpCSSocketControl.ACCEPT_CONNECTION))
                {
                    Accepted = true;
                    newControl |= TcpCSSocketControl.READY;
                } else if (control.CheckFlag(TcpCSSocketControl.REFUSE_CONNECTION))
                {
                    newControl |= TcpCSSocketControl.CLOSE;
                    close = true;
                }
            }

            if (control.CheckFlag(TcpCSSocketControl.CLOSE))
            {
                newControl |= TcpCSSocketControl.CLOSE;
                close = true;
            }
            Connection.Stream.Write((new TcpCSSocketControl(SocketId, newControl)).Build());
            if (close)
            {
                Connections.Remove(IdToString(SocketId));
            }
        }

        public void ForceClose()
        {
            byte[] control = (new TcpCSSocketControl(SocketId, TcpCSSocketControl.CLOSE)).Build();
            if(Connection.Socket.Connected)
                Connection.Stream.Write(control);
            if(App.Connection.Socket.Connected)
                App.Connection.Stream.Write(control);
        }
    }
}