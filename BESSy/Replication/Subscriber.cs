using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using BESSy.Crypto;
using BESSy.Serialization;
using BESSy.Transactions;
using System.Threading;

namespace BESSy.Replication
{
    public delegate void ReplicateTransaction<IdType, EntityType>(ITransaction<IdType, EntityType> transaction, long timestamp);

    public interface IReplicationSubscriber<IdType, EntityType> : IDisposable
    {
        event ReplicateTransaction<IdType, EntityType> OnReplicate;
    }

    public class Subscriber<IdType, EntityType> : IReplicationSubscriber<IdType, EntityType>
    {
        public Subscriber(AbstractTransactionalDatabase<IdType, EntityType> database, string replicationFolder, TimeSpan interval)
        {
            _interval = interval;
            _database = database;
            _replicationFolder = replicationFolder;
            _formatter = new BSONFormatter();

            _timer = new System.Threading.Timer(PickupTransactions, null, interval, interval);

        }

        public Subscriber(AbstractTransactionalDatabase<IdType, EntityType> database, string replicationFolder, TimeSpan interval, ICrypto crypto, object[] hash) 
            : this(database, replicationFolder, interval)
        {
            if (crypto == null)
                return;

            _formatter = new QueryCryptoFormatter(crypto, new BSONFormatter(), hash);
        }

        object _syncRoot = new object();

        IQueryableFormatter _formatter;
        FileStream _pauseLock = null;
        AbstractTransactionalDatabase<IdType, EntityType> _database;
        string _replicationFolder;
        TimeSpan _interval;
        System.Threading.Timer _timer;
        List<string> _errors = new List<string>();

        private void LogError(string p)
        {
            Trace.TraceError(p);
            lock (_syncRoot)
                _errors.Add(p);
        }

        private void PauseReplication()
        {
            try
            {
                lock (_syncRoot)
                {
                    if (_pauseLock != null)
                        return;

                    _pauseLock = File.Create(Path.Combine(_replicationFolder, Guid.NewGuid() + ".pause"));
                }
            }
            catch (SystemException sysEx) { Trace.TraceError(sysEx.ToString()); }

            return;
        }

        private void UnpauseReplication()
        {
            try
            {
                if (_pauseLock == null)
                    return;

                lock (_syncRoot)
                {
                    var name = _pauseLock.Name;

                    _pauseLock.Dispose();

                    File.Delete(name);
                }
            }
            catch (SystemException sysEx) { Trace.TraceError(sysEx.ToString()); }

            return;
        }

        protected void PickupTransactions(object state)
        {
            bool locked = false;

            try
            {
                if (!Directory.Exists(_replicationFolder))
                {
                    lock (_syncRoot)
                        _errors.Clear();

                    LogError(string.Format("Directory does not exist {0}", _replicationFolder));
                    return;
                }

                if (Monitor.TryEnter(_syncRoot, 3000))
                {
                    locked = true;

                    var files = Directory.GetFiles(_replicationFolder, "*.transaction", SearchOption.TopDirectoryOnly);

                    var transactions = new Dictionary<string, FileInfo>();

                    if (files.Length > 0)
                        foreach (var file in files)
                        {
                            Guid id;
                            if (File.GetCreationTime(file).Ticks > _database.LastReplicatedTimeStamp 
                                && Guid.TryParse(Path.GetFileNameWithoutExtension(file), out id) 
                                && !_database.RecentTransactions.Contains(id))

                                transactions.Add(file, new FileInfo(Path.Combine(_replicationFolder, file)));
                        }

                    foreach (var trans in transactions.OrderBy(f => f.Value.CreationTime))
                    {
                        using (var fs = trans.Value.OpenRead())
                        {
                            var transaction = _formatter.UnformatObj<ITransaction<IdType, EntityType>>(fs);

                            if (transaction.Source == _database.TransactionSource)
                                continue;

                            InvokeOnReplicate(transaction, trans.Value.CreationTime.Ticks);
                        }
                    }

                    UnpauseReplication();
                }
            }
            catch (Exception ex)
            {
                LogError(string.Format(ex.ToString()));

                if (_errors.Count > 3)
                    PauseReplication();
            }
            finally
            {
                if (locked)
                    try { Monitor.Exit(_syncRoot); }
                    catch (SynchronizationLockException syncLockEx) { LogError(syncLockEx.ToString()); }
            }
        }

        #region Event OnReplicate

        protected void InvokeOnReplicate(ITransaction<IdType, EntityType> transaction, long timestamp)
        {
            if (OnReplicate != null)
                OnReplicate(transaction, timestamp);
                
        }
        public event ReplicateTransaction<IdType, EntityType> OnReplicate;

        #endregion

        
        public void Dispose()
        {
            UnpauseReplication();

            lock (_syncRoot)
            {
                _timer.Change(-1, -1);
                _timer = null;
                _database = null;
                _replicationFolder = string.Empty;
            }
        }
    }
}
