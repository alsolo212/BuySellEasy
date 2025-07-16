using Application.Interfaces;
using Application.Services;
using Domain.Entities;
using Microsoft.AspNetCore.Mvc;

namespace BSE.Controllers
{
    public class ProductController : Controller
    {
        private readonly IProductService _productService;

        public ProductController(IProductService productService)
        {
            _productService = productService;
        }


        [Route("products")]
        public IActionResult Products()
        {
            List<Product> product = _productService.GetProducts();
            return View(product);
        }

        [Route("details")]
        public IActionResult Details()
        {
            List<Product> product = new List<Product>()
            {
                new Product() { ProductId = 1, ProductName = "T-shirt", ProductPrice = 123 },
                new Product() { ProductId = 2, ProductName = "Boots", ProductPrice = 234 },
                new Product() { ProductId = 3, ProductName = "Watch", ProductPrice = 345 }
            };
            return View("Products", product);
        }
    }
}
