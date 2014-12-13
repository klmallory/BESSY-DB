using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using BESSy.Containers;
using BESSy.Files;

namespace BESSy.Tests.Mocks
{
    public class MockStreamContainer : ResourceStreamContainer
    {
        public MockStreamContainer()
        {
            ResourceType = typeof(Stream).FullName;
        }

        public MockStreamContainer(Stream resource)
            : this()
        {
            SetResource<Stream>(resource);
        }

        private static IContentConverter<Stream> _converter = new MockStreamConverter();
    }
}
