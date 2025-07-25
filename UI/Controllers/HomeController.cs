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
        public IActionResult Index()
        {
            var categories = _categoriesService.GetCategories();
            return View(categories);
        }

        [Route("categories")]
        public IActionResult Categories()
        {
            return View();
        }
    }
}
