using Microsoft.EntityFrameworkCore;
using sales_portal.domain.Interfaces;
using sales_portal.domain.Models;
using sales_portal.infrastructure.Contexts;

namespace sales_portal.infrastructure;

public class Repository<T> : IRepository<T> where T : BaseEntity
{
    private readonly AppDbContext _dbContext;
    private readonly DbSet<T> _dbSet;

    public Repository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
        _dbSet = dbContext.Set<T>();
    }

    public IQueryable<T> GetAll() => _dbSet.AsQueryable();

    public Task<T?> GetByIdAsync(Guid id)
        => _dbSet.FirstOrDefaultAsync(entity => entity.Id == id);

    public Task AddAsync(T entity)
    {
        _dbSet.Add(entity);
        return Task.CompletedTask;
    }

    public Task AddRangeAsync(IEnumerable<T> entities)
    {
        _dbSet.AddRange(entities);
        return Task.CompletedTask;
    }

    public void Update(T entity) => _dbSet.Update(entity);

    public void Remove(T entity) => _dbSet.Remove(entity);

    public void RemoveRange(IEnumerable<T> entities) => _dbSet.RemoveRange(entities);

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => _dbContext.SaveChangesAsync(cancellationToken);

    public async Task<IRepositoryTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        return new RepositoryTransaction(transaction);
    }

    public void ClearTracking() => _dbContext.ChangeTracker.Clear();
}
