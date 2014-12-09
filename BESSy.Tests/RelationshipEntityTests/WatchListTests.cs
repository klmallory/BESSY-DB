using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using BESSy.Relational;

namespace BESSy.Tests.RelationshipEntityTests
{
    [TestFixture]
    public class WatchListTests: FileTest
    {
        [Test]
        public void WatchListTriggersAdd()
        {
            var wl = new WatchList<int>("carp");
            bool triggered = false;

            wl.AddInternal(1);
            wl.AddInternal(2);

            wl.OnCollectionChanged += new CollectionChanged<int>(delegate(string name, IEnumerable<int> collection)
                {
                    triggered = true;
                });

            var l = wl as IList<int>;
            l.Add(3);

            Assert.IsTrue(triggered);
        }

        [Test]
        public void WatchListTriggersInsert()
        {
            var wl = new WatchList<int>("carp");
            bool triggered = false;

            wl.AddInternal(1);
            wl.AddInternal(2);

            wl.OnCollectionChanged += new CollectionChanged<int>(delegate(string name, IEnumerable<int> collection)
            {
                triggered = true;
            });

            var l = wl as IList<int>;
            l.Insert(0, 3);

            Assert.IsTrue(triggered);
            triggered = false;

            wl.InsertRange(0, new int[] { 4, 5, 6, 7, 8 });

            Assert.IsTrue(triggered);
        }

        [Test]
        public void WatchListTriggersRemoveAt()
        {
            var wl = new WatchList<int>("carp");
            bool triggered = false;

            wl.AddInternal(1);
            wl.AddInternal(2);

            wl.OnCollectionChanged += new CollectionChanged<int>(delegate(string name, IEnumerable<int> collection)
            {
                triggered = true;
            });

            wl.RemoveAt(0);

            Assert.IsTrue(triggered);
        }

        [Test]
        public void WatchListTriggersIndexer()
        {
            var wl = new WatchList<int>("carp");
            bool triggered = false;

            wl.AddInternal(1);
            wl.AddInternal(2);

            wl.OnCollectionChanged += new CollectionChanged<int>(delegate(string name, IEnumerable<int> collection)
            {
                triggered = true;
            });

            wl[1] = 3;
            
            Assert.IsTrue(triggered);
            Assert.IsTrue(wl[1] == 3);
        }

        [Test]
        public void WatchListTriggersAddRange()
        {
            var wl = new WatchList<int>("carp");
            bool triggered = false;

            wl.AddRangeInternal(new int[] { 1, 2, 3 });

            wl.OnCollectionChanged += new CollectionChanged<int>(delegate(string name, IEnumerable<int> collection)
            {
                triggered = true;
            });

            wl.AddRange(new int[] { 4, 5, 6, 7 });

            Assert.IsTrue(triggered);
        }

        [Test]
        public void WatchListTriggersClear()
        {
            var wl = new WatchList<int>("carp");
            bool triggered = false;

            wl.AddRangeInternal(new int[] { 1, 2, 3 });

            wl.OnCollectionChanged += new CollectionChanged<int>(delegate(string name, IEnumerable<int> collection)
            {
                triggered = true;
            });

            wl.Clear();

            Assert.IsTrue(triggered);
        }

        [Test]
        public void WatchListTriggersRemove()
        {
            var wl = new WatchList<int>("carp");
            bool triggered = false;

            wl.AddRangeInternal(new int[] { 1, 2, 3 });

            wl.OnCollectionChanged += new CollectionChanged<int>(delegate(string name, IEnumerable<int> collection)
            {
                triggered = true;
            });

            wl.Remove(2);

            Assert.IsTrue(triggered);
        }

        [Test]
        public void WatchListTriggersRemoveAll()
        {
            var wl = new WatchList<int>("carp");
            bool triggered = false;

            wl.AddRangeInternal(new int[] { 1, 2, 3 });

            wl.OnCollectionChanged += new CollectionChanged<int>(delegate(string name, IEnumerable<int> collection)
            {
                triggered = true;
            });

            wl.RemoveAll(new Predicate<int>(p => p == 1));

            Assert.IsTrue(triggered);
        }

        [Test]
        public void WatchListTriggersRemoveRange()
        {
            var wl = new WatchList<int>("carp");
            bool triggered = false;

            wl.AddRangeInternal(new int[] { 1, 2, 3 });

            wl.OnCollectionChanged += new CollectionChanged<int>(delegate(string name, IEnumerable<int> collection)
            {
                triggered = true;
            });

            wl.RemoveRange(0, 2);

            Assert.IsTrue(triggered);
        }

        [Test]
        public void WatchListTriggersTrimExcess()
        {
            var wl = new WatchList<int>("carp");
            bool triggered = false;

            wl.AddRangeInternal(new int[] { 1, 2, 3 });

            wl.OnCollectionChanged += new CollectionChanged<int>(delegate(string name, IEnumerable<int> collection)
            {
                triggered = true;
            });

            wl.TrimExcess();

            Assert.IsTrue(triggered);
        }
    }
}
