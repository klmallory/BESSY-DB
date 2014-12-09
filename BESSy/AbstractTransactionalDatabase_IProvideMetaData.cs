using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BESSy.Factories;
using BESSy.Replication;

namespace BESSy
{
    public partial class AbstractTransactionalDatabase<IdType, EntityType>
    {
        #region IConfigureDatabase members

        void IConfigureDatabase.AddIndex(string indexCommand)
        {
            var obj = IndexFromStringFactory.Create(indexCommand);
            var name = IndexFromStringFactory.GetNameFrom(indexCommand);

            _indexes.Add(name, obj);
        }

        void IConfigureDatabase.RemoveIndex(string name)
        {
            WithoutIndex(name);

            if (_core.Indexes.Contains(name))
            {
                _core.Indexes.Remove(name);
                _fileManager.SaveCore<IdType>();
            }
        }

        void IConfigureDatabase.WithPublisher(string command)
        {
            var obj = ReplicationFromStringFactory.Create(command) as IReplicationPublisher<IdType, EntityType>;
            var name = ReplicationFromStringFactory.GetNameFrom(command);

            lock (_syncRoot)
            {
                if (_publishers.ContainsKey(name))
                    return;

                obj.Database = this;

                _publishers.Add(name, obj);

                if (!_core.Publishers.Any(p => ReplicationFromStringFactory.GetNameFrom(p) == name))
                    _core.Publishers.Add(command);
            }
        }

        void IConfigureDatabase.WithoutPublisher(string name)
        {
            WithoutPublishing(name);

            if (_core.Publishers.Any(p => ReplicationFromStringFactory.GetNameFrom(p) == name))
                _core.Publishers.Remove(name);
        }

        void IConfigureDatabase.WithSubscriber(string command)
        {
            var obj = ReplicationFromStringFactory.Create(command) as IReplicationSubscriber<IdType, EntityType>;
            var name = ReplicationFromStringFactory.GetNameFrom(command);

            lock (_syncRoot)
            {
                if (_publishers.ContainsKey(name))
                    return;

                obj.Database = this;

                _subscribers.Add(name, obj);

                lock (_syncRoot)
                    if (!_core.Subscribers.Any(p => ReplicationFromStringFactory.GetNameFrom(p) == name))
                        _core.Subscribers.Add(command);
            }
        }

        void IConfigureDatabase.WithoutSubscriber(string name)
        {
            WithoutSubscription(name);

            if (_core.Subscribers.Any(p => ReplicationFromStringFactory.GetNameFrom(p) == name))
                _core.Subscribers.Remove(name);
        }

        #endregion
    }
}
