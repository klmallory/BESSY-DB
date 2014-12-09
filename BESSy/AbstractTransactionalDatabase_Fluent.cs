using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BESSy.Replication;
using BESSy.Serialization.Converters;
using BESSy.Synchronization;
using System.Diagnostics;

namespace BESSy
{
    public partial class AbstractTransactionalDatabase<IdType, EntityType>
    {
        public ITransactionalDatabase<IdType, EntityType> WithPublishing(string name, IReplicationPublisher<IdType, EntityType> replication)
        {
            lock (_syncRoot)
            {
                if (_publishers.ContainsKey(name))
                    return this;

                replication.Database = this;

                _publishers.Add(name, replication);
            }

            return this;
        }

        public ITransactionalDatabase<IdType, EntityType> WithoutPublishing(string name)
        {
            lock (_syncRoot)
            {
                if (!_publishers.ContainsKey(name))
                    return this;

                var pub = _publishers[name];

                if (pub == null)
                    return this;

                pub.Dispose();

                _publishers.Remove(name);
            }

            return this;
        }

        public ITransactionalDatabase<IdType, EntityType> WithSubscription(string name, IReplicationSubscriber<IdType, EntityType> replication)
        {
            lock (_syncRoot)
            {
                if (_subscribers.ContainsKey(name))
                    return this;

                replication.Database = this;

                replication.OnReplicate += new ReplicateTransaction<IdType, EntityType>(OnReplicateReceived);

                _subscribers.Add(name, replication);
            }

            return this;
        }

        public ITransactionalDatabase<IdType, EntityType> WithoutSubscription(string name)
        {
            lock (_syncRoot)
            {
                if (!_subscribers.ContainsKey(name))
                    return this;

                var sub = _subscribers[name];

                if (sub == null)
                    return this;

                sub.OnReplicate -= new ReplicateTransaction<IdType, EntityType>(OnReplicateReceived);

                sub.Dispose();

                _subscribers.Remove(name);
            }

            return this;
        }

        public ITransactionalDatabase<IdType, EntityType> WithIndex<IndexType>(string name, string indexProperty, IBinConverter<IndexType> indexConverter)
        {
            try
            {
                lock (_syncIndex)
                {
                    if (_indexes.ContainsKey(name))
                        throw new DuplicateKeyException("Index with this property already exists.");

                    var index = _indexFactory.Create<IndexType, EntityType, long>
                        (GetIndexName(_fileName + "." + name)
                        , indexProperty
                        , false
                        , 10240
                        , indexConverter
                        , new BinConverter64()
                        , new RowSynchronizer<long>(new BinConverter64())
                        , new RowSynchronizer<int>(new BinConverter32()));

                    _indexes.Add(name, index);
                }
            }
            catch (Exception ex)
            {
                Trace.TraceError("Error initializing secondary location: {0}, property: {1}", name, indexProperty);
                Trace.TraceError(ex.ToString());

                throw;
            }
            return this;
        }

        public ITransactionalDatabase<IdType, EntityType> WithoutIndex(string name)
        {
            try
            {
                lock (_syncIndex)
                {
                    if (!_indexes.ContainsKey(name))
                        return this;

                    var index = _indexes[name] as IDisposable;

                    if (index != null)
                        index.Dispose();

                    _indexes.Remove(name);
                }
            }
            catch (Exception ex)
            {
                Trace.TraceError("Error unloading location: {0}", name);
                Trace.TraceError(ex.ToString());
            }
            return this;
        }
    }
}
