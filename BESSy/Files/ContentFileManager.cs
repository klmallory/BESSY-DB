///*
//Copyright (c) 2011,2012,2013 Kristen Mallory dba Klink

//Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"),
//to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, 
//and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

//The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

//THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, 
//FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, 
//DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, 
//OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

//*/

//using System;
//using System.Collections.Generic;
//using System.Configuration;
//using System.Diagnostics;
//using System.IO;
//using System.Linq;
//using System.Security.AccessControl;
//using System.Text;
//using BESSy.Json;
//using BESSy.Seeding;
//using BESSy.Serialization;
//using BESSy.Extensions;
//using BESSy.Serialization.Converters;

namespace BESSy.Files
{
public interface IContentConverter<ResourceType>
{
    ResourceType GetResourceFrom(byte[] val);
    byte[] GetBytesFrom(ResourceType resource);
}

//    public class ContentFileManager<ResourceType> : BatchFileManager<ResourceContainer<ResourceType>>
//    {
//        public ContentFileManager(IFormatter formatter, IContentConverter<ResourceType> converter) : this(Environment.SystemPageSize, Environment.SystemPageSize, formatter, converter) 
//        {
//            if (converter == null)
//                throw new ArgumentNullException("converter can not be null");

//            _converter = converter; 
//        }

//        public ContentFileManager(int batchSize, int bufferSize, IFormatter formatter, IContentConverter<ResourceType> converter) : base(batchSize, bufferSize, formatter) 
//        {
//            if (converter == null)
//                throw new ArgumentNullException("converter can not be null");

//            _converter = converter; 
//        }

//        IContentConverter<ResourceType> _converter;

//        public override IList<ResourceContainer<ResourceType>> LoadBatchFrom(Stream stream)
//        {
//            try
//            {
//                long pos = stream.Position;
//                var len = stream.Length;
//                if (len - pos < SegmentDelimeter.Array.Length)
//                    return new List<ResourceContainer<ResourceType>>();

//                List<ResourceContainer<byte[]>> containers = null;

//                lock (_syncRoot)
//                {
//                    var buffer = new byte[_bufferSize];

//                    int read = stream.Read(buffer, 0, buffer.Length);
//                    int count = 0;

//                    ArraySegment<byte> s = new ArraySegment<byte>(buffer, 0, SeedStart.Array.Length);
//                    if (s.Equals<byte>(SeedStart))
//                    {
//                        FindBatchStart(stream);
//                        read = stream.Read(buffer, 0, buffer.Length);
//                    }

//                    ArraySegment<byte> c = new ArraySegment<byte>(buffer, 0, BatchStart.Array.Length);
//                    if (c.Equals<byte>(BatchStart))
//                    {
//                        count = _batchConverter.FromBytes(buffer.Skip(BatchStart.Array.Length).Take(4).ToArray());
//                        buffer = buffer.Skip(BatchStart.Array.Length + 4).ToArray();
//                    }
//                    else
//                    {
//                        count = _batchConverter.FromBytes(buffer.Take(4).ToArray());
//                        buffer = buffer.Skip(4).ToArray();
//                    }

//                    pos = stream.Position;

//                    var match = SegmentDelimeter.Array[0];
//                    var delLength = SegmentDelimeter.Array.Length;

//                    using (var bufferStream = new MemoryStream())
//                    {
//                        while (read > SegmentDelimeter.Array.Length)
//                        {
//                            var index = Array.FindIndex(buffer, b => b == match);

//                            while (index >= 0 && index <= buffer.Length - delLength)
//                            {
//                                ArraySegment<byte> b = new ArraySegment<byte>(buffer, index, delLength);

//                                if (b.Equals<byte>(SegmentDelimeter))
//                                {
//                                    bufferStream.Write(buffer, 0, index);

//                                    stream.Position = pos + index + delLength;

//                                    if (count > 0)
//                                        containers = _formatter.UnformatObj<ResourceContainer<byte[]>[]>(bufferStream).ToList();
//                                    else
//                                        containers = new List<ResourceContainer<byte[]>>();
//                                }

//                                index = Array.FindIndex(buffer, index + 1, n => n == match);
//                            }

//                            bufferStream.Write(buffer, 0, buffer.Length - delLength);

//                            pos = stream.Position -= delLength;

//                            read = stream.Read(buffer, 0, buffer.Length);
//                        }
//                    }
//                }

//                if (containers != null)
//                    return containers.Select(s => new ResourceContainer<ResourceType>() { Name = s.Name, Value = _converter.GetResourceFrom(s.Value) }).ToList();
//            }
//            catch (JsonSerializationException jsEx) { Trace.TraceError(String.Format("File could not be deserialized : \r\n {0} \r\n {1}", jsEx.InnerException, jsEx)); throw; }
//            catch (Exception ex) { Trace.TraceError(String.Format(_error, "", ex)); throw; }

//            return new List<ResourceContainer<ResourceType>>();
//        }

//        public override  long SaveBatch(Stream stream, IList<ResourceContainer<ResourceType>> objs, long atPosition)
//        {
//#if DEBUG
//            if (objs == null)
//                throw new ArgumentNullException("obj");
//#endif

//            try
//            {
//                var bytes = objs.Where(s => s != null).Select(s => new ResourceContainer<byte[]>() { Name = s.Name, Value = _converter.GetBytesFrom(s.Name, s.Value) }).ToArray();
//                var formatted = _formatter.FormatObjStream(bytes);

//                lock (_syncRoot)
//                {
//                    if (atPosition >= 0)
//                        stream.Position = atPosition;

//                    if (stream.Position == 0)
//                        stream.Write(BatchStart.Array, 0, BatchStart.Array.Length);

//                    stream.Write(_batchConverter.ToBytes(objs.Count), 0, _batchConverter.Length);

//                    formatted.WriteAllTo(stream);

//                    stream.Write(SegmentDelimeter.Array, 0, SegmentDelimeter.Array.Length);

//                    var position = stream.Position;

//                    stream.Flush();

//                    return position;
//                }
//            }
//            catch (JsonSerializationException jsEx) { Trace.TraceError(String.Format("File could not be serialized : \r\n {0} \r\n {1}", jsEx.InnerException, jsEx)); throw; }
//            catch (Exception ex) { Trace.TraceError(String.Format(_error, "", ex)); throw; }
//        }
//    }
}
