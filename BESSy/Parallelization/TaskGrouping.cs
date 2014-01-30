﻿/*
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
    public struct IndexingCPUGroup<I>
    {
        public int StartSegment;
        public int EndSegment;

        //TODO: support greater than 1MB filesize
        public int OldOffset;

        public IList<IndexingInsertSubset<I>> Inserts;
    }

    public struct IndexingInsertSubset<I>
    {
        public int StartNewSegment;
        public int EndNewSegment;
        public IList<I> IdsToAdd;

        //TODO: support greater than 1MB filesize
        public int NewOffset;
    }

    public static class TaskGrouping
    {
        internal static readonly int MemoryLimit = Environment.Is64BitOperatingSystem ? 2048000 : 1024000;
        internal static readonly int ReadLimit = Environment.Is64BitOperatingSystem ? 1638400 : 819200;
        internal static readonly int InsertLimit = Environment.Is64BitOperatingSystem ? 409600 : 204800;
        internal static readonly int ArrayLimit = Environment.Is64BitOperatingSystem ? 204800 : 102400;

        public static List<int> GetSegmentedTaskGroups(int length, int stride)
        {
            int rem;

            var procs = (Environment.ProcessorCount).Clamp(1, int.MaxValue);

            if (length < procs * 10 && stride < MemoryLimit)
                return new List<int>() { length - 1 };

            if (stride >= ReadLimit / 2 )
                procs = Math.Max(length, 1);
            else if (((length * (long)stride) / procs) > ReadLimit)
                procs = (int)((length * (long)stride) / ReadLimit);

            var len = Math.Max(Math.DivRem(length, procs, out rem), 1);

            var paras = new List<int>();

            for (int i = 1; i <= procs; i++)
                paras.Add((i * len) - 1);

            if (rem > 0)
                paras.Add((procs * len) + rem - 1);

            return paras;
        }

        public static List<IndexingCPUGroup<I>> GetCPUGroupsFor<I, T>
            (IDictionary<I, T> items
            , IDictionary<int, I> groups
            , IBinConverter<I> _idConverter
            , int stride, int newStride)
        {
            var maxAdd = (InsertLimit / newStride).Clamp(1, InsertLimit);

            var newGroups = new List<IndexingCPUGroup<I>>();

            var keys = groups.OrderBy(g => g.Value).Select(r => r.Key).ToArray();

            var idsToAppend = new Dictionary<int, IList<I>>();

            List<I> added = new List<I>();

            for (var k = 0; k < keys.Length; k++)
            {
                if (k == 0)
                {
                    var toAdd = items.Keys.Where
                        (i => _idConverter.Compare(i, groups[keys[k]]) < 0)
                        .ToList();

                    var group = GetGroup<I>(maxAdd, 0, keys[k], ref added, ref toAdd);

                    newGroups.Add(group);
                }
                else if (k < keys.Length - 1)
                {
                    var toAdd = items.Keys.Where
                        (i => _idConverter.Compare(i, groups[keys[k]]) < 0
                            && _idConverter.Compare(i, groups[keys[k - 1]]) > 0
                            && !added.Contains(i)).ToList();

                    var group = GetGroup<I>(maxAdd, keys[k - 1] + 1, keys[k], ref added, ref toAdd);

                    newGroups.Add(group);
                }
                else if (k < keys.Length)
                {
                    var toAdd = items.Keys.Where
                        (i => _idConverter.Compare(i, groups[keys[k - 1]]) > 0
                            && !added.Contains(i)).ToList();

                    var group = GetGroup<I>(maxAdd, keys[k - 1] + 1, keys[k], ref added, ref toAdd);

                    newGroups.Add(group);
                }
            }

            return newGroups;
        }

        public static IndexingCPUGroup<I> GetGroup<I>(int maxAdd, int startSegment, int endSegment, ref List<I> added, ref List<I> toAdd)
        {
            var group = new IndexingCPUGroup<I>()
            {
                StartSegment = startSegment,
                EndSegment = endSegment,
                Inserts = new List<IndexingInsertSubset<I>>()
            };

            if (toAdd.Count <= 0)
                group.Inserts.Add
                    (new IndexingInsertSubset<I>()
                        {
                            StartNewSegment = startSegment + added.Count,
                            EndNewSegment = endSegment + added.Count,
                            IdsToAdd = new List<I>()
                        });
            else
                for (var i = 0; i < toAdd.Count; i += maxAdd)
                {
                    var sub = toAdd.Skip(i).Take(maxAdd).ToList();

                    var insert = new IndexingInsertSubset<I>();

                    if (i == 0)
                    {
                        insert.StartNewSegment = startSegment + added.Count;
                        insert.EndNewSegment = endSegment + added.Count + sub.Count;
                    }
                    else
                    {
                        insert.StartNewSegment = group.Inserts.Last().EndNewSegment +1;
                        insert.EndNewSegment = (insert.StartNewSegment + sub.Count) -1;
                    }

                    insert.IdsToAdd = sub;

                    group.Inserts.Add(insert);

                    added.AddRange(sub);
                }

            return group;
        }

        public static List<IndexingCPUGroup<I>> GetCPUGroupsFor<I>(IDictionary<int, I> groups)
        {
            var newGroups = new List<IndexingCPUGroup<I>>();

            var keys = groups.OrderBy(g => g.Value).Select(r => r.Key).ToArray();

            for (var k = 0; k < keys.Length; k++)
            {
                if (k == 0)
                {
                    if (keys[k] > 0)
                        newGroups.Add(new IndexingCPUGroup<I>()
                        {
                            StartSegment = 0,
                            //StartNewSegment = 0,
                            EndSegment = keys[k]
                            //EndNewSegment = keys[k],
                            //IdsToAdd = new List<I>()
                        });
                }
                else if (k < keys.Length - 1)
                {
                    newGroups.Add(new IndexingCPUGroup<I>()
                    {
                        StartSegment = keys[k - 1],
                        //StartNewSegment = keys[k - 1],
                        EndSegment = keys[k]
                        //EndNewSegment = keys[k],
                        //IdsToAdd = new List<I>()
                    });
                }
                else if (k < keys.Length)
                {
                    newGroups.Add(new IndexingCPUGroup<I>()
                    {
                        StartSegment = keys[k - 1],
                        //StartNewSegment = keys[k - 1],
                        EndSegment = keys[k]
                        //EndNewSegment = keys[k],
                        //IdsToAdd = new List<I>()
                    });
                }
            }

            return newGroups;
        }
    }
}
