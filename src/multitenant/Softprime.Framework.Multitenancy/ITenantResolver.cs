using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace Softprime.Framework.Multitenancy
{
    public interface ITenantResolver
    {
        Task<TenantContext> ResolveAsync(HttpContext context);
    }
}