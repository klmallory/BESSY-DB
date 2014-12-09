using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using System.Drawing;
using BESSy.Tests.ResourceRepositoryTests.Resources;
using System.IO;
using BESSy.Resources;

namespace BESSy.Tests.ResourceRepositoryTests
{
    [TestFixture]
    public class ResourceCRUDTests : FileTest
    {

        [Test]
        public void ResourceRepoLoads()
        {
            var ic = new ImageConverter();

            using (var repo = new ResourceWrapper<Bitmap>(b => ic.ConvertFrom(b) as Bitmap, s => null, n => null, testRes.ResourceManager))
            {
                Assert.AreEqual(8, repo.Length);
            }
        }

        [Test]
        public void ResourceRepoEnumerates()
        {
            var ic = new ImageConverter();

            using (var repo = new ResourceWrapper<Bitmap>(b => ic.ConvertFrom(b) as Bitmap, s => null, n => null, testRes.ResourceManager))
            {
                Assert.AreEqual(8, repo.Length);

                var bitmaps = repo.Where(s => s.Value != null).Select(s => s.Value).OfType<Bitmap>();

                Assert.AreEqual(4, bitmaps.Count());

                var en = repo.GetEnumerator();

                if (en.MoveNext())
                {
                    Assert.IsNotNull(en.Current);
                    en.Reset();
                }
            }
        }

        [Test]
        public void ResourceRepoFetches()
        {
            var ic = new ImageConverter();

            using (var repo = new ResourceWrapper<Bitmap>(b => ic.ConvertFrom(b) as Bitmap, s => null, n => null, testRes.ResourceManager))
            {
                Assert.AreEqual(8, repo.Length);

                var luna = repo.Fetch("Luna_DIFF");

                Assert.IsNotNull(luna);
            }
        }

        [Test]
        public void ResourceRepoFetchesAudio()
        {
            var ic = new ImageConverter();

            using (var repo = new ResourceWrapper<Stream>(b => new MemoryStream(b), s => null, n => null, testRes.ResourceManager))
            {
                Assert.AreEqual(8, repo.Length);

                var sound = repo.Fetch("MiscAngelic");

                Assert.IsNotNull(sound);
            }
        }

        [Test]
        public void ResourceRepoFetchesWavAudio()
        {
            var ic = new ImageConverter();

            using (var repo = new ResourceWrapper<Stream>(b => new MemoryStream(b), s => s, n => null, testRes.ResourceManager))
            {
                Assert.AreEqual(8, repo.Length);

                var sound = repo.Fetch("Speech_Misrecognition");

                Assert.IsNotNull(sound);

                sound = repo.GetFileStream("Speech_Misrecognition");

                Assert.IsNotNull(sound);
            }
        }

        [Test]
        public void ResourceRepoFetchesString()
        {
            var ic = new ImageConverter();

            using (var repo = new ResourceWrapper<string>(b => new ASCIIEncoding().GetString(b), s => null, n => null, testRes.ResourceManager))
            {
                Assert.AreEqual(8, repo.Length);

                var value = repo.Fetch("TestString");

                Assert.IsNotNull(value);

                var value2 = repo.GetContents("TestString");

                Assert.AreEqual(value, value2);

                Assert.AreEqual(repo.CastContentsAs<string>("TestString"), value2);

                repo.Clear();
            }
        }
    }
}
