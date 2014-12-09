using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using BESSy.Replication;
using BESSy.Replication.Tcp;
using BESSy.Serialization;


namespace BESSy.Tests.Mocks.Tcp
{
    public class MockTcpPublisher<IdType, EntityType> : TcpTransactionPublisher<IdType, EntityType>
    {
        public MockTcpPublisher(IPAddress publishEndPoint, int port, int interval, TcpSettings settings)
            : base(publishEndPoint, port, interval, settings)
        {

        }


    }
}
