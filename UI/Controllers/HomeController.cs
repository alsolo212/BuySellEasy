using Application.DTO.FiltersDto;
using Application.ServiceContracts;
using Application.Services;
using Domain.IdentityEntities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using UI.Helpers;

namespace BSE.Controllers
{
    public class HomeController : Controller
    {
        private readonly IProductService _productService;
        private readonly IProductImageService _productImageService;
        private readonly ICategoriesService _categoriesService;
        private readonly UserManager<User> _userManager;
        private readonly ICartService _cartService;

        public HomeController(ICartService cartService, IProductService productService, IProductImageService productImageService, ICategoriesService categoriesService, UserManager<User> userManager)
        {
            _cartService = cartService;
            _userManager = userManager;
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

        private async Task<Guid> GetCurrentUserIdAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            return user!.Id;
        }

        [HttpPost]
        public async Task<IActionResult> AddToCart(Guid productId)
        {
            var userId = await GetCurrentUserIdAsync();
            var result = await _cartService.AddToCartAsync(userId, productId);

            if (!result)
                TempData["Error"] = "Невозможно добавить товар в корзину.";
            else
                TempData["Success"] = "Товар добавлен в корзину!";

            return RedirectToAction("Details", "Product", new { id = productId });
        }

        [HttpPost]
        public async Task<IActionResult> RemoveFromCart(Guid cartItemId)
        {
            var userId = await GetCurrentUserIdAsync();
            var result = await _cartService.RemoveFromCartAsync(cartItemId, userId);

            if (!result)
                TempData["Error"] = "Ошибка при удалении товара из корзины.";
            else
                TempData["Success"] = "Товар удалён из корзины.";

            return RedirectToAction("Cart");
        }

        [HttpGet]
        [Route("cart")]
        public async Task<IActionResult> Cart()
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                var userId = await GetCurrentUserIdAsync();
                var cartItems = await _cartService.GetUserCartAsync(userId);
                return View(cartItems);
            }
            else
            {
                return RedirectToAction("Auth", "Account");
            }
        }
    }
}