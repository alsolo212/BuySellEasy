using Domain.IdentityEntities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.DbContextt
{
    public static class IdentitySeed
    {
        public static readonly Guid SuperAdminRoleId = Guid.Parse("00000000-0000-0000-0000-000000000003");
        public static readonly Guid SuperAdminUserId = Guid.Parse("00000000-0000-0000-0000-000000000010");

        public const string SuperAdmin = "SuperAdmin";
        public const string Admin = "Admin";
        public const string User = "User";
        public static async Task SeedAsync(IServiceProvider serviceProvider)
        {
            var roleManager = serviceProvider.GetRequiredService<RoleManager<Role>>();
            var userManager = serviceProvider.GetRequiredService<UserManager<User>>();
            var configuration = serviceProvider.GetRequiredService<IConfiguration>();

            var rolesList = new List<Role>() {
                new Role()
                {
                    Name = SuperAdmin,
                    NormalizedName = SuperAdmin.ToUpper(),
                    Id = SuperAdminRoleId
                },
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

            var superAdmin = await userManager.FindByEmailAsync("superadmin@gmail.com");
            if (superAdmin is null)
            {
                var superAdminPassword = configuration["Seed:SuperAdminPassword"];
                if (string.IsNullOrWhiteSpace(superAdminPassword))
                {
                    throw new InvalidOperationException(
                        "Seed:SuperAdminPassword is not configured. Set it with .NET User Secrets or the SEED__SUPERADMINPASSWORD environment variable.");
                }

                superAdmin = new User
                {
                    Id = SuperAdminUserId,
                    UserName = "SuperAdmin",
                    Email = "superadmin@gmail.com",
                    CreatedAt = DateTime.UtcNow,
                    EmailConfirmed = true,
                    IsVerified = true
                };

                var createResult = await userManager.CreateAsync(superAdmin, superAdminPassword);
                if (!createResult.Succeeded)
                {
                    var errors = string.Join("; ", createResult.Errors.Select(error => error.Description));
                    throw new InvalidOperationException($"Unable to seed super admin user. {errors}");
                }
            }

            if (!await userManager.IsInRoleAsync(superAdmin, SuperAdmin))
            {
                await userManager.AddToRoleAsync(superAdmin, SuperAdmin);
            }

            if (!await userManager.IsInRoleAsync(superAdmin, Admin))
            {
                await userManager.AddToRoleAsync(superAdmin, Admin);
            }
        }
    }
}
