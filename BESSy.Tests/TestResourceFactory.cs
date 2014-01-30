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
using System.IO;
using System.Linq;
using System.Text;
using BESSy.Crypto;
using BESSy.Files;
using BESSy.Seeding;
using BESSy.Serialization;
using BESSy.Serialization.Converters;
using BESSy.Tests.Mocks;
using BESSy.Json;
using BESSy.Cache;
using BESSy.Factories;
using System.Security;
using BESSy.Relational;

namespace BESSy.Tests
{
    internal static class TestResourceFactory
    {
        static Random random = new Random((int)DateTime.Now.Ticks);

        internal static IQueryableFormatter CreateBsonFormatter()
        {
            return new BSONFormatter(BSONFormatter.GetDefaultSettings());
        }

        internal static IQueryableFormatter CreateJsonFormatter()
        {
            return new JSONFormatter(JSONFormatter.GetDefaultSettings());
        }

        internal static IQueryableFormatter CreateJsonFormatterWithoutArrayFormatting()
        {
            var settings = JSONFormatter.GetDefaultSettings();
            settings.TypeNameHandling = TypeNameHandling.Objects;

            return new JSONFormatter(settings);
        }

        internal static ICrypto CreateCrypto()
        {
            byte[] vector = new byte[8];
            random.NextBytes(vector);

            var s = new SecureString();

            foreach (var b in vector) { s.AppendChar(Convert.ToChar(b)); }

            return new RC2Crypto(s);
        }

        internal static SecureString CreateSecureString()
        {
            byte[] vector = new byte[8];
            random.NextBytes(vector);

            var s = new SecureString();

            foreach (var b in vector) { s.AppendChar(Convert.ToChar(b)); }

            return s;
        }

        internal static IQueryableFormatter CreateZipFormatter()
        {
            return new QueryZipFormatter(CreateBsonFormatter(), QuickZipFormatter.DefaultProperties);
        }

        internal static IQueryableFormatter CreateCryptoFormatter()
        {
            return new  QueryCryptoFormatter(CreateCrypto(), CreateZipFormatter(), CreateSecureString());
        }

        internal static IBatchFileManager<EntityType> CreateBatchFileManager<EntityType>(IQueryableFormatter formatter)
        {
            return new BatchFileManager<EntityType>(512, 4096, formatter);
        }

        internal static IIndexedEntityMapManager<EntityType, IdType> CreateIndexedMapManager<EntityType, IdType>(IQueryableFormatter formatter, IBinConverter<IdType> idConverter)
        {
            return new IndexedEntityMapManager<EntityType, IdType>(idConverter, formatter);
        }

        internal static IIndexMapManager<IdType, PropertyType> CreateIndexMapManager<IdType, PropertyType>(string fileName, IBinConverter<IdType> idConverter, IBinConverter<PropertyType> propertyConverter)
        {
            return new IndexMapManager<IdType, PropertyType>(fileName, idConverter, propertyConverter);
        }

        internal static IRepositoryCacheFactory CreateRepositoryCacheFactory()
        {
            return new RepositoryCacheFactory(-1);
        }

        internal static IIndexRepositoryFactory<IdType, PropertyType> CreateIndexFactory<IdType, PropertyType>()
        {
            return new IndexRepositoryFactory<IdType, PropertyType>();
        }

        internal static IIndexRepositoryFactory<IdType, PropertyType> CreateIndexFactory<IdType, PropertyType>(IBinConverter<IdType> idConverter, IBinConverter<PropertyType> propertyConverter, ISeed<IdType> seed)
        {
            return new IndexRepositoryFactory<IdType, PropertyType>(idConverter, propertyConverter, seed);
        }

        internal static IIndexRepositoryFactory<IdType, PropertyType> CreateIndexFactory<IdType, PropertyType>(int cacheSize, ISeed<IdType> seed)
        {
            return CreateIndexFactory<IdType, PropertyType>(new RepositoryCacheFactory(cacheSize), seed);
        }

        internal static IIndexRepositoryFactory<IdType, PropertyType> CreateIndexFactory<IdType, PropertyType>(IRepositoryCacheFactory cacheFactory)
        {
            var indexFactory = new IndexRepositoryFactory<IdType, PropertyType>()
            {
                DefaultCacheFactory = cacheFactory
            };

            return indexFactory;
        }

        internal static IIndexRepositoryFactory<IdType, PropertyType> CreateIndexFactory<IdType, PropertyType>(IRepositoryCacheFactory cacheFactory, ISeed<IdType> seed)
        {
            var indexFactory = new IndexRepositoryFactory<IdType, PropertyType>()
            {
                DefaultCacheFactory = cacheFactory,
                DefaultSeed = seed
            };

            return indexFactory;
        }


        internal static MockClassA CreateRandom()
        {
            return new MockClassC()
            {
                Name = "Class " + random.Next(),
                Location = new MockStruct()
                    {
                        X = (float)random.NextDouble(),
                        Y = (float)random.NextDouble(),
                        Z = (float)random.NextDouble(),
                        W = (float)random.NextDouble()
                    },
                GetSomeCheckSum = new double[] { random.NextDouble(), random.NextDouble() },
                ReferenceCode = "R " + random.Next(),
                ReplicationID = Guid.NewGuid()
            };
        }

        internal static IList<MockClassA> GetMockClassAObjects(int count)
        {
            var mocks = new List<MockClassA>();

            Enumerable.Range(0, count)
                .ToList()
                .ForEach(i => mocks.Add(CreateRandom()));


            return mocks;
        }

        internal static MockClassE CreateRandomRelation(IRepository<RelationshipEntity<int>, int> repo)
        {
            return new MockClassE(repo)
            {
                Name = "Class " + random.Next(),
                Location = new MockStruct()
                {
                    X = (float)random.NextDouble(),
                    Y = (float)random.NextDouble(),
                    Z = (float)random.NextDouble(),
                    W = (float)random.NextDouble()
                },
                GetSomeCheckSum = new double[] { random.NextDouble(), random.NextDouble() },
                ReferenceCode = "R " + random.Next(),
                ReplicationID = Guid.NewGuid(),
                NamesOfStuff = new string[] { Guid.NewGuid().ToString(), Guid.NewGuid().ToString() },
                TPSCoverSheet = Guid.NewGuid().ToString(),
                HiC = null,
                LowBall = null,
                Parent = null
            };
        }

        internal static IList<MockClassE> GetMockClassDObjects(int count, IRepository<RelationshipEntity<int>, int> repo)
        {
            var mocks = new List<MockClassE>();

            Enumerable.Range(0, count)
                .ToList()
                .ForEach(i => mocks.Add(CreateRandomRelation(repo)));

            foreach (var m in mocks)
            {
                m.HiC = CreateRandomRelation(repo);
                m.LowBall = new List<MockClassD> { CreateRandomRelation(repo), CreateRandomRelation(repo) };
                m.Parent = CreateRandomRelation(repo) as MockClassE;
            }

            return mocks.ToList();
        }
    }
}
