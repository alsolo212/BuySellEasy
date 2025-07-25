using Microsoft.AspNetCore.Mvc;
using Application.ServiceContracts;

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
            List<string> categories = _categoriesService.GetCategories();
            return View(categories);
        }

        [Route("categories")]
        public IActionResult Categories()
        {
            return View();
        }
    }
}
