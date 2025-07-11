using Microsoft.AspNetCore.Mvc;
using BSE.Models;

namespace BSE.Controllers
{
    public class HomeController : Controller
    {
        [Route("/")]
        [Route("home")]
        public IActionResult Index()
        {
            ViewBag.ListTitle = "Categories";
            ViewBag.ListCategories = new List<string>()
            {
                "All categories",
                "Clothes",
                "Cars",
                "Job"
            };
            return View();
        }

        [Route("categories")]
        public IActionResult Categories()
        {
            return View();
        }
    }
}
