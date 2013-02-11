using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BESSy.Files;
using BESSy.Seeding;
using System.IO;

namespace BESSy.Tests.Mocks
{
    public class MockBatchFileManager<EntityType> : IBatchFileManager<EntityType>
    {
        public MockBatchFileManager(int capacity)
        {
            _capacity = capacity;
        }

        object _seed;
        int _stride;
        int _capacity;
        IList<IList<EntityType>> _data = new List<IList<EntityType>>();
        int _batchIndex = 0;

        public int BatchSize
        {
            get { return 4096; }
        }

        public int GetBatchedSegmentCount(Stream stream)
        {
            return _capacity;
        }

        public ISeed<IdType> LoadSeedFrom<IdType>(System.IO.Stream stream)
        {
            return (ISeed<IdType>)_seed;
        }

        public IList<EntityType> LoadBatchFrom(System.IO.Stream stream)
        {
            if (_batchIndex >= _data.Count - 1)
            {
                _batchIndex = 0;
                return new List<EntityType>();
            }
            else
                _batchIndex += 1;

            return _data[_batchIndex];
        }

        public long SaveSeed<IdType>(System.IO.Stream stream, Seeding.ISeed<IdType> seed)
        {
            _seed = seed;
            _stride = seed.Stride;

            return seed.MinimumSeedStride;
        }

        public long SaveBatch(System.IO.Stream stream, IList<EntityType> objs, long atPosition)
        {
            _data.Add(objs);

            return atPosition + (objs.Count * _stride);
        }

        public string WorkingPath {get; set;}

        public System.IO.Stream GetWritableFileStream(string fileNamePath)
        {
            return new MemoryStream();
        }

        public System.IO.Stream GetReadableFileStream(string fileNamePath)
        {
            return new MemoryStream();
        }

        public void Dispose()
        {
            
        }

        public void Replace(string fromFileName, string toFileName)
        {
            
        }
    }
}
