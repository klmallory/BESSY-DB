using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using System.Drawing;
using BESSy.Tests.ResourceRepositoryTests.Resources;

namespace BESSy.Tests.ResourceRepositoryTests
{
    [TestFixture]
    public class ResourceCRUDTests : FileTest
    {

        [Test]
        public void ResourceRepoLoads()
        {
            var ic = new ImageConverter();

            using (var repo = new ResourceWrapper<Bitmap>(b => ic.ConvertFrom(b) as Bitmap, testRes.ResourceManager))
            {
                Assert.AreEqual(6, repo.Length);
            }
        }

        [Test]
        public void ResourceRepoEnumerates()
        {
            var ic = new ImageConverter();

            using (var repo = new ResourceWrapper<Bitmap>(b => ic.ConvertFrom(b) as Bitmap, testRes.ResourceManager))
            {
                Assert.AreEqual(6, repo.Length);

                var bitmaps = repo.Select(s => s.Value).OfType<Bitmap>();

                Assert.AreEqual(4, bitmaps.Count());
            }
        }

        [Test]
        public void ResourceRepoFetches()
        {
            var ic = new ImageConverter();

            using (var repo = new ResourceWrapper<Bitmap>(b => ic.ConvertFrom(b) as Bitmap, testRes.ResourceManager))
            {
                Assert.AreEqual(6, repo.Length);

                var luna = repo.Fetch("Luna_DIFF");

                Assert.IsNotNull(luna);
            }
        }
    }
}
