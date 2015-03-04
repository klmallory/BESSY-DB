using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Timers;
using BESSy.Extensions;
using BESSy.Serialization;
using System.Threading.Tasks;
using System.IO;
using BESSy.Transactions;
using BESSy.Json;

namespace BESSy.Replication.Tcp
{
    public class TcpTransactionSubscriber<IdType, EntityType> : IReplicationSubscriber<IdType, EntityType>
    {
        public TcpTransactionSubscriber()
            : this(8355)
        {

        }

        public TcpTransactionSubscriber(int port)
            : this(port, new TcpListenerSettings(), new LZ4ZipFormatter(new BSONFormatter(), false, 1024000))
        {

        }

        public TcpTransactionSubscriber(int port, TcpListenerSettings settings)
            : this(port, settings, new LZ4ZipFormatter(new BSONFormatter(), false, 1024000))
        {

        }

        public TcpTransactionSubscriber(int port, TcpListenerSettings settings, IQueryableFormatter formatter)
        {
            _settings = settings;

            _listener = CreateListener(port);

            _formatter = formatter;

            _listenThread = new Thread(new ThreadStart(Listen));
            _listenThread.Start();
        }

        object _syncRoot = new object();
        object _syncConnect = new object();
        object _syncClient = new object();

        bool _active = true;

        TcpListenerSettings _settings;
        TcpListener _listener;
        Thread _listenThread;

        Dictionary<EndPoint, Tuple<Guid, Guid, long>> _authTokens = new Dictionary<EndPoint, Tuple<Guid, Guid, long>>();

        protected IQueryableFormatter _formatter;

        protected TcpListener CreateListener(int port)
        {
            var listener = new TcpListener(IPAddress.Any, port);
            //listener.Server.SetIPProtectionLevel(_settings.IpProtectionLevel);
            listener.Server.ExclusiveAddressUse = _settings.ExclusiveAddressUse;
            listener.Server.DontFragment = _settings.DontFragment;
            listener.Server.LingerState = _settings.Linger ? new LingerOption(_settings.Linger, _settings.LingerTime) : new LingerOption(false, 0);

            return listener;

        }
        protected virtual void CloseSocket(TcpClient client)
        {
            if (client != null)
                client.Close();
        }

        protected virtual void CloseSocket(ConnectedTcpClient client)
        {
            if (client != null && client.Client != null)
                client.Client.Close();
        }

        protected virtual bool ReadBufferFrom(ConnectedTcpClient connectedClient)
        {
            if (connectedClient == null)
                return false;

            var client = connectedClient.Client;

            try
            {
                using (NetworkStream inStream = client.GetStream())
                {
                    var buffer = new byte[Environment.SystemPageSize];
                    var header = new byte[TcpHeader.HEADER_LENGTH];

                    using (var readStream = new MemoryStream())
                    {
                        inStream.Read(header, 0, header.Length);

                        while (inStream.DataAvailable)
                        {
                            var read = inStream.Read(buffer, 0, buffer.Length);

                            readStream.Write(buffer, 0, read);
                        }

                        readStream.Position = 0;

                        if (TcpHeader.AUTH_START.Equals<byte>(new ArraySegment<byte>(header)))
                        {
                            return ReadInAuthentication(connectedClient, readStream);
                        }
                        else if (TcpHeader.PACKAGE_START.Equals<byte>(new ArraySegment<byte>(header)))
                        {
                            return ReadInTransaction(connectedClient.Client, readStream);
                        }
                        else
                            SendError(client, TcpErrorCode.MalformedHeader, "netStream header not understood: {0}", Encoding.ASCII.GetString(header));
                    }
                }
            }
            catch (SocketException soEx)
            {
                SendError(client, TcpErrorCode.UnhandledException, soEx.ToString());

                Trace.TraceError(soEx.ToString());
                CloseSocket(client);
            }

            return false;
        }

        protected virtual bool ReadInAuthentication(ConnectedTcpClient client, MemoryStream readStream)
        {
            var auth = _formatter.UnformatObj<AuthPackage>(readStream);

            if (auth == null)
                return false;

            if (auth.SourceId != Database.TransactionSource)
            {
                var token = new Tuple<Guid, Guid, long>(auth.SourceId, Guid.NewGuid(), DateTime.Now.Ticks + 10000000L * 900L);

                _authTokens.Add(client.Client.Client.RemoteEndPoint, token);

                var obj = _formatter.FormatObj(new Tuple<Guid, long>(token.Item2, token.Item3));
                
                var package = new byte[TcpHeader.ACK.Array.Length + obj.Length];
                
                Array.Copy(TcpHeader.ACK.Array, package, TcpHeader.ACK.Array.Length);
                Array.Copy(obj, 0, package, TcpHeader.ACK.Array.Length, obj.Length);

                var outStream = client.Client.GetStream();

                outStream.Write(package, 0, package.Length);
                outStream.Flush();

                return true;
            }

            return false;
        }

        protected virtual bool ReadInTransaction(TcpClient client, MemoryStream readStream)
        {
            var package = _formatter.UnformatObj<TcpTransactionBuffer<IdType, EntityType>>(readStream);

            var outStream = client.GetStream();

            outStream.Write(TcpHeader.ACK.Array, 0, TcpHeader.ACK.Array.Length);
            outStream.Flush();

            InvokeReplicateTransaction(package.Transaction);

            return true;
        }

        protected virtual void SendError(TcpClient client, TcpErrorCode code, string format, object parms)
        {
            SendError(client, code, format, new object[] { parms });
        }

        protected virtual void SendError(TcpClient client, TcpErrorCode code, string format, params object[] parms)
        {
            try
            {
                if (!client.Connected)
                    return;

                var package = new ErrorPackage() { Message = string.Format(format, parms) };
                var bytes = _formatter.FormatObj(package);
                var outStream = client.GetStream();

                outStream.Write(TcpHeader.ERROR.Array, 0, TcpHeader.HEADER_LENGTH);
                outStream.Write(bytes, 0, bytes.Length);
                outStream.Flush();
            }
            catch (System.IO.IOException ioEx)
            {
                Trace.TraceError("sending error message failed: {0}", ioEx);
            }
            catch (SystemException sysEx)
            {
                Trace.TraceError("sending error message failed: {0}", sysEx);
            }
        }

        protected virtual void Listen()
        {
            _listener.Start();

            bool locked = false;

            while (_active)
            {
                try
                {
                    Thread.Sleep(10);

                    if (_listener == null || Database == null || !Monitor.TryEnter(_syncRoot))
                        continue;

                    locked = true;

                    if (_listener.Pending())
                    {
                        using (var socket = _listener.AcceptTcpClient())
                        {
                            if (socket != null)
                            {
                                var id = Guid.NewGuid();

                                var connection = new ConnectedTcpClient(socket, id);

                                while (connection.Client.Connected && connection.Client.Available <= 0)
                                    Thread.Sleep(10);

                                if (!ReadBufferFrom(connection))
                                    return;

                                CloseSocket(socket);
                            }
                        }
                    }
                }
                catch (JsonException jsEx)
                {
                    Trace.TraceError(jsEx.ToString());
                }
                catch (System.Net.Sockets.SocketException soEx)
                {
                    Trace.TraceError(soEx.ToString());
                }
                finally
                {
                    if (locked)
                        Monitor.Exit(_syncRoot);

                    locked = false;
                }
            }

            lock (_syncRoot)
                if (_listener != null)
                    _listener.Stop();
        }

        protected virtual void InvokeReplicateTransaction(ITransaction<IdType, EntityType> transaction)
        {
            Trace.TraceInformation("invoking replicate for trans: {0}", transaction.Id);

            if (OnReplicate != null)
                OnReplicate(transaction, DateTime.Now.ToUniversalTime().Ticks);
        }

        public AbstractTransactionalDatabase<IdType, EntityType> Database { get; set; }
        public string[] Settings { get; set; }

        public event ReplicateTransaction<IdType, EntityType> OnReplicate;

        public void Dispose()
        {
            lock (_syncRoot)
            {
                _active = false;

                if (_listener != null)
                    _listener.Stop();

                _listener = null;
            }
        }
    }
}
