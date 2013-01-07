using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BESSy
{
    public class FileRepository<EntityType, IdType> : IRepository<EntityType, IdType>
    {
        #region IRepository<EntityType,IdType> Members

        public IdType Add(EntityType item)
        {
            throw new NotImplementedException();
        }

        public void Flush(IList<EntityType> dataSource)
        {
            throw new NotImplementedException();
        }

        public void AddOrUpdate(EntityType item, IdType id)
        {
            throw new NotImplementedException();
        }

        public void Update(EntityType item, IdType id)
        {
            throw new NotImplementedException();
        }

        public void Delete(IdType id)
        {
            throw new NotImplementedException();
        }

        #endregion

        #region IReadOnlyRepository<EntityType,IdType> Members

        public EntityType Fetch(IdType id)
        {
            throw new NotImplementedException();
        }

        public int Count()
        {
            throw new NotImplementedException();
        }

        public void Clear()
        {
            throw new NotImplementedException();
        }

        #endregion

        #region IDisposable Members

        public void Dispose()
        {
            throw new NotImplementedException();
        }

        #endregion
    }
}
