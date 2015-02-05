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
using BESSy.Serialization.Converters;
using BESSy.Extensions;

namespace BESSy.Parallelization
{
    public struct IndexingCPUGroup
    {
        public long StartSegment;
        public long EndSegment;

        //TODO: support greater than 1MB filesize
        public long OldOffset;

        //public IList<IndexingInsertSubset<I>> Inserts;
    }

    //public struct IndexingInsertSubset<I>
    //{
    //    public long StartNewSegment;
    //    public long EndNewSegment;
    //    public IList<I> IdsToAdd;

    //    //TODO: support greater than 1MB filesize
    //    public long NewOffset;
    //}

    public static class TaskGrouping
    {
        internal static readonly int TransactionLimit = Environment.Is64BitOperatingSystem ? 20480000 : 10240000;
        internal static readonly int MemoryLimit = Environment.Is64BitOperatingSystem ? 2048000 : 1024000;
        internal static readonly int ReadLimit = Environment.Is64BitOperatingSystem ? 1638400 : 819200;
        internal static readonly int InsertLimit = Environment.Is64BitOperatingSystem ? 409600 : 204800;
        internal static readonly int ArrayLimit = Environment.Is64BitOperatingSystem ? 204800 : 102400;

        public static List<int> GetSegmentedTaskGroups(int length, int stride)
        {
            return GetSegmentedTaskGroups((long)length, (long)stride).Select(s => (int)s).ToList();
        }

        public static List<long> GetSegmentedTaskGroups(long length, long stride)
        {
            long rem;

            var procs = (long)(Environment.ProcessorCount).Clamp(1, int.MaxValue);

            if (length < procs * 10 && stride < MemoryLimit)
                return new List<long>() { length - 1 };

            if (stride >= ReadLimit / 2)
                procs = Math.Max(length, 1);
            else if (((length * (long)stride) / procs) > ReadLimit)
                procs = (int)((length * (long)stride) / ReadLimit);

            var len = Math.Max(Math.DivRem(length, procs, out rem), 1);

            var paras = new List<long>();

            for (var i = 1L; i <= procs; i++)
                paras.Add((i * len) - 1);

            if (rem > 0)
                paras.Add((procs * len) + rem - 1);

            return paras;
        }

        public static List<IndexingCPUGroup> GetCPUGroupsFor(IList<int> groups)
        {
            return GetCPUGroupsFor(groups.Select(s => (long)s).ToList());
        }

        public static List<IndexingCPUGroup> GetCPUGroupsFor(IList<long> groups)
        {
            var newGroups = new List<IndexingCPUGroup>();

            var keys = groups.OrderBy(g => g).Select(r => r).ToArray();

            for (var k = 0; k < keys.Length; k++)
            {
                if (k == 0)
                {
                    if (keys[k] > 0)
                        newGroups.Add(new IndexingCPUGroup()
                        {
                            StartSegment = 0,
                            EndSegment = keys[k]
                        });
                }
                else if (k < keys.Length - 1)
                {
                    newGroups.Add(new IndexingCPUGroup()
                    {
                        StartSegment = keys[k - 1],
                        EndSegment = keys[k]
                    });
                }
                else if (k < keys.Length)
                {
                    newGroups.Add(new IndexingCPUGroup()
                    {
                        StartSegment = keys[k - 1],
                        EndSegment = keys[k]
                    });
                }
            }

            return newGroups;
        }
    }
}
