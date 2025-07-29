using Application.ServiceContracts;
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
        public async Task<IActionResult> Products()
        {
            var products = await _productService.GetProducts();
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
        public async Task<IActionResult> Create(Product product)
        {
            if (ModelState.IsValid)
            {
                product.CreatedAt = DateTime.Now;
                await _productService.AddProduct(product);
                return RedirectToAction("Products");
            }

            ViewBag.CreateError = "Invalid input. Please check your values.";
            return View(product);
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
        public async Task<IActionResult> Edit(Guid id, Product product)
        {
            if (ModelState.IsValid)
            {
                product.Id = id;
                await _productService.UpdateProduct(product);
                return RedirectToAction("Products");
            }

            ViewBag.EditError = "Invalid input.";
            return View(product);
        }

        [HttpPost("product/delete/{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            await _productService.DeleteProduct(id);
            return RedirectToAction("Products");
        }
    }
}
