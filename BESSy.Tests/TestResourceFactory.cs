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
using Newtonsoft.Json;
using BESSy.Cache;
using BESSy.Factories;

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

            return new RC2Crypto(vector);
        }

        internal static ISafeFormatter CreateZipFormatter()
        {
            return new QuickZipFormatter(CreateBsonFormatter(), QuickZipFormatter.DefaultProperties);
        }

        internal static ISafeFormatter CreateCryptoFormatter()
        {
            return new CryptoFormatter(CreateCrypto(), CreateZipFormatter(), QuickZipFormatter.DefaultProperties.Values.ToArray());
        }

        internal static IBatchFileManager<EntityType> CreateBatchFileManager<EntityType>(ISafeFormatter formatter)
        {
            return new BatchFileManager<EntityType>(512, 4096, formatter);
        }

        internal static IIndexedEntityMapManager<EntityType, IdType> CreateIndexedMapManager<EntityType, IdType>(ISafeFormatter formatter, IBinConverter<IdType> idConverter)
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

        internal static MockClassD CreateRandomRelation(IRepository<RelationshipEntity<int>, int> repo)
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
                Es = new Dictionary<int, MockClassE>() { 
                    { random.Next(), (MockClassE)CreateRandomRelation(repo) },
                    { random.Next(), (MockClassE)CreateRandomRelation(repo)},
                 }
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

        internal static IList<MockClassD> GetMockClassDObjects(int count, IRepository<RelationshipEntity<int>, int> repo)
        {
            var mocks = new List<MockClassD>();

            Enumerable.Range(0, count)
                .ToList()
                .ForEach(i => mocks.Add(CreateRandomRelation(repo)));


            return mocks;
        }
    }
}
