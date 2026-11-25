using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Softprime.Framework.DAL;
using Softprime.Framework.Domain.DAL;
using Xunit;
using Xunit.Abstractions;
#if EFCORE3
using Softprime.Framework.EF;
#else
using Softprime.Framework.EFCore5;
#endif

namespace Softprime.Framework.Data.Tests
{
    public sealed class RepositoryTests : IDisposable
    {
        private readonly SqliteConnection connection = new SqliteConnection("Data Source=:memory:");
        private readonly DbContextOptions<TestContext> options;
        private readonly TestContext context;
        private readonly IUnitOfWork unit;

        public RepositoryTests(ITestOutputHelper output)
        {
            var core = GetProductVersion(typeof(DbContext));
            var sqlite = GetProductVersion(typeof(SqliteDbContextOptionsBuilderExtensions));
            output.WriteLine($"CLR {Environment.Version}; EF Core {core}; EF Core SQLite {sqlite}");
#if EFCORE3
            const string expectedPatch = "3.1.11";
#else
            const string expectedPatch = "5.0.17";
#endif
            core.Split('+')[0].Should().Be(expectedPatch);
            sqlite.Split('+')[0].Should().Be(expectedPatch);

            connection.Open();
            output.WriteLine($"SQLite engine {connection.ServerVersion}");
            options = new DbContextOptionsBuilder<TestContext>().UseSqlite(connection).Options;
            context = new TestContext(options);
            context.Database.EnsureCreated();
            unit = new TestUnitOfWork(context);
        }

#if EFCORE3 && NET10_0
        [Fact]
        public void Find_Net10_ThrowsDuringLegacyQueryTranslation()
        {
            var repository = unit.Repository<Item>();
            var item = new Item { Name = "stored" };
            repository.Save(item);
            unit.SaveChanges();

            Action query = () => repository.Find(row => row.Name == "stored", out _);

            item.Id.Should().BeGreaterThan(0);
            repository.Get(item.Id).Should().BeSameAs(item);
            var error = query.Should().Throw<TypeInitializationException>();
            error.Which.TypeName.Should().Be("Microsoft.EntityFrameworkCore.EnumerableMethods");
            error.WithInnerException<InvalidOperationException>();
        }
#else
        [Fact]
        public void Save_UpdateAndDelete_TrackChangesUntilSaveChanges()
        {
            var repository = unit.Repository<Item>();
            var item = new Item { Name = "original" };

            repository.Save(item).Should().BeSameAs(item);

            unit.Repository<Item>().Should().BeSameAs(repository);
            context.Entry(item).State.Should().Be(EntityState.Added);
            using (var beforeSave = new TestContext(options))
                beforeSave.Set<Item>().Count().Should().Be(0);

            unit.SaveChanges();
            item.Id.Should().BeGreaterThan(0);
            repository.Get(item.Id).Should().BeSameAs(item);
            repository.Load(item.Id).Should().BeSameAs(item);
            repository.Get(row => row.Name == "original").Should().BeSameAs(item);

            item.Name = "updated";
            repository.Update(item);
            using (var beforeUpdate = new TestContext(options))
                beforeUpdate.Set<Item>().Find(item.Id).Name.Should().Be("original");
            unit.SaveChanges();

            using (var afterUpdate = new TestContext(options))
                afterUpdate.Set<Item>().Find(item.Id).Name.Should().Be("updated");

            repository.Delete(row => row.Id == item.Id);
            context.Entry(item).State.Should().Be(EntityState.Deleted);
            unit.SaveChanges();

            repository.Get(item.Id).Should().BeNull();
            repository.Load(item.Id).Should().BeNull();
            item.HookCalls.Should().Be(0);
        }

        [Fact]
        public async Task AsyncOperations_MutateTheTrackedEntityAndReturnMissingKeysAsNull()
        {
            var repository = unit.Repository<Item>();
            var item = new Item { Name = "async" };

            (await repository.SaveAsync(item)).Should().BeSameAs(item);
            context.Entry(item).State.Should().Be(EntityState.Added);
            unit.SaveChanges();
            (await repository.GetAsync(item.Id)).Should().BeSameAs(item);
            (await repository.LoadAsync(item.Id)).Should().BeSameAs(item);
            (await repository.GetAsync(row => row.Name == "async")).Should().BeSameAs(item);

            item.Name = "updated";
            await repository.UpdateAsync(item);
            unit.SaveChanges();
            using (var reader = new TestContext(options))
                reader.Set<Item>().Find(item.Id).Name.Should().Be("updated");

            await repository.DeleteAsync(item);
            unit.SaveChanges();

            (await repository.GetAsync(item.Id)).Should().BeNull();
            (await repository.LoadAsync(item.Id)).Should().BeNull();
            item.HookCalls.Should().Be(0);
        }
#endif

        [Fact]
        public async Task SaveOrUpdateAndMerge_ChooseStatesForTransientAndPersistedEntities()
        {
            var repository = unit.Repository<Item>();
            var sync = new Item { Name = "sync" };
            var async = new Item { Name = "async" };

            repository.SaveOrUpdate(sync).Should().BeSameAs(sync);
            (await repository.SaveOrUpdateAsync(async)).Should().BeSameAs(async);
            context.Entry(sync).State.Should().Be(EntityState.Added);
            context.Entry(async).State.Should().Be(EntityState.Added);
            unit.SaveChanges();

            repository.SaveOrUpdate(sync).Should().BeSameAs(sync);
            (await repository.SaveOrUpdateAsync(async)).Should().BeSameAs(async);
            context.Entry(sync).State.Should().Be(EntityState.Modified);
            context.Entry(async).State.Should().Be(EntityState.Modified);

            repository.Merge(sync).Should().BeSameAs(sync);
            (await repository.MergeAsync(async)).Should().BeSameAs(async);
            context.Entry(sync).State.Should().Be(EntityState.Modified);
            context.Entry(async).State.Should().Be(EntityState.Modified);
        }

#if !(EFCORE3 && NET10_0)
        [Theory]
        [InlineData(0, 2, 2)]
        [InlineData(1, 2, 1)]
        [InlineData(2, 2, 0)]
        [InlineData(0, 0, 0)]
        [InlineData(-1, 2, 2)]
        [InlineData(0, -1, 3)]
        public void Find_FilteredPage_TotalCountsRowsOnThatPage(int index, int size, int expected)
        {
            var repository = unit.Repository<Item>();
            foreach (var name in new[] { "include-a", "include-b", "include-c", "exclude" })
                repository.Save(new Item { Name = name });
            unit.SaveChanges();

            var page = repository.Find(item => item.Name.StartsWith("include"), out var total, index, size).ToList();

            total.Should().Be(expected);
            page.Should().HaveCount(expected);
            page.Should().NotContain(item => !item.Name.StartsWith("include"));
        }
#endif

        [Fact]
        public async Task GetAndLoad_NullKey_ReturnNullWithoutTranslatingQuery()
        {
            var repository = unit.Repository<Item>();
            object key = null;

            repository.Get(key).Should().BeNull();
            repository.Load(key).Should().BeNull();
            (await repository.GetAsync(key)).Should().BeNull();
            (await repository.LoadAsync(key)).Should().BeNull();
        }

        [Fact]
        public async Task Save_NullEntity_RejectsItWithoutAddingATrackedEntry()
        {
            var repository = unit.Repository<Item>();

            Action save = () => repository.Save(null);
            Func<Task> saveAsync = () => repository.SaveAsync(null);

            save.Should().ThrowExactly<ArgumentNullException>().WithParameterName("entity");
            (await saveAsync.Should().ThrowExactlyAsync<ArgumentNullException>()).WithParameterName("entity");
            context.ChangeTracker.Entries().Should().BeEmpty();
        }

        [Fact]
        public void Save_DuplicateIdentity_RejectsSecondInstanceInSameContext()
        {
            var repository = unit.Repository<Item>();
            var first = new Item { Name = "first" };
            var duplicate = new Item { Name = "second" };
            first.SetId(9);
            duplicate.SetId(9);
            repository.Save(first);

            Action save = () => repository.Save(duplicate);

            save.Should().ThrowExactly<InvalidOperationException>();
            repository.Get(9).Should().BeSameAs(first);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task Delete_BeforeInitialSave_DetachesEntityWithoutInsertingIt(bool asynchronous)
        {
            var repository = unit.Repository<Item>();
            var item = new Item { Name = "discard" };
            repository.Save(item);

            if (asynchronous)
                await repository.DeleteAsync(item);
            else
                repository.Delete(item);
            unit.SaveChanges();

            context.Entry(item).State.Should().Be(EntityState.Detached);
            using var count = connection.CreateCommand();
            count.CommandText = "SELECT COUNT(*) FROM Items";
            Convert.ToInt32(count.ExecuteScalar()).Should().Be(0);
        }

        [Fact]
        public void Repository_SeparateUnits_TrackTheirOwnInstances()
        {
            var item = new Item { Name = "one" };
            item.SetId(15);
            unit.Repository<Item>().Save(item);
            using var otherContext = new TestContext(options);
            using var otherUnit = new TestUnitOfWork(otherContext);
            var otherItem = new Item { Name = "two" };
            otherItem.SetId(item.Id);

            otherUnit.Repository<Item>().Save(otherItem);

            unit.Repository<Item>().Get(item.Id).Should().BeSameAs(item);
            otherUnit.Repository<Item>().Get(item.Id).Should().BeSameAs(otherItem);
        }

        [Fact]
        public void Find_NullFilter_ThrowsArgumentNullException()
        {
            var repository = unit.Repository<Item>();

            Action find = () => repository.Find(null);
            Action findPage = () => repository.Find(null, out _);

            find.Should().Throw<ArgumentNullException>().WithParameterName("predicate");
            findPage.Should().Throw<ArgumentNullException>().WithParameterName("predicate");
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void SaveChanges_InsideTransaction_FollowsCommitOrRollback(bool commit)
        {
            var repository = unit.Repository<Item>();
            var item = new Item { Name = "transaction" };

            using (var transaction = context.Database.BeginTransaction())
            {
                repository.Save(item);
                unit.SaveChanges();
                using var count = connection.CreateCommand();
                count.Transaction = (SqliteTransaction)transaction.GetDbTransaction();
                count.CommandText = "SELECT COUNT(*) FROM Items";
                Convert.ToInt32(count.ExecuteScalar()).Should().Be(1);
                if (commit)
                    transaction.Commit();
                else
                    transaction.Rollback();
            }

            using var persisted = connection.CreateCommand();
            persisted.CommandText = "SELECT COUNT(*) FROM Items";
            Convert.ToInt32(persisted.ExecuteScalar()).Should().Be(commit ? 1 : 0);
        }

        [Fact]
        public void Dispose_UnitOfWork_DisposesItsContext()
        {
            unit.Dispose();

            Action query = () => context.Set<Item>().Count();

            query.Should().Throw<ObjectDisposedException>();
        }

        public void Dispose()
        {
            unit.Dispose();
            connection.Dispose();
        }

        private static string GetProductVersion(Type type)
        {
            return type.Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion;
        }

        private sealed class TestUnitOfWork : EFUnitOfWork<TestContext>
        {
            public TestUnitOfWork(TestContext instance) : base(instance)
            {
            }
        }

        private sealed class TestContext : DbContext
        {
            public TestContext(DbContextOptions<TestContext> options) : base(options)
            {
            }

            protected override void OnModelCreating(ModelBuilder modelBuilder)
            {
                modelBuilder.Entity<Item>().ToTable("Items").HasKey(item => item.Id);
            }
        }

        public sealed class Item : EntityWithTypedId<int>
        {
            public string Name { get; set; }
            public int HookCalls { get; private set; }

            public override void BeforeSave() => HookCalls++;
            public override void AfterSave() => HookCalls++;
            public override void BeforeDelete() => HookCalls++;
            public override void AfterDelete() => HookCalls++;
        }
    }
}
