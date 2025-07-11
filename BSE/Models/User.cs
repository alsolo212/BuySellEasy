using System.ComponentModel.DataAnnotations;

namespace BSE.Models
{
    public class User
    {
        public string? UserName { get; set; }

        [EmailAddress(ErrorMessage = "{0} should be a proper email address")]
        [Required(ErrorMessage = "{0} cant be blank")]
        public string? Email { get; set; }

        [Phone(ErrorMessage = "{0} should contain 10 digits")]
        public string? Phone { get; set; }

        [Required(ErrorMessage = "{0} cant be blank")]
        public string? Password { get; set; }

        [Required(ErrorMessage = "{0} cant be blank")]
        [Compare("Password", ErrorMessage = "{0} and {1} do not match")]
        [Display(Name = "Re-enter password")]
        public string? ConfirmPassword { get; set; }


    }
}
