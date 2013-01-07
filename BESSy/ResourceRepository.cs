using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Resources;

namespace BESSy
{
    public class ResourceRepository<EntityType> : AbstractResourceRepository<EntityType>
    {
        public ResourceRepository(Func<byte[], EntityType> readDelegate, ResourceManager resources) : base(resources)
        {
            _readDelegate = readDelegate;
        }

        Func<byte[], EntityType> _readDelegate;

        public override EntityType Fetch(string id)
        {
            var contents = GetFileContents(id);

            return _readDelegate.Invoke(contents);
        }
    }
}
