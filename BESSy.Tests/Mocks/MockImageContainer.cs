using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;
using BESSy.Files;
using BESSy.Json;
using BESSy.Containers;

namespace BESSy.Tests.Mocks
{

    public class MockImageContainer : ResourceContainer
    {
        public MockImageContainer()
        {
            ResourceType = typeof(Bitmap).FullName;
        }

        public MockImageContainer(Bitmap resource) : this()
        {
            SetResource<Bitmap>(resource);
        }

        private static IContentConverter<Bitmap> _converter = new MockImageConverter();

        protected override object GetResourceFrom(byte[] bytes)
        {
            return _converter.GetResourceFrom(bytes);
        }

        protected override byte[] GetBytesFrom(object resource)
        {
            return _converter.GetBytesFrom(resource as Bitmap);
        }
    }
}
