using Optiq.Embedded.Models;

namespace Optiq.Embedded.Interfaces
{
    public interface IOptiqSet<TEntity> where TEntity : class
    {
        void Upsert(string id, TEntity entity);
        TEntity? Find(string id);
        void Remove(string id);
        OptiqQueryResult<TEntity> Search(string? keyword, OptiqQuery query, int skip, int take);
    }
}
