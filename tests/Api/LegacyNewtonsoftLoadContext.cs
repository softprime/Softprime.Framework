using System;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;

namespace Softprime.Framework.Api.Tests
{
    internal sealed class LegacyNewtonsoftLoadContext : AssemblyLoadContext
    {
        private const string SerializerName = "Newtonsoft.Json";

        internal LegacyNewtonsoftLoadContext() : base("Legacy Newtonsoft.Json")
        {
        }

        internal Assembly LoadSerializer() => LoadFromAssemblyName(new AssemblyName(SerializerName));

        protected override Assembly Load(AssemblyName assemblyName) =>
            assemblyName.Name == SerializerName
                ? LoadFromAssemblyPath(Path.Combine(AppContext.BaseDirectory, "legacy", "Newtonsoft.Json.dll"))
                : null;
    }
}
