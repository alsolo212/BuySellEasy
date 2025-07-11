using Microsoft.AspNetCore.Mvc;
using BSE.Models;

namespace BSE.Controllers
{
    public class UserController : Controller
    {
        [Route("users")]
        public IActionResult Users()
        {
            List<User> user = new List<User>()
            {
                new User() { UserName = "Johns", Email = "qwe@gmail.com", Phone = "0991234567", Password = "123", ConfirmPassword = "123" },
                new User() { UserName = "Linda", Email = "rty@gmail.com", Phone = "0933215476", Password = "234", ConfirmPassword = "234" },
                new User() { UserName = "Susan", Email = "uio@gmail.com", Phone = null, Password = "345", ConfirmPassword = "345" }
            };

            if (!ModelState.IsValid)
            {
                string errors = string.Join("\n", ModelState.Values.SelectMany(value => value.Errors).Select(err => err.ErrorMessage));

                return BadRequest(errors);
            }
            //return Content($"{user}");

            return View("Users", user);
        }
    }
}