using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using BESSy.Files;
using BESSy.Extensions;

namespace BESSy.Tests.Mocks
{
    internal class MockStreamConverter : IContentConverter<Stream>
    {
        public Stream GetResourceFrom(byte[] val)
        {
            return new MemoryStream(val);
        }

        public byte[] GetBytesFrom(Stream resource)
        {
            return resource.ReadAll();
        }
    }
}
