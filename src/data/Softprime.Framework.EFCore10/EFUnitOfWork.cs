using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Softprime.Framework.DAL;
using Softprime.Framework.DAL.Entity;
using Softprime.Framework.DAL.Repository;

namespace Softprime.Framework.EFCore
{
    /// <summary>
    /// Agrupa repositórios e alterações em um contexto do Entity Framework Core.
    /// </summary>
    public sealed class EFUnitOfWork<TContext> : IUnitOfWork where TContext : DbContext
    {
        private readonly Dictionary<Type, object> _repositories = new Dictionary<Type, object>();
        private bool _disposed;

        /// <summary>
        /// Cria uma unidade de trabalho que gerencia o ciclo de vida do contexto fornecido.
        /// </summary>
        public EFUnitOfWork(TContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            Context = context;
        }

        /// <summary>
        /// Contexto compartilhado pelos repositórios da unidade de trabalho.
        /// </summary>
        public TContext Context { get; }

        public void SaveChanges() => Context.SaveChanges();

        /// <summary>
        /// Persiste as alterações pendentes e retorna o número de entradas gravadas.
        /// </summary>
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
            Context.SaveChangesAsync(cancellationToken);

        public IRepository<TEntity> Repository<TEntity>() where TEntity : class, IEntity
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (!_repositories.TryGetValue(typeof(TEntity), out var repository))
            {
                repository = new EFRepository<TEntity>(Context);
                _repositories.Add(typeof(TEntity), repository);
            }

            return (IRepository<TEntity>)repository;
        }

        /// <summary>
        /// Libera o contexto e os repositórios da unidade de trabalho.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _repositories.Clear();
            Context.Dispose();
        }
    }
}
