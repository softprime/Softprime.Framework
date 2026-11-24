
using System;
using System.IO;
using System.Reflection;
using System.Runtime.Versioning;
using Softprime.Framework.Api.Dto.Default;
using Softprime.Framework.Common.Extensions;
using Softprime.Framework.DAL.Entity;
using Softprime.Framework.Domain.DAL;
#if !NET48
using System.Runtime.Loader;
#endif

namespace Softprime.Framework.PackageSmoke
{
    internal static class Program
    {
        private static void Main(string[] args)
        {
            Console.WriteLine($"[RUNTIME] {typeof(Program).Assembly.GetCustomAttribute<TargetFrameworkAttribute>().FrameworkName}; CLR {Environment.Version}");
#if NET48
            const string sharedFramework = ".NETStandard,Version=v2.0";
            const string domainFramework = ".NETFramework,Version=v4.8";
#else
            const string sharedFramework = ".NETStandard,Version=v2.1";
            const string domainFramework = sharedFramework;
#endif
            CheckAsset(typeof(Strings).Assembly, sharedFramework);
            CheckAsset(typeof(PagedResultDto<>).Assembly, sharedFramework);
            CheckAsset(typeof(Entity).Assembly, domainFramework);
            CheckAsset(typeof(IEntity).Assembly, domainFramework);

            var entity = new SmokeEntity();
            if (!entity.IsTransient())
                throw new InvalidOperationException("A nova entidade deveria ser transiente.");
            entity.SetId(5);

            var dto = new PagedResultDto<int> { TotalCount = 1, Results = new[] { 5 } };
            if (entity.IsTransient() || dto.TotalCount != 1 || "item-5".OnlyNumbers() != "5")
                throw new InvalidOperationException("Falha no consumo dos contratos de Domain, DTO ou Common.");

#if !NET48
            var legacy = new NetStandardContext(args[0], args[1]);
            try
            {
                var common = legacy.LoadFromAssemblyName(typeof(Strings).Assembly.GetName());
                var dtoAssembly = legacy.LoadFromAssemblyName(typeof(PagedResultDto<>).Assembly.GetName());
                var domain = legacy.LoadFromAssemblyName(typeof(Entity).Assembly.GetName());
                var dal = legacy.LoadFromAssemblyName(typeof(IEntity).Assembly.GetName());
                foreach (var assembly in new[] { common, dtoAssembly, domain, dal })
                    CheckAsset(assembly, ".NETStandard,Version=v2.0");

                var onlyNumbers = common.GetType(typeof(Strings).FullName, true).GetMethod(nameof(Strings.OnlyNumbers));
                var digits = (string)onlyNumbers.Invoke(null, new object[] { "item-7" });
                var dtoType = dtoAssembly.GetType(typeof(PagedResultDto<>).FullName, true).MakeGenericType(typeof(int));
                var page = Activator.CreateInstance(dtoType);
                var count = dtoType.GetProperty(nameof(PagedResultDto<int>.TotalCount));
                count.SetValue(page, 7);
                var entityType = domain.GetType(typeof(Entity).FullName, true);
                if (digits != "7" || (int)count.GetValue(page) != 7 ||
                    !dal.GetType(typeof(IEntity).FullName, true).IsAssignableFrom(entityType))
                    throw new InvalidOperationException("Falha no consumo explícito dos assets netstandard2.0.");
            }
            finally
            {
                legacy.Unload();
            }
#endif
        }

        private static void CheckAsset(Assembly assembly, string expected)
        {
            var target = assembly.GetCustomAttribute<TargetFrameworkAttribute>().FrameworkName;
            var name = assembly.GetName().Name;
            if (target != expected)
                throw new InvalidOperationException($"{name}: esperado {expected}, carregado {target}.");
            Console.WriteLine($"[LOADED_ASSET] {name}: {target} ({assembly.Location})");
        }

        private sealed class SmokeEntity : Entity
        {
        }

#if !NET48
        private sealed class NetStandardContext : AssemblyLoadContext
        {
            private readonly string packages;
            private readonly string version;

            public NetStandardContext(string packages, string version) : base(isCollectible: true)
            {
                this.packages = packages;
                this.version = version;
            }

            protected override Assembly Load(AssemblyName name)
            {
                if (!name.Name.StartsWith("Softprime.Framework.", StringComparison.Ordinal))
                    return null;

                var file = Path.Combine(packages, name.Name.ToLowerInvariant(), version,
                    "lib", "netstandard2.0", name.Name + ".dll");
                return LoadFromAssemblyPath(file);
            }
        }
#endif
    }
}
