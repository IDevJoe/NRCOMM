using System;
using System.Collections.Generic;
using System.Reflection;
using NRLib;
using Serilog;

namespace NRLib.Packets.Attributes
{
    [AttributeUsage(AttributeTargets.Class)]
    public class PacketHandler : Attribute
    {
        public static Dictionary<PackType, Type> Associations = new Dictionary<PackType, Type>();
        
        public PackType Type { get; }
        public PacketHandler(PackType type)
        {
            this.Type = type;
        }

        public static void Associate()
        {
            foreach(var t in typeof(NRL).Assembly.GetTypes())
            {
                Attribute at = t.GetCustomAttribute(typeof(PacketHandler));
                if (at == null) continue;
                PacketHandler h = (PacketHandler) at;
                Associations.Add(h.Type, t);
                Log.Debug("{Type} associated with packet type {PType}", t.FullName, h.Type);
            }
        }
    }
}