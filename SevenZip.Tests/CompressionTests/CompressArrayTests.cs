/*
Copyright © 2011, Kristen Mallory DBA klink.
All rights reserved.
*/
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;

namespace SevenZip.Tests.ZipTests
{
    [TestFixture]
    public class CompressArrayTests
    {
        Byte[] testData = null;
        CoderPropID[] propertyNames = null;
        object[] properties = null;

        [TestFixtureSetUp]
        public void FixtureSetup()
        {
            //7za u -t7z -r -mx=9 -mmt=4 -mhe=on -mem=AES256 -pPASSWORD Backup.7z ./FILES/*.*
            //7za u -t7z -r -mx -mm=lzma2 -mhe -xr!*.pst -mem=AES256 -pPASSWORD Backup.7z ./FILES/*.*
            //System.Security.Cryptography.Aes.Create()
            //System.Security.Cryptography.RC2

            testData = new Byte[255];

            for (byte i = 0; i <= testData.Length - 1; i++)
            {
                testData[i] = i;
            }

            propertyNames = new CoderPropID[]
				{
					CoderPropID.DictionarySize,
					CoderPropID.PosStateBits,
					CoderPropID.LitContextBits,
					CoderPropID.LitPosBits,
					CoderPropID.Algorithm,
					CoderPropID.NumFastBytes,
					CoderPropID.MatchFinder,
					CoderPropID.EndMarker,
                     
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
        }

        [Test]
        public void CanZipAndUnzipByteArray()
        {
            Byte[] bytes;

            using (var inStream = new MemoryStream(testData, false))
            {
                using (Stream outStream = new MemoryStream())
                {
                    LZMA.Encoder encoder = new LZMA.Encoder();
                    encoder.SetCoderProperties(propertyNames, properties);
                    encoder.WriteCoderProperties(outStream);

                    var fileSize = inStream.Length;

                    for (int i = 0; i < 8; i++)
                        outStream.WriteByte((Byte)(fileSize >> (8 * i)));

                    inStream.Position = 0;
                    encoder.Code(inStream, outStream, -1, -1, null);

                    bytes = new Byte[outStream.Length];
                    outStream.Position = 0;

                    outStream.Read(bytes, 0, bytes.Length);
                }
            }

            Assert.Greater(bytes.Length, 0);

            using (var readStream = new MemoryStream(bytes, false))
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

                Stream outStream = new MemoryStream();

                decoder.Code(readStream, outStream, compressedSize, outSize, null);

                bytes = new Byte[outStream.Length];
                outStream.Position = 0;

                outStream.Read(bytes, 0, bytes.Length);
            }

            Assert.Greater(bytes.Length, 0);
            Assert.AreEqual(testData.Length, bytes.Length);

            for (int i = 0; i <= bytes.Length -1; i++)
            {
                Assert.AreEqual(testData[i], bytes[i]);
            }
        }
    }
}
