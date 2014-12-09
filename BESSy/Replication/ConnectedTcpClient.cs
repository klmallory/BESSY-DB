using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Sockets;

namespace BESSy.Replication
{
    public class ConnectedTcpClient
    {
        //public ConnectedTcpClient(TcpClient client) : this(client, Guid.NewGuid())
        //{

        //}

        public ConnectedTcpClient(TcpClient client, Guid id)
        {
            ConnectionId = id;
            Client = client;
        }

        public Guid ConnectionId { get; protected set; }
        public TcpClient Client { get; protected set; }
    }
}
