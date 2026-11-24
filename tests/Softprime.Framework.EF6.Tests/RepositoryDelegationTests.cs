using System;
using System.Data.Entity;
using System.Linq.Expressions;
using FluentAssertions;
using Moq;
using Softprime.Framework.DAL.Repository;
using Xunit;

namespace Softprime.Framework.EF6.Tests
{
    public sealed class RepositoryDelegationTests
    {
        [Fact]
        public void Save_NullEntity_ThrowsBeforeAccessingContext()
        {
            IRepository<TestItem> repository = new EFRepository<TestItem>(null);

            Action save = () => repository.Save(null);

            save.Should().ThrowExactly<ArgumentNullException>().WithParameterName("entity");
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void GetOrLoad_Key_ForwardsSingleKeyAndReturnsSetResult(bool load)
        {
            var item = new TestItem(8, "sample");
            var set = new Mock<DbSet<TestItem>>(MockBehavior.Strict);
            set.Setup(items => items.Find(8)).Returns(item);
            using (var context = ContextFor(set))
            using (IRepository<TestItem> repository = new EFRepository<TestItem>(context))
            {
                var result = load ? repository.Load(8) : repository.Get(8);

                result.Should().BeSameAs(item);
                set.Verify(items => items.Find(8), Times.Once);
            }
        }

        [Fact]
        public void Get_Predicate_ForwardsExpressionAsTheFindKey()
        {
            Expression<Func<TestItem, bool>> predicate = row => row.Id == 8;
            var set = new Mock<DbSet<TestItem>>(MockBehavior.Strict);
            set.Setup(items => items.Find(predicate)).Returns((TestItem)null);
            using (var context = ContextFor(set))
            using (IRepository<TestItem> repository = new EFRepository<TestItem>(context))
            {
                var result = repository.Get(predicate);

                result.Should().BeNull();
                set.Verify(items => items.Find(predicate), Times.Once);
            }
        }

        [Fact]
        public void SaveAndDelete_Entities_ForwardToTheSetWithoutCallingHooks()
        {
            var item = new TestItem(0, "sample");
            var set = new Mock<DbSet<TestItem>>(MockBehavior.Strict);
            set.Setup(items => items.Add(item)).Returns(item);
            set.Setup(items => items.Remove(item)).Returns(item);
            using (var context = ContextFor(set))
            using (IRepository<TestItem> repository = new EFRepository<TestItem>(context))
            {
                var result = repository.Save(item);
                repository.Delete(item);

                result.Should().BeSameAs(item);
                item.HookCalls.Should().Be(0);
                set.Verify(items => items.Add(item), Times.Once);
                set.Verify(items => items.Remove(item), Times.Once);
            }
        }

        private static DbContext ContextFor(Mock<DbSet<TestItem>> set)
        {
            var context = new Mock<DbContext>();
            context.Setup(db => db.Set<TestItem>()).Returns(set.Object);
            return context.Object;
        }
    }
}
