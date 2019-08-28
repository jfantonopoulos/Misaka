using Microsoft.Extensions.DependencyModel;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using System.Text;

namespace Misaka.Classes
{
    class DynamicAssemblyLoader : AssemblyLoadContext
    {
        public DynamicAssemblyLoader()
        {
        }

        public Assembly LoadStream(MemoryStream memStream)
        {
            var deps = DependencyContext.Default;
            return LoadFromStream(memStream);
        }

        protected override Assembly Load(AssemblyName assemblyName)
        {
            var deps = DependencyContext.Default;
            return Assembly.Load(assemblyName);
        }
    }
}
