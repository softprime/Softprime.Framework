using System;
using System.Threading.Tasks;
using FluentAssertions;
using NHibernate;
using NHibernate.Proxy;
using Xunit;

namespace Softprime.Framework.NH.Tests
{
    public sealed class RepositoryTests
    {
        [Fact]
        public void Load_WithoutQuery_ReturnsAnUninitializedProxyWithTheRequestedKey()
        {
            using (var factory = TestMappings.BuildFactory("dbo", "SoftprimeItems"))
            using (var session = factory.OpenSession())
            using (var repository = new NHRepository<TestItem>(session))
            {
                var proxy = repository.Load(42);

                proxy.Should().BeAssignableTo<INHibernateProxy>();
                proxy.Id.Should().Be(42);
                NHibernateUtil.IsInitialized(proxy).Should().BeFalse();
            }
        }

        [Fact]
        public async Task SaveAndSaveAsync_AssignedIdentifiers_ReturnNullAndAttachTheEntities()
        {
            using (var factory = TestMappings.BuildFactory("dbo", "SoftprimeItems"))
            using (var session = factory.OpenSession())
            using (var repository = new NHRepository<TestItem>(session))
            {
                var item = new TestItem(1, "sync");
                var asyncItem = new TestItem(2, "async");

                var result = repository.Save(item);
                var asyncResult = await repository.SaveAsync(asyncItem);

                result.Should().BeNull();
                asyncResult.Should().BeNull();
                session.Contains(item).Should().BeTrue();
                session.Contains(asyncItem).Should().BeTrue();
                item.HookCalls.Should().Be(0);
                asyncItem.HookCalls.Should().Be(0);
            }
        }

        [Fact]
        public void Dispose_Repository_ClosesItsSession()
        {
            using (var factory = TestMappings.BuildFactory("dbo", "SoftprimeItems"))
            using (var session = factory.OpenSession())
            {
                var repository = new NHRepository<TestItem>(session);
                session.IsOpen.Should().BeTrue();

                repository.Dispose();
                repository.Dispose();

                session.IsOpen.Should().BeFalse();
            }
        }
    }
}
