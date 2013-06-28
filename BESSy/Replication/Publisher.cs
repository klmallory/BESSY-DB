using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BESSy.Transactions;
using System.IO;
using BESSy.Serialization;
using BESSy.Crypto;
using BESSy.Extensions;
using System.Security.AccessControl;
using System.Threading;
using System.Diagnostics;

namespace BESSy.Replication
{
    public struct TransactionBuffer
    {
        public Guid Id;
        public Stream Stream;
    }

    public interface IReplicationPublisher<IdType, EntityType> : IDisposable
    {
        void Publish(ITransaction<IdType, EntityType> transaction);
    }

    public class Publisher<IdType, EntityType> : IReplicationPublisher<IdType, EntityType>
    {
        public Publisher(AbstractTransactionalDatabase<IdType, EntityType> database, string replicationFolder)
        {
            _database = database;
            _replicationFolder = replicationFolder;

            _formatter = new BSONFormatter();

            _queueTimer = new Timer(new TimerCallback(PublishQueue), null, -1, -1);

            if (!Directory.Exists(_replicationFolder))
                CreateDirectory();
        }

        public Publisher(AbstractTransactionalDatabase<IdType, EntityType> database
            , string replicationFolder
            , ICrypto crypto, object[] hash) 
            : this(database, replicationFolder)
        {
            _formatter = new QueryCryptoFormatter(crypto, new BSONFormatter(), hash);
        }

        object _syncRoot = new object();
        string _replicationFolder;

        Queue<TransactionBuffer> _queue = new Queue<TransactionBuffer>();
        IQueryableFormatter _formatter;
        AbstractTransactionalDatabase<IdType, EntityType> _database;

        Timer _queueTimer;

        protected FileStream OpenFile(string fileName, int bufferSize)
        {
            return new FileStream
                 (fileName
                 , FileMode.OpenOrCreate
                 , FileAccess.ReadWrite
                 , FileShare.None
                 , bufferSize, true);
        }

        private void CreateDirectory()
        {
            var access = new DirectorySecurity();

            access.AddAccessRule(new FileSystemAccessRule("Everyone", FileSystemRights.Write, AccessControlType.Allow));
            access.AddAccessRule(new FileSystemAccessRule("Everyone", FileSystemRights.ReadAndExecute, AccessControlType.Allow));
            access.AddAccessRule(new FileSystemAccessRule("Everyone", FileSystemRights.CreateFiles, AccessControlType.Allow));

#if DEBUG 
            access.AddAccessRule(new FileSystemAccessRule("Everyone", FileSystemRights.Modify, AccessControlType.Allow));
            access.AddAccessRule(new FileSystemAccessRule("Everyone", FileSystemRights.FullControl, AccessControlType.Allow));
#endif
            Directory.CreateDirectory(_replicationFolder, access);
        }

        private void WriteTransaction(ITransaction<IdType, EntityType> transaction)
        {
            var fi = new FileInfo(Path.Combine(_replicationFolder, transaction.Id.ToString() + ".transaction"));

            if (fi.Exists)
                return;

            var buf = _formatter.FormatObjStream(transaction);

            using (var fs = OpenFile(fi.FullName, Environment.SystemPageSize))
            {
                buf.WriteAllTo(fs);

                fs.Flush();
                fs.Close();
            }
        }

        private void WriteTransaction(TransactionBuffer buffer)
        {
            var fi = new FileInfo(Path.Combine(_replicationFolder, buffer.Id.ToString() + ".transaction"));

            if (fi.Exists)
                return;

            using (var fs = OpenFile(fi.FullName, Environment.SystemPageSize))
            {
                buffer.Stream.WriteAllTo(fs);
                
                fs.Flush();
                fs.Close();
            }

        }

        protected void PublishQueue(object state)
        {
            Guid workingId = Guid.Empty;
            bool locked = false;
            try
            {
                if (Directory.GetFiles(_replicationFolder, "*.pause").Length > 0)
                    return;

                if (Monitor.TryEnter(_syncRoot, 1000))
                {
                    locked = true;

                    while (_queue.Count > 0 && Directory.GetFiles(_replicationFolder, "*.pause").Length == 0)
                        WriteTransaction(_queue.Dequeue());

                    if (_queue.Count == 0)
                        _queueTimer.Change(-1, -1);
                }
            }
            catch (SystemException sysEx)
            {
                var rEx = new ReplicationException(workingId, sysEx.Message, sysEx);
                Trace.TraceError(sysEx.ToString());

                throw rEx;
            }
            finally
            {
                if (locked)
                    Monitor.Exit(_syncRoot);
            }

        }

        public void Publish(ITransaction<IdType, EntityType> transaction)
        {
            if (transaction == null)
                return;

            try
            {
                if (Directory.GetFiles(_replicationFolder, "*.pause").Length > 0)
                {
                    lock (_syncRoot)
                    {
                        _queue.Enqueue(new TransactionBuffer()
                        {
                            Id = transaction.Id,
                            Stream = _formatter.FormatObjStream(transaction)
                        });

                        _queueTimer.Change(3000, 3000);
                    }

                    return;
                }

                WriteTransaction(transaction);
            }
            catch (SystemException sysEx)
            {
                var rEx = new ReplicationException(transaction.Id, sysEx.Message, sysEx);
                Trace.TraceError(sysEx.ToString());

                throw rEx;
            }
        }

        public void Dispose()
        {
            lock (_syncRoot)
            {
                _database = null;
                _replicationFolder = string.Empty;
            }
        }
    }
}
