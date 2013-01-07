using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BESSy
{
    public interface ICache<EntityType, IdType>
    {
        bool IsNew(IdType id);
        bool Contains(IdType id);
        EntityType GetFromCache(IdType id);
        void CacheItem(IdType id);
        void Detach(IdType id);
        void ClearCache();
        void Sweep();
    }
}
