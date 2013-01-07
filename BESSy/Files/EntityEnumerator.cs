/*
Copyright © 2011, Kristen Mallory DBA klink.
All rights reserved.
*/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections;
using BESSy.Serialization.Converters;
using System.Threading;
using System.Diagnostics;

namespace BESSy.Files
{
    public class EntityEnumerator<EntityType, IdType> : IEnumerator<EntityType>
    {
        public EntityEnumerator(
            IIndexedEntityMapManager<EntityType, IdType> map
            , object syncRoot
            , IBinConverter<IdType> idConverter)
        {
            _syncRoot = syncRoot;
            _map = map;
            _idconverter = idConverter;

            if (!Monitor.TryEnter(syncRoot, 50000))
                throw new ThreadStateException("EntityEnumerator could not get a lock on syncRoot parameter.");

            Trace.TraceInformation("EntityEnumerator syncRoot entered.");
        }

        object _syncRoot;
        IIndexedEntityMapManager<EntityType, IdType> _map;
        IBinConverter<IdType> _idconverter;

        int _currentSegment = -1;

        public void SetIndex(int segment)
        {
            _currentSegment = segment;
        }

        #region IEnumerator<T> Members

        public EntityType Current
        {
            get
            {
                EntityType entity;

                while (!_map.TryLoadFromSegment(_currentSegment, out entity) && _currentSegment < _map.Length - 1)
                {
                    _currentSegment++;
                }

                //if (object.Equals(entity, default(EntityType)))
                //    Trace.TraceError("Not supposed to happen.");

                return entity;
            }
        }

        #endregion

        #region IEnumerator Members

        object IEnumerator.Current
        {
            get
            {
                return Current;
            }
        }

        public bool MoveNext()
        {
            if (_currentSegment < _map.Length - 1)
            {
                _currentSegment++;

                return true;
            }

            _currentSegment = -1;

            Monitor.Exit(_syncRoot);

            Trace.TraceInformation("IndexEnumerator syncRoot exited.");

            return false;
        }

        public void Reset()
        {
            _currentSegment = -1;
        }

        #endregion

        #region IDisposable Members

        public void Dispose()
        {
            if (_syncRoot != null)
                try
                {
                    Monitor.Exit(_syncRoot);
                    Trace.TraceInformation("EntityEnumerator syncRoot exited.");
                }
                catch (SynchronizationLockException) 
                { Trace.TraceWarning("syncLock did not exit."); }
        }

        #endregion
    }
}
