using NHibernate;
using NHibernate.Cfg;
using NHibernate.Connection;
using NHibernate.Dialect;
using NHibernate.Driver;
using NHibernate.Mapping.ByCode;
using Softprime.Framework.Domain.DAL;
using Softprime.Framework.Tests.LegacyProviders;

namespace Softprime.Framework.NH.Tests
{
    internal static class TestMappings
    {
        public static ISessionFactory BuildFactory(string schema, string table, RelationalDatabase database = null)
        {
            var configuration = new Configuration();
            configuration.SetProperty("hbm2ddl.keywords", "none");

            if (database?.Provider == Provider.Oracle)
            {
                configuration.SetProperty("dialect", typeof(Oracle12cDialect).AssemblyQualifiedName);
                configuration.SetProperty("connection.driver_class", typeof(OracleManagedDataClientDriver).AssemblyQualifiedName);
            }
            else
            {
                configuration.SetProperty("dialect", typeof(MsSql2012Dialect).AssemblyQualifiedName);
                configuration.SetProperty("connection.driver_class", typeof(SqlClientDriver).AssemblyQualifiedName);
            }

            if (database == null)
                configuration.SetProperty("connection.provider", typeof(UserSuppliedConnectionProvider).AssemblyQualifiedName);
            else
                configuration.SetProperty("connection.connection_string", database.ConnectionString);

            var mapper = new ModelMapper();
            mapper.Class<TestItem>(item =>
            {
                item.Schema(schema);
                item.Table(table);
                item.Id(row => row.Id, id =>
                {
                    id.Column("ID");
                    id.Generator(Generators.Assigned);
                });
                item.Property(row => row.Label, label =>
                {
                    label.Column("LABEL");
                    label.Length(100);
                    label.NotNullable(true);
                });
            });
            configuration.AddMapping(mapper.CompileMappingForAllExplicitlyAddedEntities());

            return configuration.BuildSessionFactory();
        }
    }

    public class TestItem : EntityWithTypedId<int>
    {
        public TestItem()
        {
        }

        public TestItem(int id, string label)
        {
            SetId(id);
            Label = label;
        }

        public virtual string Label { get; set; }
        public virtual int HookCalls { get; protected set; }

        public override void BeforeSave() => HookCalls++;
        public override void AfterSave() => HookCalls++;
        public override void BeforeDelete() => HookCalls++;
        public override void AfterDelete() => HookCalls++;
    }
}
