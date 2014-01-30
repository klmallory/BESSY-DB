using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Resources;
using System.Collections;
using System.IO;

namespace BESSy.Files
{
    public class ResourceEnumerator<T> : IEnumerator<KeyValuePair<string, T>>
    {
        public ResourceEnumerator(IResourceWrapper<T> wrapper)
        {
            _wrapper = wrapper;
            _setEnumerator = wrapper.ResourceSet.GetEnumerator();
        }

        IResourceWrapper<T> _wrapper;
        IDictionaryEnumerator _setEnumerator;

        public KeyValuePair<string, T> Current
        {
            get
            {
                if (_setEnumerator.Value is byte[])
                    return new KeyValuePair<string, T>((string)_setEnumerator.Key, _wrapper.GetFrom((byte[])_setEnumerator.Value));
                else if (_setEnumerator.Value is Stream)
                    return new KeyValuePair<string, T>((string)_setEnumerator.Key, _wrapper.GetFrom((Stream)_setEnumerator.Value));
                else
                    return new KeyValuePair<string, T>((string)_setEnumerator.Key, (T)_setEnumerator.Value);
            }
        }

        public string CurrentName
        {
            get
            {
                return _setEnumerator.Key as string;
            }
        }

        object System.Collections.IEnumerator.Current
        {
            get { return Current; }
        }

        public bool MoveNext()
        {
            return _setEnumerator.MoveNext();
        }

        public void Reset()
        {
            _setEnumerator.Reset();
        }

        public void Dispose()
        {
            _wrapper = null;
            _setEnumerator = null;
        }
    }
}
