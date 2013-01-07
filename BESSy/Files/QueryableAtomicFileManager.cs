/*
Copyright © 2011, Kristen Mallory DBA klink.
All rights reserved.
*/
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using BESSy.Seeding;
using Newtonsoft.Json.Linq;

namespace BESSy.Files
{
    public interface IQueryableFileManager<EntityType> : ISegmentedFileManager<EntityType>
    {
        IEnumerable<JObject> AsQueryable();
    }

    /// <summary>
    /// TODO : Step into the ring with the big boys.
    /// full query management.
    /// fully atomic file operations.
    /// </summary>
    public class QueryableAtomicFileManager<EntityType> : ISegmentedFileManager<EntityType>
    {

        public void SaveSegment(EntityType entity, Stream stream, int segment)
        {
            throw new NotImplementedException();
        }

        public EntityType LoadSegmentFrom(Stream stream, int segment)
        {
            throw new NotImplementedException();
        }

        public int GetSegmentCount(string fileNamePath)
        {
            throw new NotImplementedException();
        }

        public int GetSegmentCount(Stream stream)
        {
            throw new NotImplementedException();
        }

        public ISeed<IdType> LoadSeedFrom<IdType>(Stream stream)
        {
            throw new NotImplementedException();
        }

        public long SaveSeed<IdType>(Stream stream, ISeed<IdType> seed)
        {
            throw new NotImplementedException();
        }

        public string WorkingPath {get; set;}

        public void Delete(string fileNamePath)
        {
            throw new NotImplementedException();
        }

        public void Delete(string fileName, string path)
        {
            throw new NotImplementedException();
        }

        public FileStream GetWritableFileStream(string fileNamePath)
        {
            throw new NotImplementedException();
        }

        public FileStream GetReadableFileStream(string fileNamePath)
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }
    }
}
