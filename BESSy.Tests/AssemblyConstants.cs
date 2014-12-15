using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BESSy.Tests
{
    public static class AssemblyConstants
    {
#if NET40
        public static readonly string ASSEMBLY_NAME = "BESSy.Tests";
#endif
#if NET45
        public static readonly string ASSEMBLY_NAME = "BESSy.Tests_45";
#endif
#if NET451
        public static readonly string ASSEMBLY_NAME = "BESSy.Tests_451";
#endif


#if NET40
        public static readonly string TEST_ASSEMBLY_NAME = "BESSy.TestAssembly";
#endif
#if NET45
        public static readonly string TEST_ASSEMBLY_NAME = "BESSy.TestAssembly_45";
#endif
#if NET451
        public static readonly string TEST_ASSEMBLY_NAME = "BESSy.TestAssembly_451";
#endif
    }
}
