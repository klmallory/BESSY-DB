using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BESSy.Extensions;

namespace BESSy.Cache
{
    internal static class Caching
    {
        internal static int DetermineOptimumCacheSize(int stride)
        {
            if (Environment.Is64BitProcess)
                return 512000000 / (((int)(Math.Ceiling(stride / (double)Environment.SystemPageSize))) * Environment.SystemPageSize).Clamp(1, int.MaxValue);
            else
                return 256000000 / (((int)(Math.Ceiling(stride / (double)Environment.SystemPageSize))) * Environment.SystemPageSize).Clamp(1, int.MaxValue);
        }
    }
}
