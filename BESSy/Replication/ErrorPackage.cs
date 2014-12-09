using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BESSy.Replication
{
    public enum TcpErrorCode : byte
    {
        None = 0,
        UnhandledException,
        AuthenticationTokenExpired,
        MalformedHeader,
        MalformedPackage
    }

    public class ErrorPackage
    {
        public TcpErrorCode Code { get; set; }
        public string Message { get; set; }
    }
}
