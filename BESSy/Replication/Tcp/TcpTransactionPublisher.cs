using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Timers;
using BESSy.Extensions;
using BESSy.Serialization;
using BESSy.Transactions;

namespace BESSy.Replication.Tcp
{
    public class TcpTransactionPublisher<IdType, EntityType> : IReplicationPublisher<IdType, EntityType>
    {
        public TcpTransactionPublisher(IPAddress publishedEndPoint, int port)
            : this(publishedEndPoint, port, 20, new TcpSettings())
        {

        }

        public TcpTransactionPublisher(IPAddress publishedEndPoint, int port, int interval) 
            : this(publishedEndPoint, port, interval, new TcpSettings())
        {

        }

        public TcpTransactionPublisher(IPAddress publishEndPoint, int port, int interval, TcpSettings settings)
            : this(publishEndPoint, port, interval, new LZ4ZipFormatter(new BSONFormatter(), false, 1024000), settings)
        {
            
        }

        public TcpTransactionPublisher(IPAddress publishEndPoint
            , int port
            , int interval
            , IQueryableFormatter formatter
            , TcpSettings settings) 
        {
            _publishEndPoint = publishEndPoint;
            _port = port;

            _formatter = formatter;
            _settings = settings;
            _queueTimer = new System.Threading.Timer(new TimerCallback(PublishQueue), null, interval, interval);
        }

        object _syncRoot = new object();
        object _syncQueue = new object();

        Queue<ITransaction<IdType, EntityType>> _queue = new Queue<ITransaction<IdType, EntityType>>();
        IQueryableFormatter _formatter;
        IPAddress _publishEndPoint;
        int _port;
        System.Threading.Timer _queueTimer;
        Tuple<Guid, long> _authToken;
        TcpSettings _settings;

        protected long GetTimeoutMarker()
        {
            return DateTime.Now.Ticks + (2 * 10000000);
        }

        protected TcpClient CreateClient()
        {
            var client = new TcpClient();
            client.ExclusiveAddressUse = _settings.ExclusiveAddressUse;
            client.SendTimeout = _settings.SendTimeout;
            client.NoDelay = _settings.NoDelay;
            client.LingerState.Enabled = _settings.LingerStateEnabled;
            client.ReceiveTimeout = _settings.ReceiveTimeout;
            client.ReceiveBufferSize = _settings.ReceiveBufferSize;
            client.SendBufferSize = _settings.SendBufferSize;

            return client;
        }

        private void Connect(TcpClient client)
        {
            client.Connect(_publishEndPoint, _port);
        }

        protected virtual void PublishQueue(object state)
        {
            Guid workingId = Guid.Empty;
            bool locked = false;

            try
            {
                if (Monitor.TryEnter(_syncRoot, 250))
                {
                    locked = true;

                    if (_authToken == null || DateTime.Now.Ticks > _authToken.Item2)
                        using (var client = CreateClient()) 
                        {
                            Connect(client);
                            Authenticate(client, Database.TransactionSource); 
                        }

                    var count = 0;

                    lock (_syncQueue)
                        count = _queue.Count;

                    if (count > 0 && _authToken != null)
                    {
                        using (var client = CreateClient())
                        {
                            Connect(client);

                            if (!client.Connected)
                                return;

                            while (count > 0)
                            {
                                TcpTransactionBuffer<IdType, EntityType> trans = null;

                                lock (_syncQueue)
                                {
                                    trans = new TcpTransactionBuffer<IdType, EntityType>(_authToken.Item1, _queue.Peek());

                                    workingId = trans.Transaction.Id;

                                    var transBuffer = _formatter.FormatObj(trans);

                                    byte[] buffer = new byte[transBuffer.Length + TcpHeader.HEADER_LENGTH];

                                    Array.Copy(TcpHeader.PACKAGE_START.Array, buffer, TcpHeader.PACKAGE_START.Array.Length);
                                    Array.Copy(transBuffer, 0, buffer, TcpHeader.PACKAGE_START.Array.Length, transBuffer.Length);

                                    var netStream = client.GetStream();
                                    netStream.Write(buffer, 0, buffer.Length);

                                    if (!ReadResponse(client, workingId))
                                        break;

                                    _queue.Dequeue();

                                    count = _queue.Count;
                                }
                            }
                        }
                    }
                }
            }
            catch (IOException ioEx)
            {
                var rEx = new ReplicationException(workingId, ioEx.Message, ioEx);
                Trace.TraceError(ioEx.ToString());
            }
            catch (SocketException soEx)
            {
                var rEx = new ReplicationException(workingId, soEx.Message, soEx);
                Trace.TraceError(soEx.ToString());
            }
            catch (SystemException sysEx)
            {
                var rEx = new ReplicationException(workingId, sysEx.Message, sysEx);
                Trace.TraceError(sysEx.ToString());
            }
            finally
            {
                if (locked)
                    Monitor.Exit(_syncRoot);

                locked = false;
            }
        }

        private bool ReadResponse(TcpClient client)
        {
            var ticks = GetTimeoutMarker();

            while (client.Connected && client.Available < 1 && DateTime.Now.Ticks < ticks)
                    Thread.Sleep(400);

            if (!client.Connected || DateTime.Now.Ticks > ticks)
                return false;

            var buffer = new byte[client.Available - TcpHeader.HEADER_LENGTH];
            var header = new byte[TcpHeader.HEADER_LENGTH];

            var inStream = client.GetStream();

            inStream.Read(header, 0, header.Length);
            var read = inStream.Read(buffer, 0, buffer.Length);

            if (TcpHeader.ACK.Equals<byte>(new ArraySegment<byte>(header)))
            {
                lock (_syncRoot)
                    _authToken = _formatter.UnformatObj<Tuple<Guid, long>>(buffer);

                return true;
            }
            else if (TcpHeader.ERROR.Equals<byte>(new ArraySegment<byte>(header)))
            {
                var errorPackage = _formatter.UnformatObj<ErrorPackage>(buffer.ToArray());

                Trace.TraceError("Socket error: {0} : ", errorPackage.Message);

                return false;
            }
            else
                throw new ReplicationException(Guid.Empty, string.Format("Authentication response not understood: {0}", Encoding.Unicode.GetString(buffer)));
        }

        private bool ReadResponse(TcpClient client, Guid? transactionId)
        {
            var ticks = GetTimeoutMarker();

            while (client.Connected && client.Available < 1 && DateTime.Now.Ticks < ticks)
                    Thread.Sleep(400);

            if (!client.Connected || DateTime.Now.Ticks > ticks)
                return false;

            var buffer = new byte[client.Available];
            var header = new byte[TcpHeader.HEADER_LENGTH];

            var inStream = client.GetStream();

            inStream.Read(header, 0, header.Length);
            var read = inStream.Read(buffer, 0, buffer.Length);

            if (TcpHeader.ACK.Equals<byte>(new ArraySegment<byte>(header)))
                return true;
            else if (TcpHeader.ERROR.Equals<byte>(new ArraySegment<byte>(header)))
            {
                var errorPackage = _formatter.UnformatObj<ErrorPackage>(buffer.ToArray());

                if (errorPackage.Code == TcpErrorCode.AuthenticationTokenExpired)
                    _authToken = null;

                Trace.TraceError("Socket error: {0} : trans : {1}", errorPackage.Message, transactionId);

                return false;
            }
            else
                throw new ReplicationException(transactionId.GetValueOrDefault(), string.Format("Response not understood: {0}", Encoding.Unicode.GetString(buffer)));
        }

        private void Authenticate(TcpClient client, Guid guid)
        {
            var auth = _formatter.FormatObjStream(new AuthPackage() { SourceId = guid } );
            var package = client.GetStream();

            package.Write(TcpHeader.AUTH_START.Array, 0, TcpHeader.HEADER_LENGTH);
            auth.WriteAllTo(package);

            ReadResponse(client);
        }

        public AbstractTransactionalDatabase<IdType, EntityType> Database { get; set; }
        public string[] Settings { get; set; }

        public void Publish(ITransaction<IdType, EntityType> transaction)
        {
            if (transaction == null || transaction.EnlistCount < 1)
                return;

            lock (_syncQueue)
                _queue.Enqueue(transaction);

            return;
        }

        public void Dispose()
        {
            _queueTimer.Change(5, 0);

            Thread.Sleep(1000);

            if (Monitor.TryEnter(_syncRoot, 250))
            {
                Database = null;
                Monitor.Exit(_syncRoot);
            }
        }
    }
}
