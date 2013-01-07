/*
Copyright © 2011, Kristen Mallory DBA klink.
All rights reserved.
*/
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using NUnit.Framework;
using SevenZip.Tests.Mocks;

namespace SevenZip.Tests.CompressionTests
{
    [TestFixture]
    public class CompressFileTests
    {
        int[] testData = null;
        StructuredDataMock[] structuredTestData = null;

        CoderPropID[] propertyNames = null;
        object[] properties = null;
        string workingpath = null;

        [TestFixtureSetUp]
        public void FixtureSetup()
        {
            testData = new int[15625];

            Random rnd = new Random();

            for (int i = 0; i <= testData.Length - 1; i++)
            {
                if (rnd.NextDouble() >= .5)
                    testData[i] = rnd.Next(0, 255);
                else
                    testData[i] = 0;
            }

            List<StructuredDataMock> structs = new List<StructuredDataMock>();

            for (int i = 0; i < 15625; i++)
            {
                if (rnd.NextDouble() >= .5)
                    structs.Add(new StructuredDataMock { Index = i, TypeId = rnd.Next(0, 32767) });
                
            }

            structuredTestData = structs.ToArray();

            propertyNames = new CoderPropID[]
				{
					CoderPropID.DictionarySize,
					CoderPropID.PosStateBits,
					CoderPropID.LitContextBits,
					CoderPropID.LitPosBits,
					CoderPropID.Algorithm,
					CoderPropID.NumFastBytes,
					CoderPropID.MatchFinder,
					CoderPropID.EndMarker
				};

            properties = new object[]
				{
					(Int32)(1 << 4),    //DictionarySize
					2,                  //PosStateBits
					3,                  //LitContextBits
					0,                  //LitPosBits
					2,                  //Algorithm
					128,                //NumFastBytes
					"bt4",              //MatchFinder
					false                //EndMarker
				};

            workingpath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
        }

        [Test]
        public void CanZipAndUnzipBlockBufferToFile()
        {
            var watch = Stopwatch.StartNew();

            var filePath = Path.Combine(workingpath, string.Format("test_{0}.bin", DateTime.Now.Ticks));

            Byte[] bytes;

            using (var binStream = new MemoryStream())
            {
                using (var b = new BinaryWriter(binStream))
                {
                    for (int i = 0; i <= testData.Length - 1; i++)
                        b.Write(testData[i]);

                    b.Flush();

                    bytes = new Byte[testData.Length * 4];

                    binStream.Position = 0;

                    binStream.Read(bytes, 0, bytes.Length);

                    Assert.AreEqual(testData.Length * 4, binStream.Length);
                }
            }

            using (var inStream = new MemoryStream(bytes, false))
            {
                using (Stream outStream = new FileStream(filePath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None, 1024, FileOptions.SequentialScan))
                {
                    LZMA.Encoder encoder = new LZMA.Encoder();
                    encoder.SetCoderProperties(propertyNames, properties);
                    encoder.WriteCoderProperties(outStream);

                    long uncompressedSize = (long)bytes.Length;

                    for (int i = 0; i < 8; i++)
                    {
                        var bit = (Byte)(uncompressedSize >> (8 * i));
                        outStream.WriteByte(bit);
                    }

                    inStream.Position = 0;
                    encoder.Code(inStream, outStream, -1, -1, null);

                    outStream.Flush();
                    outStream.Close();
                }
            }

            Assert.Greater(bytes.Length, 0);
            Assert.IsTrue(File.Exists(filePath));

            using (var readStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.None, 1024, FileOptions.SequentialScan))
            {
                byte[] zipProps = new byte[5];
                if (readStream.Read(zipProps, 0, 5) != 5)
                    throw (new Exception("input .lzma is too short"));

                LZMA.Decoder decoder = new LZMA.Decoder();

                decoder.SetDecoderProperties(zipProps);

                long outSize = 0;
                for (int i = 0; i < 8; i++)
                {
                    int v = readStream.ReadByte();
                    if (v < 0)
                        throw (new Exception("Can't Read 1"));
                    outSize |= ((long)(byte)v) << (8 * i);
                }

                long compressedSize = readStream.Length - readStream.Position;

                using (Stream outStream = new MemoryStream())
                {
                    decoder.Code(readStream, outStream, compressedSize, outSize, null);

                    bytes = new Byte[outStream.Length];

                    using (var b = new BinaryReader(outStream))
                    {
                        var len = (bytes.Length / 4);

                        outStream.Position = 0;

                        int[] readData = new int[len];

                        for (int i = 0; i < readData.Length; i++)
                            readData[i] = b.ReadInt32();

                        Assert.AreEqual(testData.Length, readData.Length);

                        for (int i = 0; i < readData.Length; i++)
                            Assert.AreEqual(testData[i], readData[i]);
                    }

                    outStream.Close();
                }
            }

            Console.WriteLine(String.Format("Milliseconds To Compress & Uncompress 62k: {0}", watch.ElapsedMilliseconds));
        }

        [Test]
        public void CanZipAndUnzipStructuredBufferToFile()
        {
            var watch = Stopwatch.StartNew();

            var filePath = Path.Combine(workingpath, string.Format("test_{0}.bin", DateTime.Now.Ticks));

            Byte[] bytes;

            using (var binStream = new MemoryStream())
            {
                using (var b = new BinaryWriter(binStream))
                {
                    var bf = new BinaryFormatter();

                    bf.Serialize(binStream, structuredTestData);

                    bytes = new Byte[binStream.Length];

                    binStream.Position = 0;

                    binStream.Read(bytes, 0, bytes.Length);
                }
            }

            using (var inStream = new MemoryStream(bytes, false))
            {
                using (Stream outStream = new FileStream(filePath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None, 1024, FileOptions.SequentialScan))
                {
                    LZMA.Encoder encoder = new LZMA.Encoder();
                    encoder.SetCoderProperties(propertyNames, properties);
                    encoder.WriteCoderProperties(outStream);

                    long uncompressedSize = (long)bytes.Length;

                    for (int i = 0; i < 8; i++)
                    {
                        var bit = (Byte)(uncompressedSize >> (8 * i));
                        outStream.WriteByte(bit);
                    }

                    inStream.Position = 0;
                    encoder.Code(inStream, outStream, -1, -1, null);

                    outStream.Flush();
                    outStream.Close();
                }
            }

            Assert.Greater(bytes.Length, 0);
            Assert.IsTrue(File.Exists(filePath));

            using (var readStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.None, 1024, FileOptions.SequentialScan))
            {
                byte[] zipProps = new byte[5];
                if (readStream.Read(zipProps, 0, 5) != 5)
                    throw (new Exception("input .lzma is too short"));

                LZMA.Decoder decoder = new LZMA.Decoder();

                decoder.SetDecoderProperties(zipProps);

                long outSize = 0;
                for (int i = 0; i < 8; i++)
                {
                    int v = readStream.ReadByte();
                    if (v < 0)
                        throw (new Exception("Can't Read 1"));
                    outSize |= ((long)(byte)v) << (8 * i);
                }

                long compressedSize = readStream.Length - readStream.Position;

                using (Stream outStream = new MemoryStream())
                {
                    decoder.Code(readStream, outStream, compressedSize, outSize, null);

                    outStream.Position = 0;

                    var bf = new BinaryFormatter();

                    var readData = bf.Deserialize(outStream) as StructuredDataMock[];

                    Assert.AreEqual(structuredTestData.Length, readData.Length);

                    for (int i = 0; i < readData.Length; i++)
                    {
                        Assert.AreEqual(readData[i].Index, structuredTestData[i].Index);
                        Assert.AreEqual(readData[i].TypeId, structuredTestData[i].TypeId);
                    }

                    outStream.Close();
                }
            }

            Console.WriteLine(String.Format("Milliseconds To Compress & Uncompress {0} StructureData : {1}", structuredTestData.Length, watch.ElapsedMilliseconds));
        }
    }
}
