using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace BESSy.Containers
{
    public class ResourceStreamContainer : ResourceContainer
    {
        public ResourceStreamContainer()
        {
            ResourceType = typeof(System.IO.Stream).FullName;
        }

        protected override object GetResourceFrom(byte[] bytes)
        {
            return new MemoryStream(bytes);
        }

        protected override byte[] GetBytesFrom(object resource)
        {
            byte[] bytes = new byte[0];

            var strm = resource as Stream;

            if (strm == null)
                return bytes;

            bytes = new byte[strm.Length];

            strm.Read(bytes, 0, bytes.Length);

            return bytes;
        }
    }
}
