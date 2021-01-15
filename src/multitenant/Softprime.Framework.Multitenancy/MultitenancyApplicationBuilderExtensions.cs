using Softprime.Framework.Multitenancy.Internal;
using Microsoft.AspNetCore.Builder;

namespace Softprime.Framework.Multitenancy
{
    public static class MultitenancyApplicationBuilderExtensions
    {
        public static IApplicationBuilder UseMultitenancy(this IApplicationBuilder app)
        {
            return app.UseMiddleware<TenantResolutionMiddleware>();
        }
    }
}
