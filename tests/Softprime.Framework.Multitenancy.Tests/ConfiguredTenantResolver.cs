using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;

namespace Softprime.Framework.Multitenancy.Tests
{
    internal sealed class ConfiguredTenantResolver : MemoryCacheTenantResolver
    {
        public const string TenantHeader = "X-Tenant-Key";
        private readonly Dictionary<string, TestTenant> tenants;
        private readonly Dictionary<string, string> tenantKeysByHost;

        public ConfiguredTenantResolver(IMemoryCache cache, MultitenancyOptions<TestTenant> options)
            : base(cache)
        {
            tenants = options.Tenants.ToDictionary(tenant => tenant.TenantKey, StringComparer.Ordinal);
            tenantKeysByHost = options.Tenants.ToDictionary(
                tenant => tenant.Host, tenant => tenant.TenantKey, StringComparer.OrdinalIgnoreCase);
        }

        public static MultitenancyOptions<TestTenant> CreateOptions()
        {
            return new MultitenancyOptions<TestTenant>
            {
                Tenants = new Collection<TestTenant>
                {
                    new TestTenant("alpha") { Host = "alpha.example.test", PrimaryHostname = "primary.example.test" },
                    new TestTenant("beta") { Host = "beta.example.test" }
                }
            };
        }

        protected override string GetContextIdentifier(HttpContext context)
        {
            var header = context.Request.Headers[TenantHeader].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(header))
                return header.Trim().ToLowerInvariant();

            var query = context.Request.Query["tenant"].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(query))
                return query.Trim().ToLowerInvariant();

            tenantKeysByHost.TryGetValue(context.Request.Host.Host, out var tenantKey);
            return tenantKey;
        }

        protected override string GetTenantIdentifier(TenantContext context) => context.Tenant.TenantKey;

        protected override Task<TenantContext> ResolveAsync(HttpContext context)
        {
            tenants.TryGetValue(GetContextIdentifier(context), out var tenant);
            return Task.FromResult(tenant == null ? null : new TenantContext(tenant));
        }
    }

    internal sealed class TestTenant : IAppTenant
    {
        public TestTenant(string key)
        {
            TenantKey = key;
        }

        public string TenantKey { get; set; }
        public DataBaseType DataBaseType { get; set; }
        public string ConnectionString { get; set; }
        public string Host { get; set; }
        public string PrimaryHostname { get; set; }
    }
}
