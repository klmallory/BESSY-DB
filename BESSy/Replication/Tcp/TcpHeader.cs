using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BESSy.Replication.Tcp
{
    public static class TcpHeader
    {
        public static readonly int HEADER_LENGTH = 6;
        public static readonly ArraySegment<byte> PING = new ArraySegment<byte>(new byte[] { 1, 1, 1, 2, 2, 2 });
        public static readonly ArraySegment<byte> PACKAGE_START = new ArraySegment<byte>(new byte[] { 3, 3, 3, 4, 4, 4 });
        public static readonly ArraySegment<byte> AUTH_START = new ArraySegment<byte>(new byte[] { 5, 5, 5, 6, 6, 6 });
        public static readonly ArraySegment<byte> ACK = new ArraySegment<byte>(new byte[] { 7, 7, 7, 8, 8, 8 });
        public static readonly ArraySegment<byte> ERROR = new ArraySegment<byte>(new byte[] { 9, 9, 9, 10, 10, 10 });
    }
}
