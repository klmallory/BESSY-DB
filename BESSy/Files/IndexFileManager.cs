using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BESSy.Serialization;

namespace BESSy.Files
{
    public class IndexFileManager<IdType, PropertyType> : BatchFileManager<IndexPropertyPair<IdType, PropertyType>>
    {
        public IndexFileManager(int bufferSize, IFormatter formatter)
            : base(bufferSize, formatter)
        {

        }
    }
}
