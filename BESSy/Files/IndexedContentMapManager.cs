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
//using System.Collections;
//using System.Collections.Generic;
//using System.Diagnostics;
//using System.IO;
//using System.IO.MemoryMappedFiles;
//using System.Linq;
//using System.Security.AccessControl;
//using System.Text;
//using System.Threading;
//using System.Threading.Tasks;
//using BESSy.Extensions;
//using BESSy.Parallelization;
//using BESSy.Serialization;
//using BESSy.Serialization.Converters;
//using BESSy.Synchronization;
//using BESSy.Json;
//using BESSy.Json.Linq;

//namespace BESSy.Files
//{
//    public class IndexedContentMapManager<ResourceType, IdType> : IndexedEntityMapManager<ResourceContainer<ResourceType>, IdType>
//    {
//        public IndexedContentMapManager(IBinConverter<IdType> idConverter, IQueryableFormatter formatter, IContentConverter<ResourceType> converter)
//            : base(idConverter, formatter)
//        {
//            _contentConverter = converter;
//        }

//        protected IContentConverter<ResourceType> _contentConverter;



//        protected override IDictionary<IdType, System.IO.Stream> GetFormattedFrom(List<KeyValuePair<int, int>> groups, IDictionary<IdType, ResourceContainer<ResourceType>> queue, out int rowSize)
//        {
//            var sync = new object();
//            rowSize = Stride;
//            var streams = new Dictionary<IdType, Stream>();

//            var items = queue.OrderBy(q => q.Key, _idConverter).ToList();

//            if (items.Count < 1)
//                return streams;

//            Parallel.ForEach(groups, delegate(KeyValuePair<int, int> group)
//            {
//                foreach (var item in items.Skip(group.Key).Take((group.Value - group.Key) + 1))
//                {
//                    if (item.Value == null || item.Value.Value == null)
//                        continue;

//                    Stream stream;

//                    if (!_formatter.TryFormatObj(
//                        new ResourceContainer<byte[]>()
//                        {
//                            Name = item.Value.Name,
//                            Value = _contentConverter.GetBytesFrom(item.Value.Name, item.Value.Value)
//                        }, out stream))

//                        stream = new MemoryStream();

//                    lock (sync)
//                        streams.Add(item.Key, stream);
//                }
//            });

//            rowSize = (int)(((streams.Values.Max(v => v.Length) / 256) + 1) * 256);

//            return streams;
//        }

//        public override int SaveBatchToFile(IList<ResourceContainer<ResourceType>> objs, int segmentStart)
//        {
//            if (objs == null || objs.Count == 0)
//                return 0;

//            lock (_syncFlush)
//            {
//                using (var lck = Synchronizer.Lock(new Range<int>(segmentStart, segmentStart + objs.Count)))
//                {
//                    using (var view = _file.CreateViewStream(Stride * segmentStart
//                        , objs.Count * Stride
//                        , MemoryMappedFileAccess.ReadWriteExecute))
//                    {
//                        foreach (var obj in objs)
//                        {
//                            Stream stream;
//                            if (!_formatter.TryFormatObj(new ResourceContainer<byte[]>()
//                            {
//                                Name = obj.Name,
//                                Value = _contentConverter.GetBytesFrom(obj.Name, obj.Value)
//                            }, out stream))
//                                stream = new MemoryStream();
//#if DEBUG
//                            if (stream.Length > Stride)
//                                throw new InvalidDataException("this object is too large for this file format.");
//#endif

//                            stream.SetLength(Stride);

//                            stream.Position = 0;
//                            stream.WriteAllTo(view);
//                        }

//                        view.Flush();
//                        view.Close();
//                    }
//                }
//            }
//            return segmentStart + objs.Count;
//        }

//        protected sealed override ResourceContainer<ResourceType> LoadFrom(JObject token)
//        {
//            var bytes = token.ToObject<ResourceContainer<byte[]>>(_formatter.Serializer);

//            return new ResourceContainer<ResourceType>()
//            {
//                Name = bytes.Name,
//                Value = _contentConverter.GetResourceFrom(bytes.Value)
//            };
//        }

//        public override ResourceContainer<ResourceType> LoadFromSegment(int segment)
//        {
//            using (var lck = Synchronizer.Lock(segment))
//            {
//                using (var view = _file.CreateViewStream(Stride * segment, Stride, MemoryMappedFileAccess.Read))
//                {
//                    ResourceContainer<byte[]> retVal;

//                    if (_formatter.TryUnformatObj<ResourceContainer<byte[]>>(view, out retVal))
//                    {
//                        view.Close();

//                        return new ResourceContainer<ResourceType>() { Name = retVal.Name, Value = _contentConverter.GetResourceFrom(retVal.Value) };
//                    }

//                    view.Close();

//                    return default(ResourceContainer<ResourceType>);
//                }
//            }
//        }

//        public override bool TryLoadFromSegment(int segment, out ResourceContainer<ResourceType> entity)
//        {
//            ResourceContainer<byte[]> bytes;

//            using (var lck = Synchronizer.Lock(segment))
//            {
//                using (var view = _file.CreateViewStream(Stride * segment, Stride, MemoryMappedFileAccess.Read))
//                {
//                    if (_formatter.TryUnformatObj<ResourceContainer<byte[]>>(view, out bytes))
//                    {
//                        entity = new ResourceContainer<ResourceType>() { Name = bytes.Name, Value = _contentConverter.GetResourceFrom(bytes.Value) };

//                        view.Close();

//                        return true;
//                    }

//                    entity = null;

//                    view.Close();

//                    return false;
//                }
//            }
//        }

//        public override bool SaveToFile(ResourceContainer<ResourceType> obj, int segment)
//        {
//            throw new NotImplementedException();

//            //Stream stream;

//            //if (obj == null || !_formatter.TryFormatObj(new ResourceContainer<byte[]>() { Name = obj.Name, Value = _contentConverter.GetBytesFrom(obj.Value)}, out stream))
//            //    stream = new MemoryStream(new Byte[Stride]);

//            //if (stream.Length > Stride)
//            //    throw new InvalidDataException("this object is too large for this file format.");

//            //using (var lck = Synchronizer.Lock(segment))
//            //{
//            //    using (var view = _file.CreateViewStream(Stride * segment, Stride, MemoryMappedFileAccess.ReadWriteExecute))
//            //    {
//            //        stream.Position = 0;
//            //        stream.WriteAllTo(view);

//            //        view.Flush();
//            //        view.Close();
//            //    }
//            //}

//            //return true;
//        }
//    }
//}
