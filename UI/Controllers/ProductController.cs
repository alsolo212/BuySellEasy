using Application.DTO.FiltersDto;
using Application.ServiceContracts;
using Domain.Entities;
using Microsoft.AspNetCore.Mvc;
using UI.Filters;

namespace BSE.Controllers
{
    public class ProductController : Controller
    {
        private readonly IProductService _productService;
        private readonly IProductImageService _productImageService;

        public ProductController(IProductService productService, IProductImageService productImageService)
        {
            _productService = productService;
            _productImageService = productImageService;
        }

        [Route("products")]
        public async Task<IActionResult> Products([FromQuery] ProductFilterDto filters)
        {
            var products = await _productService.GetProducts(filters);
            return View(products);
        }

        [HttpGet]
        [Route("product/details/{id}")]
        public async Task<IActionResult> Details(Guid id)
        {
            var product = await _productService.GetProductById(id);
            if (product == null)
                return NotFound();

            return View(product);
        }

        [HttpGet]
        [Route("product/create")]
        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [Route("product/create")]
        public async Task<IActionResult> Create(Product product, List<IFormFile> photos)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.CreateError = "Invalid input.";
                return View(product);
            }

            product.CreatedAt = DateTime.Now;

            // Сначала добавим продукт, чтобы получить его Id
            await _productService.AddProduct(product);

            // Обработка загрузки фотографий
            if (photos != null && photos.Count > 0)
            {
                string uploadPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "product-photos");

                if (!Directory.Exists(uploadPath))
                    Directory.CreateDirectory(uploadPath);

                foreach (var photo in photos.Take(40))
                {
                    if (photo.Length > 0)
                    {
                        var fileName = Guid.NewGuid() + Path.GetExtension(photo.FileName);
                        var filePath = Path.Combine(uploadPath, fileName);

                        using (var stream = new FileStream(filePath, FileMode.Create))
                        {
                            await photo.CopyToAsync(stream);
                        }

                        var image = new ProductImage
                        {
                            Id = Guid.NewGuid(),
                            ProductId = product.Id, // Здесь используем product.Id, а не id
                            ImagePath = $"/uploads/product-photos/{fileName}"
                        };

                        await _productImageService.AddProductImage(image);
                    }
                }
            }

            return RedirectToAction("Products");
        }



        [HttpGet]
        [Route("product/edit/{id}")]
        public async Task<IActionResult> Edit(Guid id)
        {
            var product = await _productService.GetProductById(id);
            if (product == null)
                return NotFound();

            return View(product);
        }

        [HttpPost]
        [Route("product/edit/{id}")]
        public async Task<IActionResult> Edit(Guid id, Product product, IFormFileCollection photos, string deleteImages)
        {
            // Валидация только основных полей продукта
            if (string.IsNullOrEmpty(product.Title) ||
                string.IsNullOrEmpty(product.Description) ||
                product.ProductPrice <= 0)
            {
                ViewBag.EditError = "Please fill all required fields correctly";
                return View(product);
            }

            product.Id = id;

            // Удаляем отмеченные изображения
            if (!string.IsNullOrEmpty(deleteImages))
            {
                var imageIds = deleteImages.Split(',')
                    .Where(x => !string.IsNullOrEmpty(x))
                    .Select(Guid.Parse)
                    .ToList();

                foreach (var imageId in imageIds)
                {
                    await _productImageService.DeleteProductImage(imageId);
                }
            }

            // Добавляем новые фото
            if (photos != null && photos.Count > 0)
            {
                string uploadPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "product-photos");

                if (!Directory.Exists(uploadPath))
                    Directory.CreateDirectory(uploadPath);

                foreach (var photo in photos.Take(40))
                {
                    if (photo.Length > 0)
                    {
                        var fileName = Guid.NewGuid() + Path.GetExtension(photo.FileName);
                        var filePath = Path.Combine(uploadPath, fileName);

                        using (var stream = new FileStream(filePath, FileMode.Create))
                        {
                            await photo.CopyToAsync(stream);
                        }

                        var image = new ProductImage
                        {
                            Id = Guid.NewGuid(),
                            ProductId = product.Id,
                            ImagePath = $"/uploads/product-photos/{fileName}"
                        };

                        await _productImageService.AddProductImage(image);
                    }
                }
            }

            await _productService.UpdateProduct(product);
            return RedirectToAction("Products");
        }

        [HttpPost("product/delete/{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            await _productService.DeleteProduct(id);
            return RedirectToAction("Products");
        }
    }
}