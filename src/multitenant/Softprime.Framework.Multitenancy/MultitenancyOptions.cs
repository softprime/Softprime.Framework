using System.Collections.ObjectModel;

namespace Softprime.Framework.Multitenancy
{
    public class MultitenancyOptions<TTenant> where TTenant : IAppTenant
     {
        public Collection<TTenant> Tenants { get; set; }
    }
}
