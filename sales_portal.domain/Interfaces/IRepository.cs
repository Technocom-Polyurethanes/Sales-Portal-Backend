using sales_portal.domain.Models;

namespace sales_portal.domain.Interfaces;

public interface IRepository<T> where T : BaseEntity
{
    IQueryable<T> GetAll();
    Task<T?> GetByIdAsync(Guid id);
    Task AddAsync(T entity);
    Task AddRangeAsync(IEnumerable<T> entities);
    void Update(T entity);
    void Remove(T entity);
    void RemoveRange(IEnumerable<T> entities);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    Task<IRepositoryTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);
    void ClearTracking();
}
