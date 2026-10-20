using Infrastructure.DbContextt;
using Api.Extensions;
using Api.Hubs;
using Api.Middleware;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApiServices(builder.Configuration);

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    if (app.Environment.IsDevelopment())
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<ProductDbContext>();
        await dbContext.Database.MigrateAsync();
    }

    await IdentitySeed.SeedAsync(scope.ServiceProvider);
}

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
app.UseRouting();
app.UseCors(ApiServiceCollectionExtensions.FrontendCorsPolicyName);
app.UseMiddleware<ApiExceptionMiddleware>();
app.UseAuthentication();
app.UseRateLimiter();
app.UseMiddleware<BlockedUserMiddleware>();
app.UseAuthorization();

// Only listing images are public user uploads. Other uploaded files are served
// through authenticated controller endpoints with ownership checks.
var listingImagesPath = Path.Combine(
    app.Environment.WebRootPath ?? Path.Combine(app.Environment.ContentRootPath, "wwwroot"),
    "uploads",
    "listings");

Directory.CreateDirectory(listingImagesPath);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(listingImagesPath),
    RequestPath = "/uploads/listings",
    OnPrepareResponse = context =>
        context.Context.Response.Headers["X-Content-Type-Options"] = "nosniff"
});

// Keep existing category image URLs working after removing the MVC host.
var categoryImagesPath = Path.Combine(
    app.Environment.WebRootPath ?? Path.Combine(app.Environment.ContentRootPath, "wwwroot"),
    "category-photos");

if (Directory.Exists(categoryImagesPath))
{
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(categoryImagesPath),
        RequestPath = "/uploads/category-photos"
    });
}

app.MapControllers();
app.MapHub<ChatHub>("/hubs/chat");
app.MapHub<OrderHub>("/hubs/orders");

app.Run();
