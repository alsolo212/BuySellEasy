using Application.ServiceContracts;
using Application.Services;
using Domain.RepositoryContracts;
using Infrastructure.Lists;
using Services.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllersWithViews();
builder.Services.AddScoped<ICategoriesService, CategoriesService>();
builder.Services.AddScoped<ICategoriesRepository, CategoryList>();

builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IUserRepository, UserList>();

builder.Services.AddScoped<IProductService, ProductsService>();
builder.Services.AddScoped<IProductRepository, ProductList>();
builder.Services.AddSession();
builder.Services.AddHttpContextAccessor();

var app = builder.Build();

app.UseSession();
app.UseStaticFiles();
app.UseRouting();
app.MapControllers();

app.Run();