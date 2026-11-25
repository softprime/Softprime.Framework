using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;

namespace Softprime.Framework.Api.Tests
{
    internal static class LoopbackHost
    {
        internal static async Task<WebApplication> StartAsync(
            Action<WebApplication> configure, Action<WebApplicationBuilder> configureBuilder = null)
        {
            var builder = WebApplication.CreateSlimBuilder();
            builder.Logging.ClearProviders();
            builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0));
            configureBuilder?.Invoke(builder);
            var app = builder.Build();

            try
            {
                configure(app);
                await app.StartAsync();
                return app;
            }
            catch
            {
                await app.DisposeAsync();
                throw;
            }
        }

        internal static Uri Address(WebApplication app) => new Uri(app.Urls.Single());
    }
}
