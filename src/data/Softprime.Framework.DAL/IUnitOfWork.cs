using System;
using Softprime.Framework.DAL.Entity;
using Softprime.Framework.DAL.Repository;

namespace Softprime.Framework.DAL
{
    public interface IUnitOfWork : IDisposable
    {
        void SaveChanges();

        IRepository<T> Repository<T>() where T : class, IEntity;
    }
}