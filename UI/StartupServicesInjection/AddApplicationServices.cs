using Application.ServiceContracts;
using Application.Services;
using Domain.IdentityEntities;
using Domain.RepositoryContracts;
using Infrastructure.DbContextt;
using Infrastructure.Repository;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace UI.StartupServicesInjection
{
    public static class StartupServicesInjection
    {
        public static IServiceCollection AddApplicationServices(this IServiceCollection services, IConfiguration configuration)
        {
            // Identity
            services.AddIdentity<User, Role>(options =>
            {
                options.Password.RequireDigit = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequiredLength = 3; // упрощаем для теста
            })
            .AddEntityFrameworkStores<ProductDbContext>()
            .AddDefaultTokenProviders()
            .AddUserStore<UserStore<User, Role, ProductDbContext, Guid>>()
            .AddRoleStore<RoleStore<Role, ProductDbContext, Guid>>();

            // Cookie
            services.ConfigureApplicationCookie(options =>
            {
                options.LoginPath = "/Account/Auth";
                options.AccessDeniedPath = "/Account/Auth";
                options.ExpireTimeSpan = TimeSpan.FromDays(7);
            });

            // БД
            services.AddDbContext<ProductDbContext>(options =>
            {
                options.UseSqlServer(configuration.GetConnectionString("DefaultConnection"));
            });

            // Репозитории и сервисы
            services.AddScoped<ICategoriesService, CategoriesService>();
            services.AddScoped<ICategoriesRepository, CategoriesRepository>();
            services.AddScoped<IProductService, ProductsService>();
            services.AddScoped<IProductRepository, ProductRepository>();
            services.AddScoped<IProductImageRepository, ProductImageRepository>();
            services.AddScoped<IProductImageService, ProductImagesService>();

            // HttpContext и Session
            services.AddHttpContextAccessor();
            services.AddSession();

            return services;
        }
    }
}
