using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using BESSy.Replication;
using BESSy.Serialization;
using BESSy.Replication.Tcp;

namespace BESSy.Tests.Mocks.Tcp
{
    public class MockTcpSubscriber<IdType, EntityType> : TcpTransactionSubscriber<IdType, EntityType>
    {
        public MockTcpSubscriber(int port)
            : base(port)
        {

        }

        public bool ThrowReadError { get; set; }
        public bool ThrowAuthError { get; set; }
        public bool ThrowReadException { get; set; }
        int exceptions = 0;

        protected override bool ReadInAuthentication(ConnectedTcpClient client, System.IO.MemoryStream readStream)
        {
            if (ThrowAuthError)
            {
                base._formatter.UnformatObj<AuthPackage>(readStream);

                SendError(client.Client, TcpErrorCode.UnhandledException, "Error Test");

                return false;
            }
            else
                return base.ReadInAuthentication(client, readStream);
        }

        protected override bool ReadInTransaction(System.Net.Sockets.TcpClient client, System.IO.MemoryStream readStream)
        {
            if (ThrowReadError)
            {
                base._formatter.UnformatObj<TcpTransactionBuffer<IdType, EntityType>>(readStream);

                SendError(client, TcpErrorCode.UnhandledException, "Error Test", new object());

                return false;
            }
            else if (ThrowReadException)
            {
                exceptions++;

                if (exceptions == 1)
                    throw new Json.JsonSerializationException("Some bad json");
                else
                    throw new SocketException(6);
            }
            else
                return base.ReadInTransaction(client, readStream);
        }
    }
}
