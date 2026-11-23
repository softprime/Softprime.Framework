using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Softprime.Framework.DAL.Entity;
using Softprime.Framework.EFCore;
using Xunit;

namespace Softprime.Framework.EFCore10.Tests
{
    public sealed class EFCoreTests : IDisposable
    {
        private readonly SqliteConnection _connection = new SqliteConnection("Data Source=:memory:");
        private readonly EFUnitOfWork<TestContext> _unitOfWork;

        public EFCoreTests()
        {
            _connection.Open();
            var options = new DbContextOptionsBuilder<TestContext>().UseSqlite(_connection).Options;
            var context = new TestContext(options);
            context.Database.EnsureCreated();
            _unitOfWork = new EFUnitOfWork<TestContext>(context);
        }

        [Fact]
        public async Task Repository_ChangedEntities_CreatesReadsUpdatesAndDeletes()
        {
            var repository = _unitOfWork.Repository<TestEntity>();
            var entity = new TestEntity { Name = "Original" };

            await repository.SaveOrUpdateAsync(entity);
            var inserts = await _unitOfWork.SaveChangesAsync();
            _unitOfWork.Context.ChangeTracker.Clear();
            var saved = await repository.GetAsync(entity.Id);
            saved.Should().NotBeNull();
            saved!.Name = "Atualizado";
            await repository.UpdateAsync(saved);
            await _unitOfWork.SaveChangesAsync();
            _unitOfWork.Context.ChangeTracker.Clear();
            var updated = repository.Get(item => item.Id == entity.Id);
            await repository.DeleteAsync(item => item.Id == entity.Id);
            var deletes = await _unitOfWork.SaveChangesAsync();

            inserts.Should().Be(1);
            entity.Id.Should().BeGreaterThan(0);
            updated!.Name.Should().Be("Atualizado");
            deletes.Should().Be(1);
            repository.Get(entity.Id).Should().BeNull();
            _unitOfWork.Repository<TestEntity>().Should().BeSameAs(repository);
        }

        [Fact]
        public void Find_PartialPage_ReportsItemCountForThatPage()
        {
            var repository = _unitOfWork.Repository<TestEntity>();
            for (var item = 0; item < 5; item++)
                repository.Save(new TestEntity { Name = "Incluído" });
            repository.Save(new TestEntity { Name = "Excluído" });
            _unitOfWork.SaveChanges();

            var page = repository.Find(item => item.Name == "Incluído", out var total, 2, 2).ToList();
            var emptyPage = repository.Find(item => item.Name == "Incluído", out var emptyTotal, 3, 2).ToList();

            page.Should().ContainSingle().Which.Name.Should().Be("Incluído");
            total.Should().Be(1);
            emptyPage.Should().BeEmpty();
            emptyTotal.Should().Be(0);
        }

        [Fact]
        public async Task SaveChanges_CommitAndRollback_PersistsOnlyCommittedChanges()
        {
            var repository = _unitOfWork.Repository<TestEntity>();

            await using (var transaction = await _unitOfWork.Context.Database.BeginTransactionAsync())
            {
                repository.Save(new TestEntity { Name = "Confirmado" });
                _unitOfWork.SaveChanges();
                await transaction.CommitAsync();
            }

            await using (var transaction = await _unitOfWork.Context.Database.BeginTransactionAsync())
            {
                repository.Save(new TestEntity { Name = "Revertido" });
                await _unitOfWork.SaveChangesAsync();
                await transaction.RollbackAsync();
            }

            _unitOfWork.Context.ChangeTracker.Clear();
            var names = await repository.All().Select(item => item.Name).ToListAsync();
            names.Should().Equal("Confirmado");
        }

        public void Dispose()
        {
            _unitOfWork.Dispose();
            _connection.Dispose();
        }

        private sealed class TestContext(DbContextOptions<TestContext> options) : DbContext(options)
        {
            protected override void OnModelCreating(ModelBuilder modelBuilder)
            {
                modelBuilder.Entity<TestEntity>().HasKey(entity => entity.Id);
            }
        }

        private sealed class TestEntity : IEntity<int>
        {
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;

            public bool IsTransient() => Id == 0;

            public IEnumerable<PropertyInfo> GetSignatureProperties() => Array.Empty<PropertyInfo>();
        }
    }
}
