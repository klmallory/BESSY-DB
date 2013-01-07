using System;
using System.Collections.Generic;
using BESSy.Synchronization;

namespace BESSy.Files
{
    public interface IEntityMapManager<EntityType>  : IMapManager, ISynchronize<int>
    {
        EntityType LoadFromSegment(int segment);
        bool TryLoadFromSegment(int segment, out EntityType entity);
        bool SaveToFile(EntityType obj, int segment);
        int SaveBatchToFile(IList<EntityType> objs, int segmentStart);
    }
}
