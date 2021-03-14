using System.Collections.Generic;
using NRLib;

namespace NREP
{
    public class AppManager
    {
        public class PublishedApp
        {
            public byte[] AppId;
            public byte[] InstanceId;
            public TcpConnection Connection;
            public string Description;
        }

        public static List<PublishedApp> PublishedApps = new List<PublishedApp>();
    }
}