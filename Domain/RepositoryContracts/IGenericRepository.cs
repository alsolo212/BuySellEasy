namespace Domain.RepositoryContracts
{
    public interface IGenericRepository<T>
    {
        public Task<Boolean> IsObjectIdExists(Guid id);
        public Task<IEnumerable<T>?> GetAllValuesAsync();
        public Task<T?> GetValueByIdAsync(Guid id);
        public Task AddAsync(T entity);
        public void DeleteElement(T entity);
        public void UpdateObject(T entity);
        public Task SaveAsync();
    }
}
