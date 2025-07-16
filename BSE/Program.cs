using Application.Interfaces;
using Application.Services;
using Domain.RepositoryContracts;
using Infrastructure.Lists;
using Microsoft.Extensions.DependencyInjection;
using ServiceContracts.Interfaces;
using Services.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllersWithViews();
builder.Services.AddScoped<ICategoriesService, CategoriesService>();
builder.Services.AddScoped<ICategoriesRepository, CategoryList>();

builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IUserRepository, UserList>();

builder.Services.AddScoped<IProductService, ProductsService>();
builder.Services.AddScoped<IProductRepository, ProductList>();

var app = builder.Build();

app.UseStaticFiles();
app.UseRouting();
app.MapControllers();

app.Run();