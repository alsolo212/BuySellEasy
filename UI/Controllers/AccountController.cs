using Application.DTO.FiltersDto;
using Application.DTO.IdentityDto;
using Application.ServiceContracts;
using Application.Services;
using Domain.IdentityEntities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UI.Helpers;

namespace UI.Controllers
{
    public class AccountController : Controller
    {
        private readonly UserManager<User> _userManager;
        private readonly SignInManager<User> _signInManager;
        private readonly IProductService _productService;
        private readonly ICategoriesService _categoriesService;

        public AccountController(IProductService productService, ICategoriesService categoriesService, UserManager<User> userManager, SignInManager<User> signInManager)
        {
            _productService = productService;
            _categoriesService = categoriesService;
            _userManager = userManager;
            _signInManager = signInManager;
        }

        [Route("auth")]
        [HttpGet]
        public IActionResult Auth()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(LoginDTO dto)
        {
            if (!ModelState.IsValid) return View("Auth");

            var user = await _userManager.FindByEmailAsync(dto.Email);
            if (user == null)
            {
                TempData["LoginError"] = "Пользователь с таким email не найден";
                return RedirectToAction("Auth");
            }

            var result = await _signInManager.PasswordSignInAsync(user, dto.Password, false, false);

            if (result.Succeeded)
                return RedirectToAction("Profile");

            TempData["LoginError"] = "Неправильный логин или пароль";
            return RedirectToAction("Auth");
        }



        [HttpPost]
        public async Task<IActionResult> Register(RegisterDTO dto)
        {
            if (ModelState.IsValid == false)
            {
                ViewBag.Errors = ModelState.Values.SelectMany(temp => temp.Errors).Select(temp => temp.ErrorMessage);
                return View("Auth", dto);
            }

            User user = new User()
            {
                UserName = dto.UserName,
                Email = dto.Email,
                PhoneNumber = dto.Phone
            };

            IdentityResult result = await _userManager.CreateAsync(user, dto.Password);

            if (result.Succeeded)
            {
                await _signInManager.SignInAsync(user, isPersistent: true);
                return RedirectToAction("Profile");
            }
            else
            {
                foreach (IdentityError error in result.Errors)
                {
                    ModelState.AddModelError("Auth", error.Description);
                }
            }

            return View("Auth", dto);
        }

        [Route("profile")]
        [HttpGet]
        public async Task<IActionResult> Profile([FromQuery] ProductFilterDto filters)
        {
            if (!User.Identity!.IsAuthenticated) return RedirectToAction("Auth");

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return RedirectToAction("Auth");

            filters.UserId = user.Id;

            var model = new HomeViewModel
            {
                Categories = await _categoriesService.GetCategories(),
                Products = await _productService.GetProducts(filters)
            };

            return View(model);
        }



        [HttpPost]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Auth");
        }
    }
}
