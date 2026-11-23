using System;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Softprime.Framework.DAL.Entity;
using Softprime.Framework.DAL.Repository;

namespace Softprime.Framework.EFCore
{
    /// <summary>
    /// Implementa o repositório da DAL com o contexto do Entity Framework Core fornecido.
    /// </summary>
    public sealed class EFRepository<TEntity> : IRepository<TEntity> where TEntity : class, IEntity
    {
        private readonly DbContext _context;

        /// <summary>
        /// Cria um repositório que usa o contexto fornecido durante a unidade de trabalho.
        /// </summary>
        public EFRepository(DbContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            _context = context;
        }

        private DbSet<TEntity> Entities => _context.Set<TEntity>();

        public IQueryable<TEntity> All() => Entities;

        public IQueryable<TEntity> Find(Expression<Func<TEntity, bool>> predicate) =>
            Entities.Where(predicate);

        /// <summary>
        /// Filtra e pagina os resultados. O total corresponde à quantidade de itens da página.
        /// </summary>
        public IQueryable<TEntity> Find(Expression<Func<TEntity, bool>> filter, out int total, int index = 0, int size = 50)
        {
            var page = Find(filter).Skip(index * size).Take(size);
            total = page.Count();
            return page;
        }

        // As buscas retornam null quando não há resultado. A assinatura legada da DAL não expressa isso.
#pragma warning disable CS8766
        public TEntity? Get(Expression<Func<TEntity, bool>> predicate) =>
            Entities.SingleOrDefault(predicate);

        public TEntity? Get(object key) => Entities.Find(key);

        public TEntity? Load(object key) => Get(key);
#pragma warning restore CS8766

        public void Delete(TEntity entity) => Entities.Remove(entity);

        public void Delete(Expression<Func<TEntity, bool>> predicate) =>
            Entities.RemoveRange(Find(predicate));

        public TEntity Save(TEntity entity) => Entities.Add(entity).Entity;

        public TEntity SaveOrUpdate(TEntity entity) =>
            entity.IsTransient() ? Save(entity) : Merge(entity);

        public void Update(TEntity entity) => Entities.Update(entity);

        public TEntity Merge(TEntity entity) => Entities.Update(entity).Entity;

#pragma warning disable CS8613
        public Task<TEntity?> GetAsync(Expression<Func<TEntity, bool>> predicate) =>
            Entities.SingleOrDefaultAsync(predicate);

        public Task<TEntity?> GetAsync(object key) => Entities.FindAsync(key).AsTask();

        public Task<TEntity?> LoadAsync(object key) => GetAsync(key);
#pragma warning restore CS8613

        public Task DeleteAsync(TEntity entity)
        {
            Delete(entity);
            return Task.CompletedTask;
        }

        public async Task DeleteAsync(Expression<Func<TEntity, bool>> predicate)
        {
            var entities = await Find(predicate).ToListAsync().ConfigureAwait(false);
            Entities.RemoveRange(entities);
        }

        public async Task<TEntity> SaveAsync(TEntity entity) =>
            (await Entities.AddAsync(entity).ConfigureAwait(false)).Entity;

        public Task<TEntity> SaveOrUpdateAsync(TEntity entity) =>
            entity.IsTransient() ? SaveAsync(entity) : MergeAsync(entity);

        public Task UpdateAsync(TEntity entity)
        {
            Update(entity);
            return Task.CompletedTask;
        }

        public Task<TEntity> MergeAsync(TEntity entity) =>
            Task.FromResult(Merge(entity));

        /// <summary>
        /// O ciclo de vida do contexto é gerenciado pela unidade de trabalho.
        /// </summary>
        public void Dispose()
        {
        }
    }
}
