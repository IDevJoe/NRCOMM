using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using NRLib.Exceptions;
using Serilog;

namespace NRLib
{
    public class Packet
    {
        public PackType PacketType { get; protected set; }
        public uint Nonce { get; protected set; }
        public byte[] Data { get; protected set; }
        public string PacketID { get; private set; }
        public TransportType TransType { get; private set; }
        public IPEndPoint ClientAddress { get; private set; }
        public TcpConnection Connection { get; private set; }

        public delegate void HandlePacket(Packet packet);

        private static Dictionary<PackType, List<HandlePacket>> PackRoutines =
            new Dictionary<PackType, List<HandlePacket>>();

        private static Dictionary<uint, HandlePacket> NonceWatch = new Dictionary<uint, HandlePacket>();

        private static Random _random = new Random();

        public static void StorePacketRoutine(PackType type, HandlePacket callback)
        {
            if (!PackRoutines.ContainsKey(type))
            {
                PackRoutines.Add(type, new List<HandlePacket>());
            }
            PackRoutines[type].Add(callback);
        }

        public static uint WatchNonce(HandlePacket callback)
        {
            uint nonce = (uint) _random.Next(0, Int32.MaxValue);
            Log.Debug("Stored nonce {Nonce} for a pre-defined routine", nonce);
            NonceWatch.Add(nonce, callback);
            return nonce;
        }

        public void ExecuteRoutine()
        {
            if (NonceWatch.ContainsKey(Nonce))
            {
                Log.Debug("Nonce {Nonce} executed as {PacketType}", Nonce, PacketType);
                NonceWatch[Nonce](this);
                NonceWatch.Remove(Nonce);
                return;
            }
            if(Nonce != 0) Log.Debug("Nonce {Nonce} is not being looked for", Nonce);
            if (!PackRoutines.ContainsKey(PacketType)) return;
            foreach (var callback in PackRoutines[PacketType])
            {
                callback(this);
            }
        }

        public enum TransportType
        {
            UDP,
            TCP
        }
        
        public Packet(byte[] payload, IPEndPoint clientAddress)
        {
            TransType = TransportType.UDP;
            ClientAddress = clientAddress;
            _parseData(payload);
        }

        public Packet(Stream stream, TcpConnection connection)
        {
            TransType = TransportType.TCP;
            Connection = connection;
            _fromStream(stream, true);
        }

        protected Packet()
        {
            
        }

        public byte[] Build(bool output = true)
        {
            using(MemoryStream str = new MemoryStream())
            using (BinaryWriter writ = new BinaryWriter(str))
            {
                writ.Write((byte)0x00);
                writ.Write((byte)PacketType);
                writ.Write(Nonce);
                writ.Write((uint)Data.Length);
                writ.Write(Data);
                byte[] res = str.ToArray();
                PacketID = GenPID(res);
                if(output) Log.Debug("Built packet {Packet} ({PType}) with a total length of {Length}: {@Data}", this.PacketID, this.PacketType, res.Length, res);
                return res;
            }
        }

        public virtual void Handle()
        {
            ExecuteRoutine();
        }

        public static string GenPID(byte[] payload)
        {
            var PacketID = "";
            using (var sha1 = SHA1.Create())
            {
                byte[] hash = sha1.ComputeHash(payload);
                for (int i = 0; i < 5; i++)
                {
                    PacketID += hash[i].ToString("x2");
                }
            }

            return PacketID;
        }

        private void _parseData(byte[] payload)
        {
            if (payload.Length < 10)
            {
                throw new InvalidPayloadException("Payload was less than 10 bytes");
            }
            PacketID = GenPID(payload);
            Log.Debug("Parsing Packet ID {PacketID} (Transport {Transport}, Address {IP}) of length {Length}: {@Packet}", PacketID, TransType, ClientAddress, payload.Length, payload);
            using(MemoryStream str = new MemoryStream(payload))
                _fromStream(str);
        }

        private void _fromStream(Stream str, bool leaveOpen = false)
        {
            using (BinaryReader reader = new BinaryReader(str, Encoding.Default, leaveOpen))
            {
                byte res = reader.ReadByte();
                if (res != 0x00)
                {
                    throw new InvalidPayloadException("Reserved byte was not zero, but was instead " + res.ToString("x2"));
                }

                byte type = reader.ReadByte();
                if (!Enum.IsDefined(typeof(PackType), (int) type))
                {
                    throw new InvalidPayloadException("Packet type invalid, client sent " + type.ToString("x2"));
                }

                PacketType = (PackType) type;
                Nonce = reader.ReadUInt32();
                uint cl = reader.ReadUInt32();
                if (str.CanSeek)
                {
                    long left = str.Length - str.Position;
                    if (cl > left)
                    {
                        throw new InvalidPayloadException("Content length (" + cl +
                                                          ") is greater than remaining payload length (" + left + ")");
                    }
                }

                Data = reader.ReadBytes((int)cl);
                PacketID = GenPID(Build(false));
                Log.Debug("Finished parsing packet {PacketID}. Type {Type}, Nonce {Nonce}, Data Length {DataLen}", PacketID,
                    PacketType, Nonce, Data.Length);
            }
        }
    }
}