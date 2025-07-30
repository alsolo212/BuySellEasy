using Application.ServiceContracts;
using Domain.Entities;
using Microsoft.AspNetCore.Mvc;

namespace BSE.Controllers
{
    public class HomeController : Controller
    {
        private readonly ICategoriesService _categoriesService;

        public HomeController(ICategoriesService categoriesService)
        {
            _categoriesService = categoriesService;
        }

        [Route("/")]
        [Route("home")]
        public async Task<IActionResult> Index()
        {
            var categories = await _categoriesService.GetCategories();
            return View(categories);
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
