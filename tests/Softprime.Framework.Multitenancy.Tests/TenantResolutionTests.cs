using System;
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
        public async Task Invoke_UnresolvedRequest_PreservesTheContextAlreadyOnTheRequest()
        {
            using var cache = new MemoryCache(Options.Create(new MemoryCacheOptions()));
            var resolver = new ConfiguredTenantResolver(cache, ConfiguredTenantResolver.CreateOptions());
            var middleware = new TenantResolutionMiddleware(_ => Task.CompletedTask);
            var request = CreateRequest("unknown", null, "localhost");
            using var existing = new TenantContext(new TestTenant("from-upstream"));
            request.SetTenantContext(existing);

            await middleware.Invoke(request, resolver);

            request.GetTenantContext().Should().BeSameAs(existing);
            request.GetTenant<TestTenant>().TenantKey.Should().Be("from-upstream");
        }

        [Fact]
        public async Task Invoke_ResolvedRequest_ReplacesTheContextAlreadyOnTheRequest()
        {
            using var cache = new MemoryCache(Options.Create(new MemoryCacheOptions()));
            var resolver = new ConfiguredTenantResolver(cache, ConfiguredTenantResolver.CreateOptions());
            var middleware = new TenantResolutionMiddleware(_ => Task.CompletedTask);
            var request = CreateRequest("beta", null, "localhost");
            using var existing = new TenantContext(new TestTenant("from-upstream"));
            request.SetTenantContext(existing);

            await middleware.Invoke(request, resolver);

            request.GetTenantContext().Should().NotBeSameAs(existing);
            request.GetTenant<TestTenant>().TenantKey.Should().Be("beta");
        }

        [Fact]
        public async Task Invoke_ResolverFailure_PropagatesAndDoesNotRunDownstream()
        {
            var failure = new InvalidOperationException("synthetic resolution failure");
            var request = CreateRequest("beta", null, "localhost");
            var calls = 0;
            var middleware = new TenantResolutionMiddleware(_ =>
            {
                calls++;
                return Task.CompletedTask;
            });

            Func<Task> invoke = () => middleware.Invoke(request, new FailingResolver(failure));

            (await invoke.Should().ThrowExactlyAsync<InvalidOperationException>()).Which.Should().BeSameAs(failure);
            calls.Should().Be(0);
            request.GetTenantContext().Should().BeNull();
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

        private sealed class FailingResolver : ITenantResolver
        {
            private readonly Exception failure;

            public FailingResolver(Exception failure) => this.failure = failure;

            public Task<TenantContext> ResolveAsync(HttpContext context) => Task.FromException<TenantContext>(failure);
        }
    }
}
