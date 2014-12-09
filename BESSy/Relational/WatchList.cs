using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections;
using BESSy.Json;

namespace BESSy.Relational
{
    public delegate void CollectionChanged<T>(string name, IEnumerable<T> collection);

    public interface IWatchList<T> : IList<T>, ICollection<T>, IEnumerable<T>, IList, ICollection, IEnumerable
    {
        event CollectionChanged<T> OnCollectionChanged;

        string PropertyName { get; set; }
    }

    public class WatchList<T> : List<T>, IWatchList<T>
    {
        public WatchList() : base()
        {
        }

        public WatchList(string name)
            : base()
        {
            PropertyName = name;
        }

        private void InvokeOnCollectionChanged()
        {
            if (OnCollectionChanged != null)
                OnCollectionChanged(PropertyName, this);
        }

        public event CollectionChanged<T> OnCollectionChanged;

        [JsonIgnore]
        public string PropertyName { get; set; }

        public new void Insert(int index, T item)
        {
            base.Insert(index, item);

            InvokeOnCollectionChanged();
        }

        public new void RemoveAt(int index)
        {
            base.RemoveAt(index);

            InvokeOnCollectionChanged();
        }

        [JsonIgnore]
        public new T this[int index]
        {
            get
            {
                return base[index];
            }
            set
            {
                base[index] = value;

                InvokeOnCollectionChanged();
            }
        }

        internal void AddInternal(T item)
        {
            base.Add(item);
        }

        public new void Add(T item)
        {
            base.Add(item);

            InvokeOnCollectionChanged();
        }

        public new void AddRange(IEnumerable<T> collection)
        {
            base.AddRange(collection);

            InvokeOnCollectionChanged();
        }

        internal void AddRangeInternal(IEnumerable<T> collection)
        {
            base.AddRange(collection);
        }

        public new void Clear()
        {
            base.Clear();

            InvokeOnCollectionChanged();
        }

        public new void CopyTo(T[] array, int arrayIndex)
        {
            base.CopyTo(array, arrayIndex);
        }

        public new bool Remove(T item)
        {
            var val = base.Remove(item);

            InvokeOnCollectionChanged();

            return val;
        }

        public new void InsertRange(int index, IEnumerable<T> collection)
        {
            base.InsertRange(index, collection);

            InvokeOnCollectionChanged();
        }

        public new int RemoveAll(Predicate<T> match)
        {
            var val = base.RemoveAll(match);

            InvokeOnCollectionChanged();

            return val;
        }

        public new void RemoveRange(int index, int count)
        {
            base.RemoveRange(index, count);

            InvokeOnCollectionChanged();
        }

        public new void TrimExcess()
        {
            base.TrimExcess();

            InvokeOnCollectionChanged();
        }
    }
}
