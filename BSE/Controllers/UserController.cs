using Application.Interfaces;
using Domain.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;

namespace BSE.Controllers
{
    public class UserController : Controller
    {
        private readonly IUserService _userService;

        public UserController(IUserService userService)
        {
            _userService = userService;
        }

        [Route("users")]
        public IActionResult Users()
        {
            List<User> user = _userService.GetUsers();
            return View(user);
        }

        [Route("profile")]
        [HttpGet]
        public IActionResult Profile()
        {
            var email = HttpContext.Session.GetString("UserEmail");
            var username = HttpContext.Session.GetString("UserName");

            if (!string.IsNullOrEmpty(email) && !string.IsNullOrEmpty(username))
            {
                ViewBag.IsAuthenticated = true;
                ViewBag.UserName = username;
            }
            else
            {
                ViewBag.IsAuthenticated = false;
            }

            return View();
        }


        [Route("register")]
        [HttpGet]
        public IActionResult Register()
        {
            return View();
        }

        [Route("register")]
        [HttpPost]
        public IActionResult Register(User user)
        {
            if (ModelState.IsValid)
            {
                _userService.RegisterUser(user);

                HttpContext.Session.SetString("UserEmail", user.Email ?? "");
                HttpContext.Session.SetString("UserName", user.UserName ?? "");

                return RedirectToAction("Profile");
            }

            ViewBag.RegisterError = "Registration failed. Check your inputs.";
            return View("Profile");
        }

        [Route("login")]
        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }

        [Route("login")]
        [HttpPost]
        public IActionResult Login(string email, string password)
        {
            var user = _userService.Login(email, password);
            if (user != null)
            {
                HttpContext.Session.SetString("UserEmail", user.Email ?? "");
                HttpContext.Session.SetString("UserName", user.UserName ?? "");
                return RedirectToAction("Profile");
            }

            ViewBag.LoginError = "Invalid email or password.";
            return View("Profile");
        }

        [Route("logout")]
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Profile");
        }
    }
}