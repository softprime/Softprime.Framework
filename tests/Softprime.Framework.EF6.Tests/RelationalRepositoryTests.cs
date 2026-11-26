using System;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Diagnostics;
using System.Linq;
using FluentAssertions;
using Softprime.Framework.Domain.DAL;
using Softprime.Framework.Tests.LegacyProviders;
using Xunit.Abstractions;

namespace Softprime.Framework.EF6.Tests
{
    public sealed class RelationalRepositoryTests
    {
        private readonly ITestOutputHelper output;

        public RelationalRepositoryTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        [DatabaseFact(Provider.SqlServer)]
        public void SqlServer_Crud_TracksChangesAndKeepsHooksUncalled() => Crud(Provider.SqlServer);

        [DatabaseFact(Provider.Oracle)]
        public void Oracle_Crud_TracksChangesAndKeepsHooksUncalled() => Crud(Provider.Oracle);

        [DatabaseFact(Provider.SqlServer)]
        public void SqlServer_Pagination_CountsFirstPageAndRejectsUnorderedSkip() => Pagination(Provider.SqlServer);

        [DatabaseFact(Provider.Oracle)]
        public void Oracle_Pagination_CountsFirstPageAndRejectsUnorderedSkip() => Pagination(Provider.Oracle);

        [DatabaseFact(Provider.SqlServer)]
        public void SqlServer_Transactions_DistinguishCommitFromRollback() => Transactions(Provider.SqlServer);

        [DatabaseFact(Provider.Oracle)]
        public void Oracle_Transactions_DistinguishCommitFromRollback() => Transactions(Provider.Oracle);

        private void Crud(Provider provider)
        {
            using (var database = OpenDatabase(provider))
            using (var unit = TestUnitOfWork.Open(database))
            {
                var repository = unit.Repository<TestItem>();
                var item = new TestItem(1, "initial");

                repository.Save(item).Should().BeSameAs(item);
                unit.DbContext.Entry(item).State.Should().Be(EntityState.Added);
                repository.All().Count().Should().Be(0);
                unit.SaveChanges();

                repository.Get(1).Should().BeSameAs(item);
                repository.Load(1).Should().BeSameAs(item);
                repository.Get(999).Should().BeNull();
                repository.Load(999).Should().BeNull();
                Action predicateGet = () => repository.Get(row => row.Id == 1);
                predicateGet.Should().ThrowExactly<ArgumentException>();

                item.Label = "updated";
                repository.Update(item);
                using (var reader = TestUnitOfWork.Open(database))
                    reader.Repository<TestItem>().Get(1).Label.Should().Be("initial");
                unit.SaveChanges();

                item.Label = "saved or updated";
                repository.SaveOrUpdate(item).Should().BeSameAs(item);
                unit.DbContext.Entry(item).State.Should().Be(EntityState.Modified);
                unit.SaveChanges();

                item.Label = "merged";
                repository.Merge(item).Should().BeSameAs(item);
                var transient = new TestItem(0, "inserted");
                repository.SaveOrUpdate(transient).Should().BeSameAs(transient);
                unit.DbContext.Entry(transient).State.Should().Be(EntityState.Added);
                unit.SaveChanges();

                using (var reader = TestUnitOfWork.Open(database))
                {
                    reader.Repository<TestItem>().Get(1).Label.Should().Be("merged");
                    reader.Repository<TestItem>().Get(0).Label.Should().Be("inserted");
                }

                repository.Delete(item);
                repository.Delete(row => row.Id == 0);
                unit.SaveChanges();

                repository.Load(1).Should().BeNull();
                repository.Get(0).Should().BeNull();
                item.HookCalls.Should().Be(0);
                transient.HookCalls.Should().Be(0);
            }
        }

        private void Pagination(Provider provider)
        {
            using (var database = OpenDatabase(provider))
            using (var unit = TestUnitOfWork.Open(database))
            {
                var repository = unit.Repository<TestItem>();
                repository.Save(new TestItem(1, "match"));
                repository.Save(new TestItem(2, "match"));
                repository.Save(new TestItem(3, "other"));
                unit.SaveChanges();

                var first = repository.Find(null, out var firstCount, size: 2).ToList();
                var filtered = repository.Find(row => row.Label == "match", out var filteredCount, size: 9).ToList();
                var empty = repository.Find(null, out var emptyCount, size: 0).ToList();
                Action nextPage = () => repository.Find(null, out _, index: 1, size: 2);

                first.Should().HaveCount(2);
                firstCount.Should().Be(2);
                filtered.Select(row => row.Id).Should().BeEquivalentTo(new[] { 1, 2 });
                filteredCount.Should().Be(2);
                empty.Should().BeEmpty();
                emptyCount.Should().Be(0);
                nextPage.Should().ThrowExactly<NotSupportedException>();
            }
        }

        private void Transactions(Provider provider)
        {
            using (var database = OpenDatabase(provider))
            {
                using (var unit = TestUnitOfWork.Open(database))
                using (var transaction = unit.DbContext.Database.BeginTransaction())
                {
                    unit.Repository<TestItem>().Save(new TestItem(1, "committed"));
                    unit.SaveChanges();
                    transaction.Commit();
                }

                using (var unit = TestUnitOfWork.Open(database))
                using (var transaction = unit.DbContext.Database.BeginTransaction())
                {
                    unit.Repository<TestItem>().Save(new TestItem(2, "rolled back"));
                    unit.SaveChanges();
                    transaction.Rollback();
                }

                using (var reader = TestUnitOfWork.Open(database))
                {
                    reader.Repository<TestItem>().Get(1).Label.Should().Be("committed");
                    reader.Repository<TestItem>().Get(2).Should().BeNull();
                }
            }
        }

        private RelationalDatabase OpenDatabase(Provider provider)
        {
            output.WriteLine($"EF {FileVersionInfo.GetVersionInfo(typeof(DbContext).Assembly.Location).ProductVersion}");
            return new RelationalDatabase(provider, output);
        }

        private sealed class TestUnitOfWork : EFUnitOfWork<TestContext>
        {
            public static TestUnitOfWork Open(RelationalDatabase database)
            {
                // EFUnitOfWork constructs T with new(); the fixture is needed only during construction.
                TestContext.DatabaseForConstruction = database;
                try
                {
                    return new TestUnitOfWork();
                }
                finally
                {
                    TestContext.DatabaseForConstruction = null;
                }
            }
        }

        private sealed class TestContext : DbContext, IDbModelCacheKeyProvider
        {
            [ThreadStatic]
            public static RelationalDatabase DatabaseForConstruction;

            private readonly RelationalDatabase database;

            static TestContext()
            {
                System.Data.Entity.Database.SetInitializer<TestContext>(null);
            }

            public TestContext()
                : base(DatabaseForConstruction.CreateConnection(), contextOwnsConnection: true)
            {
                database = DatabaseForConstruction;
                Configuration.ProxyCreationEnabled = false;
            }

            public string CacheKey => $"{database.Provider}/{database.Schema}/{database.Table}";

            protected override void OnModelCreating(DbModelBuilder builder)
            {
                var item = builder.Entity<TestItem>();
                item.ToTable(database.Table, database.Schema);
                item.HasKey(row => row.Id);
                item.Property(row => row.Id).HasColumnName("ID").HasDatabaseGeneratedOption(DatabaseGeneratedOption.None);
                item.Property(row => row.Label).HasColumnName("LABEL").HasMaxLength(100).IsRequired();
            }
        }
    }

    public sealed class TestItem : EntityWithTypedId<int>
    {
        public TestItem()
        {
        }

        public TestItem(int id, string label)
        {
            SetId(id);
            Label = label;
        }

        public string Label { get; set; }

        [NotMapped]
        public int HookCalls { get; private set; }

        public override void BeforeSave() => HookCalls++;
        public override void AfterSave() => HookCalls++;
        public override void BeforeDelete() => HookCalls++;
        public override void AfterDelete() => HookCalls++;
    }
}
