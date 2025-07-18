using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Domain.Entities
{
    public class User
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required(ErrorMessage = "Username is required")]
        public string? UserName { get; set; }

        [EmailAddress(ErrorMessage = "Invalid email format")]
        [Required(ErrorMessage = "Email is required")]
        public string? Email { get; set; }

        [Phone(ErrorMessage = "Phone must contain 10 digits")]
        public string? Phone { get; set; }

        [Required(ErrorMessage = "Password is required")]
        public string? Password { get; set; }

        [NotMapped] // Не храним в базе
        [Compare("Password", ErrorMessage = "Passwords do not match")]
        public string? ConfirmPassword { get; set; }
    }
}
