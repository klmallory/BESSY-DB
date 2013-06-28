using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Text;
using BESSy.Extensions;
using BESSy.Files;
using BESSy.Parallelization;
using BESSy.Seeding;
using BESSy.Serialization;
using BESSy.Serialization.Converters;
using BESSy.Synchronization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BESSy.Indexes
{
    public interface IPrimaryIndexFileManager<IdType, EntityType, SegmentType> : IIndexFileManager<IdType, EntityType, SegmentType>, IQueryableFile 
    {
        ISeed<IdType> Seed { get; }
    }

    public sealed class PrimaryIndexFileManager<IdType, EntityType> : IndexFileManager<IdType, EntityType>, IPrimaryIndexFileManager<IdType, EntityType, int>
    {
        public PrimaryIndexFileManager(string fileNamePath)
            : this(fileNamePath, new BSONFormatter())
        { }

        public PrimaryIndexFileManager(string fileNamePath, IQueryableFormatter formatter)
            : this(fileNamePath, Environment.SystemPageSize.Clamp(2048, 8192), formatter)
        { }

        public PrimaryIndexFileManager(string fileNamePath, int bufferSize, IQueryableFormatter formatter)
            : this(fileNamePath, bufferSize, formatter, new RowSynchronizer<int>(new BinConverter32()))
        { }

        public PrimaryIndexFileManager(string fileNamePath, int bufferSize, IQueryableFormatter formatter, IRowSynchronizer<int> rowSynchronizer)
            : this(fileNamePath, bufferSize, 0, 0, formatter, rowSynchronizer)
        { }

        public PrimaryIndexFileManager(string fileNamePath, int bufferSize, int startingSize, int maximumBlockSize, IQueryableFormatter formatter, IRowSynchronizer<int> rowSynchronizer)
            : base(fileNamePath, null, bufferSize, startingSize, maximumBlockSize, null, formatter, rowSynchronizer)
        {
            _seed = null;
        }

        public PrimaryIndexFileManager(string fileNamePath, ISeed<IdType> seed)
            : this(fileNamePath, new BSONFormatter(), seed)
        { }

        public PrimaryIndexFileManager(string fileNamePath, IQueryableFormatter formatter, ISeed<IdType> seed)
            : this(fileNamePath, Environment.SystemPageSize.Clamp(2048, 8192), formatter, seed)
        { }

        public PrimaryIndexFileManager(string fileNamePath, int bufferSize, IQueryableFormatter formatter, ISeed<IdType> seed)
            : this(fileNamePath, bufferSize, formatter, seed, new RowSynchronizer<int>(new BinConverter32()))
        { }

        public PrimaryIndexFileManager(string fileNamePath, int bufferSize, IQueryableFormatter formatter, ISeed<IdType> seed, IRowSynchronizer<int> rowSynchronizer)
            : this(fileNamePath, bufferSize, 0, 0, formatter, seed, rowSynchronizer)
        { }

        public PrimaryIndexFileManager(string fileNamePath, int bufferSize, int startingSize, int maximumBlockSize, IQueryableFormatter formatter, ISeed<IdType> seed, IRowSynchronizer<int> rowSynchronizer)
            : base(fileNamePath, seed.IdProperty, bufferSize, startingSize, maximumBlockSize, (IBinConverter<IdType>)seed.IdConverter, formatter, rowSynchronizer)
        {
            _seed = null;

            Seed = seed;
            _propertyConverter = (IBinConverter<IdType>)seed.IdConverter;
        }

        protected override void InitializeSeedFrom(FileStream fileStream)
        {
            var seed = LoadSeedFrom<IdType>(fileStream);

            if (seed != null)
            {
                Seed = seed;
                _propertyConverter = (IBinConverter<IdType>)seed.IdConverter;

                Stride = _formatter.FormatObj(new IndexPropertyPair<IdType, int>(_propertyConverter.Max, int.MaxValue)).Length;
            }

            //_seed = new Seed32(Seed.);
            base.InitializeSeedFrom(fileStream);
        }

        protected override void InitializeSeed()
        {
            if (Seed == null)
                throw new InvalidOperationException("seed must be specified in a new index.");

            _propertyConverter = (IBinConverter<IdType>)Seed.IdConverter;
            Stride = _formatter.FormatObj(new IndexPropertyPair<IdType, int>(_propertyConverter.Max, int.MaxValue)).Length;

            base.InitializeSeed();
        }

        protected override void ReinitializeSeed(int recordsWritten)
        {
            base.ReinitializeSeed(recordsWritten);
        }

        protected override long SaveSeed()
        {
            var seedStream = _formatter.FormatObjStream(Seed);

            try
            {
                if (GetPositionFor(seedStream.Length + SegmentDelimeter.Array.Length) > SeedPosition)
                {
                    Rebuild(Stride, Length, GetPositionFor(seedStream.Length + SegmentDelimeter.Array.Length));
                    seedStream = _formatter.FormatObjStream(Seed);
                }

                return SaveSeed(_fileStream, seedStream, SeedPosition);
            }
            finally { if (seedStream != null) seedStream.Dispose(); GC.Collect(); }
        }

        public ISeed<IdType> Seed { get; private set; }
        public override int SeedPosition { get { return Seed.MinimumSeedStride; } protected set { Seed.MinimumSeedStride = value; } }

        public override int Load()
        {

            var length = base.Load();

            IndexGet = (Func<EntityType, IdType>)Delegate.CreateDelegate(typeof(Func<EntityType, IdType>), typeof(EntityType).GetProperty(Seed.IdProperty).GetGetMethod());

            _seed = new Seed32(length);

            return length;
        }

    }
}
