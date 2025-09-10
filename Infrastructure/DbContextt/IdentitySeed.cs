using Domain.IdentityEntities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.DbContextt
{
    public static class IdentitySeed
    {
        public const string Admin = "Admin";
        public const string User = "User";
        public static async Task SeedAsync(IServiceProvider serviceProvider)
        {
            var roleManager = serviceProvider.GetRequiredService<RoleManager<Role>>();

            var rolesList = new List<Role>() {
                new Role()
                {
                    Name = Admin,
                    NormalizedName = Admin.ToUpper(),
                    Id = Guid.Parse("00000000-0000-0000-0000-000000000001")
                },
                new Role()
                {
                    Name = User,
                    NormalizedName = User.ToUpper(),
                    Id = Guid.Parse("00000000-0000-0000-0000-000000000002")
                }
            };

            foreach (var role in rolesList) {
                var existingRole = await roleManager.FindByNameAsync(role.Name!);
                if (existingRole == null) { 
                    await roleManager.CreateAsync(role);
                }
            }
        }
    }
}
