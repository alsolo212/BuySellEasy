using Microsoft.AspNetCore.Mvc;
using BSE.Models;

namespace BSE.Controllers
{
    public class ProductController : Controller
    {
        [Route("products")]
        public IActionResult Products()
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
