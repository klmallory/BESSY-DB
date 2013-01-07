using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BESSy.Files
{
    public interface IMapManager : IDisposable
    {
        int Stride { get; }
        int Length { get; }
        bool FlushQueueActive { get; }
        void OpenOrCreate(string fileName, int length, int stride);
    }
}
