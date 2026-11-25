using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Softprime.Framework.Multitenancy.Internal;
using Xunit;

namespace Softprime.Framework.Multitenancy.Tests
{
    public class TenantHostTests
    {
        [Fact]
        public async Task UsePerTenant_RequestsForTwoTenants_BuildsAndReusesIsolatedBranches()
        {
            await using var host = await TenantHost.StartAsync();

            using var alpha = await host.RequestAsync("/orders", header: "alpha");
            using var beta = await host.RequestAsync("/orders?tenant=beta");
            using var alphaByHost = await host.RequestAsync("/orders", host: "alpha.example.test");
            using var unknown = await host.RequestAsync("/orders?tenant=unknown");

            alpha.StatusCode.Should().Be(HttpStatusCode.OK);
            beta.StatusCode.Should().Be(HttpStatusCode.OK);
            alphaByHost.StatusCode.Should().Be(HttpStatusCode.OK);
            ResponseHeader(alpha, "X-Tenant-Branch").Should().Be("alpha");
            ResponseHeader(beta, "X-Tenant-Branch").Should().Be("beta");
            ResponseHeader(alphaByHost, "X-Tenant-Branch").Should().Be("alpha");
            ResponseHeader(alpha, "X-Scoped-Tenant").Should().Be("alpha");
            ResponseHeader(beta, "X-Scoped-Tenant").Should().Be("beta");
            var alphaContextId = await alpha.Content.ReadAsStringAsync();
            Guid.TryParse(alphaContextId, out _).Should().BeTrue();
            (await alphaByHost.Content.ReadAsStringAsync()).Should().Be(alphaContextId);
            (await beta.Content.ReadAsStringAsync()).Should().NotBe(alphaContextId);
            host.BuildCounts["alpha"].Should().Be(1);
            host.BuildCounts["beta"].Should().Be(1);

            // UsePerTenant ends the pipeline without invoking the downstream delegate for unresolved requests.
            unknown.StatusCode.Should().Be(HttpStatusCode.OK);
            unknown.Headers.Contains("X-Scoped-Tenant").Should().BeFalse();
            (await unknown.Content.ReadAsStringAsync()).Should().BeEmpty();
        }

        [Fact]
        public async Task UsePerTenant_ConcurrentRequests_ReusesTheCorrectBranchAndContext()
        {
            await using var host = await TenantHost.StartAsync();
            using var firstWarm = await host.RequestAsync("/orders", header: "alpha");
            using var secondWarm = await host.RequestAsync("/orders?tenant=beta");

            var requests = await Task.WhenAll(
                host.RequestAsync("/orders", host: "alpha.example.test"),
                host.RequestAsync("/orders", header: "beta"));
            using var first = requests[0];
            using var second = requests[1];

            ResponseHeader(first, "X-Tenant-Branch").Should().Be("alpha");
            ResponseHeader(first, "X-Scoped-Tenant").Should().Be("alpha");
            ResponseHeader(second, "X-Tenant-Branch").Should().Be("beta");
            ResponseHeader(second, "X-Scoped-Tenant").Should().Be("beta");
            (await first.Content.ReadAsStringAsync()).Should().Be(await firstWarm.Content.ReadAsStringAsync());
            (await second.Content.ReadAsStringAsync()).Should().Be(await secondWarm.Content.ReadAsStringAsync());
            host.BuildCounts["alpha"].Should().Be(1);
            host.BuildCounts["beta"].Should().Be(1);
        }

        [Theory]
        [InlineData(false, HttpStatusCode.Found)]
        [InlineData(true, HttpStatusCode.MovedPermanently)]
        public async Task TenantUnresolvedRedirect_MissingOrUnknownTenant_UsesConfiguredStatusAndLocation(
            bool permanent, HttpStatusCode expectedStatus)
        {
            await using var host = await TenantHost.StartAsync(redirectUnresolved: permanent);

            using var missing = await host.RequestAsync("/orders");
            using var unknown = await host.RequestAsync("/orders?tenant=unknown");
            using var resolved = await host.RequestAsync("/orders", header: "beta");

            missing.StatusCode.Should().Be(expectedStatus);
            missing.Headers.Location.OriginalString.Should().Be("/select-tenant");
            unknown.StatusCode.Should().Be(expectedStatus);
            unknown.Headers.Location.OriginalString.Should().Be("/select-tenant");
            resolved.StatusCode.Should().Be(HttpStatusCode.OK);
            ResponseHeader(resolved, "X-Scoped-Tenant").Should().Be("beta");
        }

        [Theory]
        [InlineData(false, HttpStatusCode.Found)]
        [InlineData(true, HttpStatusCode.MovedPermanently)]
        public async Task PrimaryHostnameRedirect_Alias_PreservesPortPathAndQuery(
            bool permanent, HttpStatusCode expectedStatus)
        {
            await using var host = await TenantHost.StartAsync(redirectPrimaryHost: permanent);

            using var alias = await host.RequestAsync("/orders/open?filter=a%20b", host: "alpha.example.test:5123");
            using var primary = await host.RequestAsync("/orders/open", header: "alpha", host: "PRIMARY.EXAMPLE.TEST");
            using var tenantWithoutPrimary = await host.RequestAsync("/orders/open", host: "beta.example.test");

            alias.StatusCode.Should().Be(expectedStatus);
            alias.Headers.Location.AbsoluteUri.Should().Be("http://primary.example.test:5123/orders/open?filter=a%20b");
            primary.StatusCode.Should().Be(HttpStatusCode.OK);
            primary.Headers.Location.Should().BeNull();
            ResponseHeader(primary, "X-Scoped-Tenant").Should().Be("alpha");
            tenantWithoutPrimary.StatusCode.Should().Be(HttpStatusCode.OK);
            tenantWithoutPrimary.Headers.Location.Should().BeNull();
        }

        private static string ResponseHeader(HttpResponseMessage response, string name) =>
            response.Headers.GetValues(name).Single();

        private sealed class TenantHost : IAsyncDisposable
        {
            private readonly WebApplication app;
            private readonly HttpClient client;

            private TenantHost(WebApplication app, Uri address, ConcurrentDictionary<string, int> buildCounts)
            {
                this.app = app;
                client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, UseProxy = false })
                {
                    BaseAddress = address,
                    Timeout = TimeSpan.FromSeconds(10)
                };
                BuildCounts = buildCounts;
            }

            public ConcurrentDictionary<string, int> BuildCounts { get; }

            public static async Task<TenantHost> StartAsync(bool? redirectUnresolved = null, bool? redirectPrimaryHost = null)
            {
                var builder = WebApplication.CreateSlimBuilder();
                builder.Logging.ClearProviders();
                builder.WebHost.ConfigureKestrel(kestrel => kestrel.Listen(IPAddress.Loopback, 0));
                builder.Services.AddSingleton(ConfiguredTenantResolver.CreateOptions());
                builder.Services.AddMultitenancy<ConfiguredTenantResolver>();
                var app = builder.Build();
                var builds = new ConcurrentDictionary<string, int>();
                app.UseMultitenancy();

                if (redirectUnresolved.HasValue)
                    app.UseMiddleware<TenantUnresolvedRedirectMiddleware<TestTenant>>("/select-tenant", redirectUnresolved.Value);

                if (redirectPrimaryHost.HasValue)
                {
                    Func<TestTenant, string> hostname = tenant => tenant.PrimaryHostname;
                    app.UseMiddleware<PrimaryHostnameRedirectMiddleware<TestTenant>>(hostname, redirectPrimaryHost.Value);
                }

                app.UsePerTenant<TestTenant>((tenant, branch) =>
                {
                    builds.AddOrUpdate(tenant.Tenant.TenantKey, 1, (_, count) => count + 1);
                    branch.Use(next => context =>
                    {
                        context.Response.Headers["X-Tenant-Branch"] = tenant.Tenant.TenantKey;
                        return next(context);
                    });
                });

                app.Run(context =>
                {
                    var scopedTenant = context.RequestServices.GetRequiredService<ITenant<IAppTenant>>();
                    context.Response.Headers["X-Scoped-Tenant"] = scopedTenant.Value.TenantKey;
                    return context.Response.WriteAsync(context.GetTenantContext().Id);
                });

                try
                {
                    await app.StartAsync();
                    var server = app.Services.GetRequiredService<IServer>();
                    var address = server.Features.Get<IServerAddressesFeature>().Addresses.Single();
                    return new TenantHost(app, new Uri(address), builds);
                }
                catch
                {
                    await app.DisposeAsync();
                    throw;
                }
            }

            public async Task<HttpResponseMessage> RequestAsync(string path, string header = null, string host = null)
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, path);
                if (header != null)
                    request.Headers.Add(ConfiguredTenantResolver.TenantHeader, header);
                if (host != null)
                    request.Headers.Host = host;
                return await client.SendAsync(request);
            }

            public async ValueTask DisposeAsync()
            {
                client.Dispose();
                try
                {
                    await app.StopAsync();
                }
                finally
                {
                    await app.DisposeAsync();
                }
            }
        }
    }
}
