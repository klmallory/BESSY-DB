using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BESSy
{
    public static class AssemblyConstants
    {
#if NET40
        public static readonly string ASSEMBLY_NAME = "BESSy";
#endif
#if NET45
        public static readonly string ASSEMBLY_NAME = "BESSy_45";
#endif
#if NET451
        public static readonly string ASSEMBLY_NAME = "BESSy_451";
#endif
    }
}
