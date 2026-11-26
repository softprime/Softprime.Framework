using System;
using System.Data.Entity;
using System.Data.Entity.Core.Common;
using System.Data.Entity.Infrastructure;
using System.Data.Entity.Infrastructure.DependencyResolution;
using System.Data.SqlClient;
using FluentAssertions;
using Softprime.Framework.DAL.Repository;
using Softprime.Framework.Tests.LegacyProviders;
using Xunit;

namespace Softprime.Framework.EF6.Tests
{
    public sealed class RepositoryTests
    {
        [Fact]
        public void AsyncMethods_ThroughDal_ThrowBeforeReturningATask()
        {
            IRepository<TestItem> repository = new EFRepository<TestItem>(null);
            var item = new TestItem(1, "value");

            Action[] calls =
            {
                () => repository.GetAsync(1),
                () => repository.GetAsync(row => row.Id == 1),
                () => repository.LoadAsync(1),
                () => repository.SaveAsync(item),
                () => repository.SaveOrUpdateAsync(item),
                () => repository.UpdateAsync(item),
                () => repository.MergeAsync(item),
                () => repository.DeleteAsync(item),
                () => repository.DeleteAsync(row => row.Id == 1)
            };

            foreach (var call in calls)
                call.Should().ThrowExactly<NotImplementedException>();
        }

        [Fact]
        public void AsyncMethods_ThroughConcreteRepository_ThrowBeforeReturningATask()
        {
            var repository = new EFRepository<TestItem>(null);
            var item = new TestItem(1, "value");
            Action[] calls =
            {
                () => repository.AllAsync(),
                () => repository.FindAsync(row => row.Id == 1),
                () => repository.FindAsync(null, out _, index: 0, size: 2),
                () => repository.UpdateAsync(item),
                () => repository.DeleteAsync(item),
                () => repository.DeleteAsync(row => row.Id == 1)
            };

            foreach (var call in calls)
                call.Should().ThrowExactly<NotImplementedException>();
        }

        [Fact]
        public void Dispose_RepositoryAndUnitOfWork_HaveDifferentContextOwnership()
        {
            using (var unit = new OfflineUnitOfWork())
            {
                var repository = unit.Repository<TestItem>();
                unit.Repository<TestItem>().Should().BeSameAs(repository);

                repository.Dispose();
                unit.DbContext.Database.Connection.Should().BeOfType<SqlConnection>();
                unit.Dispose();

                Action accessConnection = () => { var connection = unit.DbContext.Database.Connection; };
                accessConnection.Should().ThrowExactly<InvalidOperationException>();
            }
        }

        [Fact]
        public void OracleProvider_ConfiguredInTestAssembly_ResolvesLegacyProviderServices()
        {
            var provider = DbConfiguration.DependencyResolver.GetService<DbProviderServices>("Oracle.ManagedDataAccess.Client");

            provider.Should().NotBeNull();
            provider.GetType().Assembly.GetName().Name.Should().Be("Oracle.ManagedDataAccess.EntityFramework");
        }

        public sealed class OfflineContext : DbContext
        {
            public OfflineContext()
                : base(new SqlConnection(@"Data Source=(localdb)\MSSQLLocalDB;Integrated Security=true"), true)
            {
            }
        }

        private sealed class OfflineUnitOfWork : EFUnitOfWork<OfflineContext>
        {
        }
    }
}
