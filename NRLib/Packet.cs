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
    /// <summary>
    /// Represents a sent/received packet
    /// </summary>
    public class Packet
    {
        /// <summary>
        /// The type of packet
        /// </summary>
        public PackType PacketType { get; protected set; }
        
        /// <summary>
        /// The nonce
        /// </summary>
        public uint Nonce { get; protected set; }
        
        /// <summary>
        /// Packet Data
        /// </summary>
        public byte[] Data { get; protected set; }
        
        /// <summary>
        /// Auto generated packet ID
        /// </summary>
        public string PacketID { get; private set; }
        
        /// <summary>
        /// How the packet arrived. Only set if the packet was received.
        /// </summary>
        public TransportType TransType { get; private set; }
        
        /// <summary>
        /// The distant end IP address. Only set if the packet was received via UDP.
        /// </summary>
        public IPEndPoint ClientAddress { get; private set; }
        
        /// <summary>
        /// The associated TCP connection. Only set if the packet was received via TCP.
        /// </summary>
        public TcpConnection Connection { get; private set; }

        public delegate void HandlePacket(Packet packet);

        private static Dictionary<PackType, List<HandlePacket>> PackRoutines =
            new Dictionary<PackType, List<HandlePacket>>();

        private static Dictionary<uint, HandlePacket> NonceWatch = new Dictionary<uint, HandlePacket>();

        private static Random _random = new Random();

        /// <summary>
        /// Stores a new packet routine
        /// </summary>
        /// <param name="type">Packet Type</param>
        /// <param name="callback">Method to be called when the packet arrives</param>
        public static void StorePacketRoutine(PackType type, HandlePacket callback)
        {
            if (!PackRoutines.ContainsKey(type))
            {
                PackRoutines.Add(type, new List<HandlePacket>());
            }
            PackRoutines[type].Add(callback);
        }

        /// <summary>
        /// Generates and stores a nonce, then calls the method when the packet is received
        /// </summary>
        /// <param name="callback">The method to call</param>
        /// <returns>A nonce</returns>
        public static uint WatchNonce(HandlePacket callback)
        {
            uint nonce = (uint) _random.Next(0, Int32.MaxValue);
            Log.Debug("Stored nonce {Nonce} for a pre-defined routine", nonce);
            NonceWatch.Add(nonce, callback);
            return nonce;
        }

        /// <summary>
        /// Executes the packet
        /// </summary>
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
        
        /// <summary>
        /// Creates a new packet with the specified payload and UDP endpoint
        /// </summary>
        /// <param name="payload">The received payload</param>
        /// <param name="clientAddress">A client IP address</param>
        public Packet(byte[] payload, IPEndPoint clientAddress)
        {
            TransType = TransportType.UDP;
            ClientAddress = clientAddress;
            _parseData(payload);
        }

        /// <summary>
        /// Creates a new packet from a stream
        /// </summary>
        /// <param name="stream">The stream to read from</param>
        /// <param name="connection">The associated connection</param>
        public Packet(Stream stream, TcpConnection connection)
        {
            TransType = TransportType.TCP;
            Connection = connection;
            _fromStream(stream, true);
        }

        protected Packet()
        {
            
        }

        /// <summary>
        /// Builds a new packet using the set data
        /// </summary>
        /// <param name="output">Whether to log the packet build</param>
        /// <returns>The built packet bytes</returns>
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

        /// <summary>
        /// Generates a packet ID using SHA1
        /// </summary>
        /// <param name="payload">The packet payload</param>
        /// <returns>A packet ID</returns>
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