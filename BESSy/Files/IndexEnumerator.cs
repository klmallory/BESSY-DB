/*
Copyright (c) 2011,2012,2013 Kristen Mallory dba Klink

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"),
to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, 
and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, 
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, 
DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, 
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

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

        #region IEnumerator<ResourceType> Members

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
