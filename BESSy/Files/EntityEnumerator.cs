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
            , IBinConverter<IdType> idConverter)
        {
            _map = map;
            _idconverter = idConverter;

            Trace.TraceInformation("EntityEnumerator syncRoot entered.");
        }

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

            return false;
        }

        public void Reset()
        {
            _currentSegment = -1;
        }

        #endregion

        public void Dispose()
        {
        }
    }
}
