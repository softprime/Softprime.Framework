using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Softprime.Framework.Multitenancy.Internal;
using Xunit;

namespace Softprime.Framework.Multitenancy.Tests
{
    public class TenantResolutionTests
    {
        [Theory]
        [InlineData(" ALPHA ", "?tenant=beta", "beta.example.test", "alpha")]
        [InlineData(null, "?tenant=BETA", "alpha.example.test", "beta")]
        [InlineData(" ", null, "ALPHA.example.test:8080", "alpha")]
        [InlineData(null, "?tenant=", "beta.example.test", "beta")]
        [InlineData(null, null, "localhost", null)]
        [InlineData("!invalid!", "?tenant=beta", "beta.example.test", null)]
        [InlineData(null, "?tenant=unknown", "alpha.example.test", null)]
        [InlineData(null, null, "unknown.example.test", null)]
        public async Task Invoke_ConfiguredSources_SetsOnlyResolvedTenantAndContinues(
            string header, string query, string host, string expected)
        {
            using var cache = new MemoryCache(Options.Create(new MemoryCacheOptions()));
            var resolver = new ConfiguredTenantResolver(cache, ConfiguredTenantResolver.CreateOptions());
            var request = CreateRequest(header, query, host);
            HttpContext downstream = null;
            string downstreamTenant = null;
            var middleware = new TenantResolutionMiddleware(context =>
            {
                downstream = context;
                downstreamTenant = context.GetTenant<TestTenant>()?.TenantKey;
                return Task.CompletedTask;
            });

            await middleware.Invoke(request, resolver);

            downstream.Should().BeSameAs(request);
            downstreamTenant.Should().Be(expected);
            request.GetTenantContext().Should().Be(expected == null ? null : cache.Get(expected));
        }

        [Fact]
        public async Task Invoke_SeparateRequests_IsolatesTenantContextAndReusesTheSameTenant()
        {
            using var cache = new MemoryCache(Options.Create(new MemoryCacheOptions()));
            var resolver = new ConfiguredTenantResolver(cache, ConfiguredTenantResolver.CreateOptions());
            var middleware = new TenantResolutionMiddleware(_ => Task.CompletedTask);
            var first = CreateRequest("alpha", null, "localhost");
            var second = CreateRequest(null, "?tenant=beta", "localhost");
            var repeated = CreateRequest(null, null, "alpha.example.test");
            var unresolved = CreateRequest(null, null, "localhost");

            await middleware.Invoke(first, resolver);
            first.GetTenantContext().Properties.Add("sample", "alpha-only");
            await middleware.Invoke(second, resolver);
            await middleware.Invoke(repeated, resolver);
            await middleware.Invoke(unresolved, resolver);

            second.GetTenantContext().Should().NotBeSameAs(first.GetTenantContext());
            second.GetTenantContext().Properties.Should().BeEmpty();
            repeated.GetTenantContext().Should().BeSameAs(first.GetTenantContext());
            repeated.GetTenantContext().Properties.Should().Contain("sample", "alpha-only");
            unresolved.GetTenantContext().Should().BeNull();
        }

        private static DefaultHttpContext CreateRequest(string header, string query, string host)
        {
            var context = new DefaultHttpContext();
            context.Request.Host = new HostString(host);
            if (query != null)
                context.Request.QueryString = new QueryString(query);
            if (header != null)
                context.Request.Headers[ConfiguredTenantResolver.TenantHeader] = header;
            return context;
        }
    }
}
