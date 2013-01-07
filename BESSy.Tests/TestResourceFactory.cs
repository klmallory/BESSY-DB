/*
Copyright © 2011, Kristen Mallory DBA klink.
All rights reserved.
*/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BESSy.Files;
using BESSy.Serialization;
using BESSy.Serialization.Converters;
using BESSy.Tests.Mocks;
using Newtonsoft.Json;
using BESSy.Seeding;
using System.IO;

namespace BESSy.Tests
{
    internal static class TestResourceFactory
    {
        static JsonSerializerSettings settings = BSONFormatter.GetDefaultSettings();
        static Random random = new Random((int)DateTime.Now.Ticks);

        internal static ISafeFormatter CreateBsonFormatter()
        {
            return new BSONFormatter(settings);
        }

        internal static ISafeFormatter CreateZipFormatter()
        {
            return new QuickZipFormatter(CreateBsonFormatter());
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

        internal static IIndexRepository<IdType, PropertyType> CreateIndexRepository<IdType, PropertyType>(string folder, string fileName, ISeed<IdType> seed, IBinConverter<IdType> idConverter, IBatchFileManager<IndexPropertyPair<IdType, PropertyType>> indexBatchManager, IIndexMapManager<IdType, PropertyType> mapManager)
        {
            return new IndexRepository<IdType, PropertyType>
                (true
                , -1
                , Path.Combine(Environment.CurrentDirectory, folder, fileName)
                , seed
                , idConverter
                , indexBatchManager
                , mapManager);
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
    }
}
