using System;
using Softprime.Framework.Multitenancy.Internal;
using Microsoft.AspNetCore.Builder;

namespace Softprime.Framework.Multitenancy
{
    public static class UsePerTenantApplicationBuilderExtensions
    {
        public static IApplicationBuilder UsePerTenant<TTenant>(this IApplicationBuilder app, Action<TenantPipelineBuilderContext<TTenant>, IApplicationBuilder> configuration)
            where TTenant : IAppTenant
        {
            app.Use(next => new TenantPipelineMiddleware<TTenant>(next, app, configuration).Invoke);
            return app;
        }
    }
}
