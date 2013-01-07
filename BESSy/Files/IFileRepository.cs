using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BESSy.Files
{
    public interface IFileRepository<EntityType> : IFileManager, IFileWriter<EntityType>, IFileReader<EntityType>, IBinWriter, IDisposable
    {
    }
}
