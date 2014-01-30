using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Resources;

namespace BESSy.ResourceWrapper
{
    public class StreamResourceWrapper : AbstractResourceWrapper<Stream>
    {
        public StreamResourceWrapper(ResourceManager manager) : base(manager)
        {
        }

        public override Stream GetFrom(byte[] contents)
        {
            return new MemoryStream(contents);
        }

        public override Stream GetFrom(Stream contents)
        {
            return contents;
        }

        public override Stream Fetch(string id)
        {
            return GetFileStream(id);
        }
    }
}
