namespace SamoBot.Infrastructure.Data;

public interface IRepository<T> where T : class
{
    Task<T?> GetById(int id, CancellationToken cancellationToken = default);
    Task<IEnumerable<T>> GetAll(CancellationToken cancellationToken = default);
    Task<int> Insert(T entity, CancellationToken cancellationToken = default);
    Task<bool> Update(T entity, CancellationToken cancellationToken = default);
    Task<bool> Delete(int id, CancellationToken cancellationToken = default);
}
