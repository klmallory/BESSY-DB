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
using System.Security;

namespace BESSy.Replication
{
    public delegate void ReplicateTransaction<IdType, EntityType>(ITransaction<IdType, EntityType> transaction, long timestamp);

    public interface IReplicationSubscriber<IdType, EntityType> : IDisposable 
    {
        AbstractTransactionalDatabase<IdType, EntityType> Database { get; set; }
        event ReplicateTransaction<IdType, EntityType> OnReplicate;
        string[] Settings { get; set; }
    }

    public class FileSubscriber<IdType, EntityType> : IReplicationSubscriber<IdType, EntityType> 
    {
        public FileSubscriber(string replicationFolder, TimeSpan interval)
        {
            _interval = interval;
            _replicationFolder = replicationFolder;
            _formatter = new BSONFormatter();

            _timer = new System.Threading.Timer(PickupTransactions, null, interval, interval);

        }

        public FileSubscriber(string replicationFolder, TimeSpan interval, IQueryableFormatter formatter)
            : this(replicationFolder, interval)
        {
            _formatter = formatter;
        }

        object _syncRoot = new object();

        IQueryableFormatter _formatter;
        FileStream _pauseLock = null;
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

                    var files = Directory.GetFiles(_replicationFolder, "*.trans", SearchOption.TopDirectoryOnly);

                    var transactions = new Dictionary<string, FileInfo>();

                    if (files.Length > 0)
                        foreach (var file in files)
                        {
                            Guid id;
                            if (File.GetCreationTime(file).Ticks > Database.LastReplicatedTimeStamp 
                                && Guid.TryParse(Path.GetFileNameWithoutExtension(file), out id) 
                                && !Database.RecentTransactions.Contains(id))

                                transactions.Add(file, new FileInfo(Path.Combine(_replicationFolder, file)));
                        }

                    foreach (var trans in transactions.OrderBy(f => f.Value.CreationTime))
                    {
                        using (var fs = trans.Value.OpenRead())
                        {
                            var transaction = _formatter.UnformatObj<ITransaction<IdType, EntityType>>(fs);

                            if (transaction.Source == Database.TransactionSource)
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

        public AbstractTransactionalDatabase<IdType, EntityType> Database { get; set; }
        public string[] Settings { get; set; }

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
                Database = null;
                _replicationFolder = string.Empty;
            }
        }
    }
}
