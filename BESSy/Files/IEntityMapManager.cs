using System;
using System.Collections.Generic;

namespace BESSy.Files
{
    public interface IEntityMapManager<EntityType>  : IMapManager
    {
        EntityType LoadFromSegment(int segment);
        bool TryLoadFromSegment(int segment, out EntityType entity);
        bool SaveToFile(EntityType obj, int segment);
        int SaveBatchToFile(IList<EntityType> objs, int segmentStart);
    }
}
