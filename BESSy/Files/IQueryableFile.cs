using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;

namespace BESSy.Files
{
    public interface IQueryableFile : IEnumerable<JObject[]>
    {
        int Pages { get; }
        JObject[] GetPage(int lastSegment);
    }
}
