using Application.DTO.FiltersDto;
using Application.ServiceContracts;
using Domain.Entities;
using Domain.RepositoryContracts;

namespace Application.Services
{
    public class ProductsService : IProductService
    {
        private readonly IProductRepository _repository;

        public ProductsService(IProductRepository repository)
        {
            _repository = repository;
        }

        public async Task<List<Product>> GetProducts(ProductFilterDto filter)
        {
            var products = await _repository.GetAllWithImagesAsync();
            if (products == null)
                return new List<Product>();

            var query = products.AsQueryable();

            // Поиск
            if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
                query = query.Where(p => p.Title != null &&
                                         p.Title.ToLower().Contains(filter.SearchTerm.ToLower()));

            // Фильтрация
            if (filter.CategoryId.HasValue)
                query = query.Where(p => p.CategoryId == filter.CategoryId);

            if (!string.IsNullOrEmpty(filter.Condition))
                query = query.Where(p => p.ProductCondition == filter.Condition);

            if (filter.MinPrice.HasValue)
                query = query.Where(p => p.ProductPrice >= filter.MinPrice.Value);

            if (filter.MaxPrice.HasValue)
                query = query.Where(p => p.ProductPrice <= filter.MaxPrice.Value);

            // Сортировка
            switch (filter.SortBy.ToLower())
            {
                case "newest":
                    query = query.OrderByDescending(p => p.CreatedAt);
                    break;
                case "cheapest":
                    query = query.OrderBy(p => p.ProductPrice);
                    break;
                case "expensive":
                    query = query.OrderByDescending(p => p.ProductPrice);
                    break;
                default: // recommended
                    query = query.OrderBy(p => p.Title); // или без сортировки
                    break;
            }

            return query.ToList();
        }

        public async Task<Product?> GetProductById(Guid id)
        {
            var product = await _repository.GetProductWithImagesAsync(id);
            if (product != null && product.Images != null)
            {
                product.Images = product.Images
                    .Where(img => File.Exists(GetImagePhysicalPath(img.ImagePath)))
                    .OrderBy(img => img.SortOrder) // <- Сортировка здесь!
                    .ToList();
            }
            return product;
        }

        public async Task AddProduct(Product product)
        {
            if (product.Id == Guid.Empty)
                product.Id = Guid.NewGuid();

            product.CreatedAt ??= DateTime.Now;

            await _repository.AddAsync(product);
            await _repository.SaveAsync();
        }

        public async Task UpdateProduct(Product product)
        {
            var existing = await _repository.GetProductWithImagesAsync(product.Id);
            if (existing != null)
            {
                existing.Title = product.Title;
                existing.CategoryId = product.CategoryId;
                existing.Description = product.Description;
                existing.ProductPrice = product.ProductPrice;
                existing.Location = product.Location;
                existing.ProductCondition = product.ProductCondition;

                _repository.UpdateObject(existing);
                await _repository.SaveAsync();
            }
        }

        public async Task DeleteProduct(Guid id)
        {
            var product = await _repository.GetProductWithImagesAsync(id);
            if (product != null)
            {
                if (product.Images != null)
                {
                    foreach (var image in product.Images)
                    {
                        var filePath = GetImagePhysicalPath(image.ImagePath);
                        if (File.Exists(filePath))
                        {
                            File.Delete(filePath);
                        }
                    }
                }

                _repository.DeleteElement(product);
                await _repository.SaveAsync();
            }
        }

        private string GetImagePhysicalPath(string imagePath)
        {
            return Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", imagePath.TrimStart('/'));
        }
    }
}