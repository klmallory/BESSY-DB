using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BESSy.Json;

namespace BESSy.Seeding
{
    public interface IFileCore<SegmentType>
    {
        string CategoryIdProperty { get; set; }
        object IdConverter { get; set; }
        string IdProperty { get; set; }
        long LastReplicatedTimeStamp { get; set; }
        int MinimumCoreStride { get; set; }
        object PropertyConverter { get; set; }
        ISeed<SegmentType> SegmentSeed { get; set; }
        Guid Source { get; }
        int Stride { get; set; }
        List<string> Publishers { get; set; }
        List<string> Subscribers { get; set; }
        List<string> Indexes { get; set; }
    }

    public interface IFileCore<IdType, SegmentType> : IFileCore<SegmentType>
    {
        ISeed<IdType> IdSeed { get; set; }
    }

    public class FileCore<IdType, SegmentType> : IFileCore<IdType, SegmentType>
    {
        public FileCore()
            : this(TypeFactory.GetSeedFor<IdType>())
        {

        }

        public FileCore(ISeed<IdType> idSeed)
            : this(idSeed, TypeFactory.GetSeedFor<SegmentType>())
        {

        }

        public FileCore(ISeed<IdType> idSeed, ISeed<SegmentType> segmentSeed)
        {
            Source = Guid.NewGuid();
            Stride = 512;
            MinimumCoreStride = 10240;
            IdSeed = idSeed;
            SegmentSeed = segmentSeed;

            Indexes = new List<string>();
            Subscribers = new List<string>();
            Publishers = new List<string>();
        }

        [JsonProperty]
        public Guid Source { get; protected set; }

        public long LastReplicatedTimeStamp { get; set; }
        public object PropertyConverter { get; set; }
        public object IdConverter { get; set; }

        public string IdProperty { get; set; }
        public string CategoryIdProperty { get; set; }

        public int MinimumCoreStride { get; set; }
        public int Stride { get; set; }

        public ISeed<SegmentType> SegmentSeed { get; set; }
        public ISeed<IdType> IdSeed { get; set; }

        public List<string> Publishers { get; set; }
        public List<string> Subscribers { get; set; }
        public List<string> Indexes { get; set; }
    }
}
