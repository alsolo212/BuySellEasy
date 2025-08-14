using Application.DTO.FiltersDto;
using Application.ServiceContracts;
using Microsoft.AspNetCore.Mvc;
using UI.Helpers;

namespace BSE.Controllers
{
    public class HomeController : Controller
    {
        private readonly IProductService _productService;
        private readonly IProductImageService _productImageService;
        private readonly ICategoriesService _categoriesService;

        public HomeController(IProductService productService, IProductImageService productImageService, ICategoriesService categoriesService)
        {
            _productService = productService;
            _productImageService = productImageService;
            _categoriesService = categoriesService;
        }

        [Route("/")]
        [Route("home")]
        public async Task<IActionResult> Index([FromQuery] ProductFilterDto filters)
        {
            var model = new HomeViewModel
            {
                Categories = await _categoriesService.GetCategories(),
                Products = await _productService.GetProducts(filters)
            };

            return View(model);
        }

        [Route("categories")]
        public IActionResult Categories()
        {
            return View();
        }

        [Route("cart")]
        public IActionResult Cart()
        {
            return View();
        }
    }
}