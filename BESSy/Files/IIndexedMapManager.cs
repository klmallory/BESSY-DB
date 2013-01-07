using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BESSy.Files
{
    public interface IIndexedMapManager<EntityType, IdType> : IMapManager
    {
        EntityType Load(IdType id);
        IdType LookupFromSegment(int segment);
        bool Save(EntityType obj, IdType id);
        bool Save(EntityType obj, IdType id, int segment);
        int SaveBatchToFile(IDictionary<IdType, EntityType> items, int segmentStart);
        void Flush(IDictionary<IdType, EntityType> items);
        event FlushCompleted<EntityType, IdType> OnFlushCompleted;
    }
}
