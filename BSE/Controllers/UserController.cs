using Application.Interfaces;
using Domain.Entities;
using Microsoft.AspNetCore.Mvc;
using ServiceContracts.Interfaces;
using Services.Services;

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

            /*if (!ModelState.IsValid)
            {
                string errors = string.Join("\n", ModelState.Values.SelectMany(value => value.Errors).Select(err => err.ErrorMessage));

                return BadRequest(errors);
            }*/
            //return Content($"{user}");
        }
    }
}