using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using NHibernate;
using NHibernate.Proxy;
using Softprime.Framework.Tests.LegacyProviders;
using Xunit.Abstractions;

namespace Softprime.Framework.NH.Tests
{
    public sealed class RelationalRepositoryTests
    {
        private readonly ITestOutputHelper output;

        public RelationalRepositoryTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        [DatabaseFact(Provider.SqlServer)]
        public Task SqlServer_Crud_UsesSessionStateAndKeepsHooksUncalled() => Crud(Provider.SqlServer);

        [DatabaseFact(Provider.Oracle)]
        public Task Oracle_Crud_UsesSessionStateAndKeepsHooksUncalled() => Crud(Provider.Oracle);

        [DatabaseFact(Provider.SqlServer)]
        public void SqlServer_GetAndLoad_DistinguishMissingKeysAndLazyProxies() => GetAndLoad(Provider.SqlServer);

        [DatabaseFact(Provider.Oracle)]
        public void Oracle_GetAndLoad_DistinguishMissingKeysAndLazyProxies() => GetAndLoad(Provider.Oracle);

        [DatabaseFact(Provider.SqlServer)]
        public void SqlServer_Pagination_CountsEachPage() => Pagination(Provider.SqlServer);

        [DatabaseFact(Provider.Oracle)]
        public void Oracle_Pagination_CountsEachPage() => Pagination(Provider.Oracle);

        [DatabaseFact(Provider.SqlServer)]
        public void SqlServer_Transactions_DistinguishCommitFromRollback() => Transactions(Provider.SqlServer);

        [DatabaseFact(Provider.Oracle)]
        public void Oracle_Transactions_DistinguishCommitFromRollback() => Transactions(Provider.Oracle);

        private async Task Crud(Provider provider)
        {
            using (var database = OpenDatabase(provider))
            using (var factory = BuildFactory(database))
            using (var session = factory.OpenSession())
            using (var repository = new NHRepository<TestItem>(session))
            {
                var first = new TestItem(1, "initial");
                var second = new TestItem(2, "async");

                using (var transaction = session.BeginTransaction())
                {
                    repository.Save(first).Should().BeNull();
                    (await repository.SaveAsync(second)).Should().BeNull();
                    session.Contains(first).Should().BeTrue();
                    session.Contains(second).Should().BeTrue();
                    transaction.Commit();
                }

                repository.Get(1).Should().BeSameAs(first);
                (await repository.GetAsync(row => row.Id == 2)).Should().BeSameAs(second);
                session.Clear();

                using (var transaction = session.BeginTransaction())
                {
                    first.Label = "updated";
                    repository.Update(first);
                    var merged = await repository.MergeAsync(new TestItem(2, "merged"));
                    merged.Should().NotBeSameAs(second);
                    merged.Label.Should().Be("merged");
                    var third = new TestItem(3, "added");
                    (await repository.SaveOrUpdateAsync(third)).Should().BeSameAs(third);
                    transaction.Commit();
                }

                session.Clear();
                repository.Get(row => row.Id == 1).Label.Should().Be("updated");
                (await repository.GetAsync(2)).Label.Should().Be("merged");
                repository.Get(3).Label.Should().Be("added");

                using (var transaction = session.BeginTransaction())
                {
                    await repository.DeleteAsync(row => row.Id == 2);
                    repository.Delete(row => row.Id == 1);
                    transaction.Commit();
                }

                repository.All().Select(row => row.Id).Should().Equal(3);
                first.HookCalls.Should().Be(0);
                second.HookCalls.Should().Be(0);
            }
        }

        private void GetAndLoad(Provider provider)
        {
            using (var database = OpenDatabase(provider))
            using (var factory = BuildFactory(database))
            {
                using (var session = factory.OpenSession())
                using (var repository = new NHRepository<TestItem>(session))
                using (var transaction = session.BeginTransaction())
                {
                    repository.Save(new TestItem(1, "present"));
                    transaction.Commit();
                }

                using (var session = factory.OpenSession())
                using (var repository = new NHRepository<TestItem>(session))
                {
                    repository.Get(999).Should().BeNull();
                    repository.Get(1).Label.Should().Be("present");
                }

                using (var session = factory.OpenSession())
                using (var repository = new NHRepository<TestItem>(session))
                {
                    var existing = repository.Load(1);
                    var missing = repository.Load(999);

                    existing.Should().BeAssignableTo<INHibernateProxy>();
                    missing.Should().BeAssignableTo<INHibernateProxy>();
                    NHibernateUtil.IsInitialized(existing).Should().BeFalse();
                    NHibernateUtil.IsInitialized(missing).Should().BeFalse();
                    missing.Id.Should().Be(999);
                    existing.Label.Should().Be("present");
                    NHibernateUtil.IsInitialized(existing).Should().BeTrue();
                    Action initializeMissing = () => { var label = missing.Label; };
                    initializeMissing.Should().ThrowExactly<ObjectNotFoundException>();
                }
            }
        }

        private void Pagination(Provider provider)
        {
            using (var database = OpenDatabase(provider))
            using (var factory = BuildFactory(database))
            using (var session = factory.OpenSession())
            using (var repository = new NHRepository<TestItem>(session))
            {
                using (var transaction = session.BeginTransaction())
                {
                    repository.Save(new TestItem(1, "match"));
                    repository.Save(new TestItem(2, "match"));
                    repository.Save(new TestItem(3, "match"));
                    repository.Save(new TestItem(4, "other"));
                    transaction.Commit();
                }

                var first = repository.Find(null, out var firstCount, size: 2).ToList();
                var filtered = repository.Find(row => row.Label == "match", out var filteredCount, size: 9).ToList();
                var partial = repository.Find(row => row.Label == "match", out var partialCount, index: 1, size: 2).ToList();
                var empty = repository.Find(row => row.Label == "match", out var emptyCount, index: 2, size: 2).ToList();

                first.Should().HaveCount(2);
                firstCount.Should().Be(2);
                filtered.Select(row => row.Id).Should().BeEquivalentTo(new[] { 1, 2, 3 });
                filteredCount.Should().Be(3);
                partial.Should().ContainSingle().Which.Label.Should().Be("match");
                partialCount.Should().Be(1);
                empty.Should().BeEmpty();
                emptyCount.Should().Be(0);
            }
        }

        private void Transactions(Provider provider)
        {
            using (var database = OpenDatabase(provider))
            using (var factory = BuildFactory(database))
            {
                using (var session = factory.OpenSession())
                using (var repository = new NHRepository<TestItem>(session))
                using (var transaction = session.BeginTransaction())
                {
                    repository.Save(new TestItem(1, "committed"));
                    transaction.Commit();
                }

                using (var session = factory.OpenSession())
                using (var repository = new NHRepository<TestItem>(session))
                using (var transaction = session.BeginTransaction())
                {
                    repository.Save(new TestItem(2, "rolled back"));
                    session.Flush();
                    repository.Get(2).Should().NotBeNull();
                    transaction.Rollback();
                }

                using (var session = factory.OpenSession())
                using (var repository = new NHRepository<TestItem>(session))
                {
                    repository.Get(1).Label.Should().Be("committed");
                    repository.Get(2).Should().BeNull();
                }
            }
        }

        private RelationalDatabase OpenDatabase(Provider provider)
        {
            output.WriteLine($"NHibernate {FileVersionInfo.GetVersionInfo(typeof(ISession).Assembly.Location).ProductVersion}");
            return new RelationalDatabase(provider, output);
        }

        private static ISessionFactory BuildFactory(RelationalDatabase database)
        {
            return TestMappings.BuildFactory(database.Schema, database.Table, database);
        }
    }
}
