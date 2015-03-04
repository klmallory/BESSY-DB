using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Sockets;

namespace BESSy.Replication.Tcp
{
    public class TcpSettings
    {
        public bool ExclusiveAddressUse = false;
        public int SendTimeout = 300000;
        public bool NoDelay = false;
        public bool LingerStateEnabled = true;
        public int ReceiveTimeout = 300000;
        public int ReceiveBufferSize = 1024;
        public int SendBufferSize = 1024;
    }

    public class TcpListenerSettings
    {
        //public IPProtectionLevel IpProtectionLevel = IPProtectionLevel.Unrestricted;
        public bool ExclusiveAddressUse = false;
        public bool DontFragment = true;
        public bool Linger = true;
        public int LingerTime = 3;
    }
}
