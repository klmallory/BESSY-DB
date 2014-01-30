/*
Copyright © 2011, Kristen Mallory DBA klink.
All rights reserved.
*/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BESSy.Parallelization;
using BESSy.Serialization.Converters;
using NUnit.Framework;

namespace BESSy.Tests.TaskGroupTests
{
    [TestFixture]
    public class TaskGroupTests
    {
        [Test]
        public void TaskGroupingGetBreakdownForNewList()
        {
            var newItems = new Dictionary<int, object>();

            Enumerable.Range(0, 2048).ToList().ForEach(e => newItems.Add(e, new object()));

            var paras = TaskGrouping.GetSegmentedTaskGroups(newItems.Count, 1024);

            Assert.AreEqual(Environment.ProcessorCount, paras.Count());
        }

                [Test]
        public void TaskGroupingGetsBreakdownForLargeStride()
        {
            var newItems = new Dictionary<int, object>();

            Enumerable.Range(0, 2048).ToList().ForEach(e => newItems.Add(e, new object()));

            var paras = TaskGrouping.GetSegmentedTaskGroups(newItems.Count, 4096000);

            Assert.AreEqual(2048, paras.Count());
        }
        
        [Test]
        public void TaskGroupingGetBreakdownForLargeList()
        {
            var expectedCount = System.Environment.Is64BitOperatingSystem ? 63 : 125;

            var newItems = new Dictionary<int, object>();

            var paras = TaskGrouping.GetSegmentedTaskGroups(100000, 1024);

            Assert.AreEqual(expectedCount, paras.Count());
        }

        [Test]
        public void TaskGroupGetsInsertsForOneHundredThousandRecords()
        {
            var expectedCount = System.Environment.Is64BitOperatingSystem ? 44 : 88;

            var rnd = new Random();

            var newItems = new Dictionary<int, object>();

            Enumerable.Range(0, 31240).ToList().ForEach(e => newItems.Add(71000 + e, new object()));

            var paras = TaskGrouping.GetSegmentedTaskGroups(70000, 1024);

            Assert.AreEqual(expectedCount, paras.Count());

            var groups = new Dictionary<int, int>();

            paras.ForEach(delegate(int p)
                {
                    groups.Add(p, (p + 1000) + rnd.Next(1, 5));
                });


            var newGroups = TaskGrouping.GetCPUGroupsFor(newItems, groups, new BinConverter32(), 1024, 1024);

            Assert.IsFalse(newGroups.Any(n => n.StartSegment == n.EndSegment));
            Assert.AreEqual(expectedCount, newGroups.Count());
            Assert.AreEqual(101239, newGroups.Max(n => n.Inserts.Max(i => i.EndNewSegment)));
        }
    }
}
