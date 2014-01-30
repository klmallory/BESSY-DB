using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BESSy.Files;
using System.Drawing;

namespace BESSy.Tests.Mocks
{
    public class MockImageConverter : IContentConverter<Bitmap>
    {
        ImageConverter img = new ImageConverter();

        public Bitmap GetResourceFrom(byte[] val)
        {
            return img.ConvertFrom(val) as Bitmap;
        }

        public byte[] GetBytesFrom(Bitmap resource)
        {
            return (byte[])img.ConvertTo(resource, typeof(byte[]));
        }
    }
}
