namespace Softprime.Framework.DAL
{
    public interface ITenant 
    {
        string TenantKey { get; }

        string ConnectionString { get; }

        string AssemblyMappingName { get; }
    }
}
