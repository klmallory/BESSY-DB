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
    public class IndexEnumerator<IdType, PropertyType> : IEnumerator<IndexPropertyPair<IdType, PropertyType>>
    {
        public IndexEnumerator
            (IIndexMapManager<IdType, PropertyType> index
            ,IBinConverter<IdType> idConverter)
        {
            _index = index;
            _idconverter = idConverter;
        }

        IIndexMapManager<IdType, PropertyType> _index;
        IBinConverter<IdType> _idconverter;

        int _currentSegment = -1;

        public void SetIndex(int segment)
        {
            _currentSegment = segment;
        }

        #region IEnumerator<T> Members

        public IndexPropertyPair<IdType, PropertyType> Current
        {
            get
            {
                IndexPropertyPair<IdType, PropertyType> index;

                while (!_index.TryLoadFromSegment(_currentSegment, out index) && _currentSegment < _index.Length - 1)
                {
                    _currentSegment++;
                }

                return index;
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
            if (_currentSegment < _index.Length - 1)
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
