using Infrastructure.DbContextt;
using Api.Extensions;
using Api.Hubs;
using Api.Middleware;
using Microsoft.EntityFrameworkCore;

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
app.UseCors(ApiServiceCollectionExtensions.FrontendCorsPolicyName);
app.UseMiddleware<ApiExceptionMiddleware>();
app.UseAuthentication();
app.UseMiddleware<BlockedUserMiddleware>();
app.UseAuthorization();
app.UseStaticFiles();
app.UseLegacyUploads();
app.MapControllers();
app.MapHub<ChatHub>("/hubs/chat");

app.Run();
