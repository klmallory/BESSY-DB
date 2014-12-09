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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using BESSy.Extensions;
using BESSy.Files;
using BESSy.Seeding;
using BESSy.Serialization;
using BESSy.Serialization.Converters;
using BESSy.Tests.Mocks;
using BESSy.Transactions;
using NUnit.Framework;

namespace BESSy.Tests.AtomicFileManagerTests
{
    public class AtomicFileManagerHelper
    {
        public static long SaveSegment<IdType, EntityType>(AtomicFileManager<EntityType> afm, EntityType entity, IdType id)
        {
            var returnSegment = -1L;

            using (var manager = new TransactionManager<IdType, EntityType>
                 (new MockTransactionFactory<IdType, EntityType>()
                 , new TransactionSynchronizer<IdType, EntityType>()))
            {
                manager.TransactionCommitted += new TransactionCommit<IdType, EntityType>(
                    delegate(ITransaction<IdType, EntityType> tranny)
                    {
                        returnSegment = (long)(afm.CommitTransaction(tranny, new Dictionary<IdType, long>()).First().Value);

                        tranny.MarkComplete();
                    });

                afm.Rebuilt += new Rebuild<EntityType>(delegate(Guid transactionId, int newStride, long newLength, int newSeedStride)
                    {
                        var core = (IFileCore<IdType, long>)afm.Core;
                        core.Stride = newStride;
                        core.MinimumCoreStride = newSeedStride;

                        afm.SaveCore<IdType>();
                    });

                using (var tLock = manager.BeginTransaction())
                {
                    tLock.Transaction.Enlist(Action.Create, id, entity);

                    tLock.Transaction.Commit();
                }
            }

            return returnSegment;
        }

        public static void SaveSegment<IdType, EntityType>(AtomicFileManager<EntityType> afm, EntityType entity, IdType id, long segment)
        {
            var records = 0;

            using (var manager = new TransactionManager<IdType, EntityType>
                 (new MockTransactionFactory<IdType, EntityType>()
                 , new TransactionSynchronizer<IdType, EntityType>()))
            {
                manager.TransactionCommitted += new TransactionCommit<IdType, EntityType>(
                    delegate(ITransaction<IdType, EntityType> tranny)
                    {
                        records = afm.CommitTransaction(tranny, new Dictionary<IdType, long>() { { id, segment } }).Count();

                        tranny.MarkComplete();
                    });

                afm.Rebuilt += new Rebuild<EntityType>(delegate(Guid transactionId, int newStride, long newLength, int newSeedStride)
                {
                    var core = (IFileCore<IdType, long>)afm.Core;
                    core.Stride = newStride;
                    core.MinimumCoreStride = newSeedStride;

                    afm.SaveCore<IdType>();
                });

                using (var tLock = manager.BeginTransaction())
                {
                    tLock.Transaction.Enlist(Action.Update, id, entity);

                    tLock.Transaction.Commit();
                }
            }
        }

        public static IDictionary<IdType, long> SaveSegments<IdType, EntityType>(AtomicFileManager<EntityType> afm, IDictionary<IdType, EntityType> entities)
        {
            IDictionary<IdType, long> returnSegments = null;

            using (var manager = new TransactionManager<IdType, EntityType>
                 (new MockTransactionFactory<IdType, EntityType>()
                 , new TransactionSynchronizer<IdType, EntityType>()))
            {
                manager.TransactionCommitted += new TransactionCommit<IdType, EntityType>(
                    delegate(ITransaction<IdType, EntityType> tranny)
                    {
                        returnSegments = afm.CommitTransaction(tranny, new Dictionary<IdType, long>());

                        tranny.MarkComplete();
                    });

                afm.Rebuilt += new Rebuild<EntityType>(delegate(Guid transactionId, int newStride, long newLength, int newSeedStride)
                {
                    var core = (IFileCore<IdType, long>)afm.Core;
                    core.Stride = newStride;
                    core.MinimumCoreStride = newSeedStride;

                    afm.SaveCore<IdType>();
                });

                using (var tLock = manager.BeginTransaction())
                {
                    foreach (var e in entities)
                        tLock.Transaction.Enlist(Action.Create, e.Key, e.Value);

                    tLock.Transaction.Commit();
                }
            }

            return returnSegments;
        }

        public static int DeleteSegment<IdType, EntityType>(AtomicFileManager<EntityType> afm, IdType id, long segment)
        {
            var records = 0;

            using (var manager = new TransactionManager<IdType, EntityType>
                 (new MockTransactionFactory<IdType, EntityType>()
                 , new TransactionSynchronizer<IdType, EntityType>()))
            {
                manager.TransactionCommitted += new TransactionCommit<IdType, EntityType>(
                    delegate(ITransaction<IdType, EntityType> tranny)
                    {
                        records = afm.CommitTransaction(tranny, new Dictionary<IdType, long>() { { id, segment } }).Count();

                        tranny.MarkComplete();
                    });

                using (var tLock = manager.BeginTransaction())
                {
                    tLock.Transaction.Enlist(Action.Delete, id, default(EntityType));

                    tLock.Transaction.Commit();
                }
            }

            return records;
        }
    }
}
