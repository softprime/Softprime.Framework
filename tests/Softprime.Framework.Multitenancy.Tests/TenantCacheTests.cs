using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Internal;
using Microsoft.Extensions.Options;
using Xunit;

namespace Softprime.Framework.Multitenancy.Tests
{
    public class TenantCacheTests
    {
        private const string ContextKey = "requested-key";
        private const string ProbeKey = "disposable-probe";

        [Fact]
        public async Task ResolveAsync_LookupAndStorageKeysDiffer_ResolvesAliasAgain()
        {
            using var cache = CreateCache();
            var resolver = new RecordingResolver(cache, _ => new TenantContext(new TestTenant("alpha")));

            var first = await ResolveAsync(resolver, "alias");
            var fromCanonicalKey = await ResolveAsync(resolver, "alpha");
            var fromAliasAgain = await ResolveAsync(resolver, "alias");

            fromCanonicalKey.Should().BeSameAs(first);
            fromAliasAgain.Should().NotBeSameAs(first);
            cache.Get("alpha").Should().BeSameAs(fromAliasAgain);
            cache.Get("alias").Should().BeNull();
            resolver.RequestedKeys.Should().Equal("alias", "alias");
        }

        [Theory]
        [InlineData(null, 0)]
        [InlineData("unknown", 2)]
        [InlineData("no-cache-key", 2)]
        public async Task ResolveAsync_AbsentOrUncacheableResults_DoNotCreateReusableEntries(
            string requested, int expectedCalls)
        {
            using var cache = CreateCache();
            var resolver = new RecordingResolver(cache, key => key == "no-cache-key"
                ? new TenantContext(new TestTenant(" "))
                : null);

            var first = await ResolveAsync(resolver, requested);
            var second = await ResolveAsync(resolver, requested);

            resolver.RequestedKeys.Should().HaveCount(expectedCalls);
            cache.Count.Should().Be(0);
            if (requested == "no-cache-key")
            {
                first.Should().NotBeNull();
                second.Should().NotBeNull().And.NotBeSameAs(first);
            }
            else
            {
                first.Should().BeNull();
                second.Should().BeNull();
            }
        }

        [Fact]
        public async Task ResolveAsync_SlidingExpiry_DisposesOnlyExpiredContextAndReplacesIt()
        {
            var clock = new ManualClock();
            using var cache = CreateCache(clock);
            var resolver = new RecordingResolver(cache, CreateTrackedContext);

            var first = await ResolveAsync(resolver, "alpha");
            clock.Advance(TimeSpan.FromMinutes(45));
            var afterFirstInterval = await ResolveAsync(resolver, "alpha");
            clock.Advance(TimeSpan.FromMinutes(45));
            var afterSecondInterval = await ResolveAsync(resolver, "alpha");
            clock.Advance(TimeSpan.FromMinutes(30));
            var other = await ResolveAsync(resolver, "beta");
            clock.Advance(TimeSpan.FromMinutes(31));
            var replacement = await ResolveAsync(resolver, "alpha");
            var otherAfterExpiry = await ResolveAsync(resolver, "beta");

            afterFirstInterval.Should().BeSameAs(first);
            afterSecondInterval.Should().BeSameAs(first);
            replacement.Should().NotBeSameAs(first);
            otherAfterExpiry.Should().BeSameAs(other);
            resolver.RequestedKeys.Should().Equal("alpha", "beta", "alpha");
            await WaitForDisposalAsync((DisposalProbe)first.Properties[ProbeKey]);
        }

        [Fact]
        public async Task ResolveAsync_RemovedEntry_DisposesItsContextAndLoadsAgain()
        {
            using var cache = CreateCache();
            var resolver = new RecordingResolver(cache, CreateTrackedContext);
            var first = await ResolveAsync(resolver, "alpha");
            var probe = (DisposalProbe)first.Properties[ProbeKey];

            cache.Remove("alpha");
            var second = await ResolveAsync(resolver, "alpha");

            second.Should().NotBeSameAs(first);
            resolver.RequestedKeys.Should().Equal("alpha", "alpha");
            await WaitForDisposalAsync(probe);
        }

        [Fact]
        public void Dispose_RepeatedCalls_DisposePropertiesAndTenantOnce()
        {
            var tenant = new DisposableTenant();
            var alreadyDisposed = new DisposalProbe(throwOnDispose: true);
            var property = new DisposalProbe();
            var context = new TenantContext(tenant);
            context.Properties.Add("already-disposed", alreadyDisposed);
            context.Properties.Add("property", property);
            context.Properties.Add("value", "synthetic");

            context.Dispose();
            context.Dispose();

            alreadyDisposed.Calls.Should().Be(1);
            property.Calls.Should().Be(1);
            tenant.Probe.Calls.Should().Be(1);
        }

        private static MemoryCache CreateCache(ManualClock clock = null)
        {
            var options = new MemoryCacheOptions();
            if (clock != null)
                options.Clock = clock;
            return new MemoryCache(Options.Create(options));
        }

        private static Task<TenantContext> ResolveAsync(ITenantResolver resolver, string key)
        {
            var request = new DefaultHttpContext();
            request.Items[ContextKey] = key;
            return resolver.ResolveAsync(request);
        }

        private static TenantContext CreateTrackedContext(string key)
        {
            var context = new TenantContext(new TestTenant(key));
            context.Properties.Add(ProbeKey, new DisposalProbe());
            return context;
        }

        private static async Task WaitForDisposalAsync(DisposalProbe probe)
        {
            var completed = await Task.WhenAny(probe.Disposed.Task, Task.Delay(TimeSpan.FromSeconds(10)));
            completed.Should().BeSameAs(probe.Disposed.Task, "cache eviction runs its callbacks asynchronously");
            probe.Calls.Should().Be(1);
        }

        private sealed class RecordingResolver : MemoryCacheTenantResolver
        {
            private readonly Func<string, TenantContext> factory;

            public RecordingResolver(IMemoryCache cache, Func<string, TenantContext> factory)
                : base(cache)
            {
                this.factory = factory;
            }

            public List<string> RequestedKeys { get; } = new List<string>();

            protected override string GetContextIdentifier(HttpContext context) => (string)context.Items[ContextKey];

            protected override string GetTenantIdentifier(TenantContext context) => context.Tenant.TenantKey;

            protected override Task<TenantContext> ResolveAsync(HttpContext context)
            {
                var key = GetContextIdentifier(context);
                RequestedKeys.Add(key);
                return Task.FromResult(factory(key));
            }
        }

        private sealed class ManualClock : ISystemClock
        {
            public DateTimeOffset UtcNow { get; private set; } = new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero);

            public void Advance(TimeSpan elapsed) => UtcNow += elapsed;
        }

        private sealed class DisposalProbe : IDisposable
        {
            private readonly bool throwOnDispose;

            public DisposalProbe(bool throwOnDispose = false)
            {
                this.throwOnDispose = throwOnDispose;
            }

            public TaskCompletionSource<bool> Disposed { get; } =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            public int Calls { get; private set; }

            public void Dispose()
            {
                Calls++;
                Disposed.TrySetResult(true);
                if (throwOnDispose)
                    throw new ObjectDisposedException(nameof(DisposalProbe));
            }
        }

        private sealed class DisposableTenant : IAppTenant, IDisposable
        {
            public string TenantKey { get; set; } = "alpha";
            public DataBaseType DataBaseType { get; set; }
            public string ConnectionString { get; set; }

            public DisposalProbe Probe { get; } = new DisposalProbe();

            public void Dispose() => Probe.Dispose();
        }
    }
}
