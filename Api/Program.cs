using Infrastructure.DbContextt;
using Api.Extensions;
using Api.Hubs;
using Api.Middleware;
using Api.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using System.Net;
using System.Text;

var builder = WebApplication.CreateBuilder(args);
var ddosProtection = builder.Configuration
    .GetSection(DdosProtectionOptions.SectionName)
    .Get<DdosProtectionOptions>() ?? new DdosProtectionOptions();

builder.WebHost.ConfigureKestrel(options =>
{
    // These limits protect the origin from connection exhaustion and slow HTTP
    // request attacks. They are deliberately configurable for the deployment.
    options.Limits.MaxConcurrentConnections = Math.Clamp(ddosProtection.MaxConcurrentConnections, 100, 100_000);
    options.Limits.MaxConcurrentUpgradedConnections = Math.Clamp(
        ddosProtection.MaxConcurrentUpgradedConnections,
        10,
        10_000);
    options.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(
        Math.Clamp(ddosProtection.RequestHeadersTimeoutSeconds, 5, 120));
    options.Limits.KeepAliveTimeout = TimeSpan.FromSeconds(
        Math.Clamp(ddosProtection.KeepAliveTimeoutSeconds, 10, 300));
    options.Limits.MinRequestBodyDataRate = new MinDataRate(
        Math.Clamp(ddosProtection.MinimumRequestBodyBytesPerSecond, 100, 100_000),
        TimeSpan.FromSeconds(10));
    options.Limits.MaxRequestBodySize = Math.Clamp(
        ddosProtection.MaxRequestBodyBytes,
        1L * 1024 * 1024,
        128L * 1024 * 1024);
});

if (!builder.Environment.IsDevelopment() && !builder.Environment.IsEnvironment("Testing"))
{
    var allowedHosts = builder.Configuration["AllowedHosts"];
    var allowedOrigins = (builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [])
        .Where(origin => !string.IsNullOrWhiteSpace(origin))
        .ToArray();
    var jwtSecretKey = builder.Configuration["Jwt:SecretKey"];
    if (string.IsNullOrWhiteSpace(allowedHosts) ||
        allowedHosts == "*" ||
        allowedOrigins.Length == 0 ||
        string.IsNullOrWhiteSpace(jwtSecretKey) ||
        Encoding.UTF8.GetByteCount(jwtSecretKey) < 32)
    {
        throw new InvalidOperationException(
            "Production requires explicit AllowedHosts, Cors:AllowedOrigins and a JWT secret of at least 32 bytes.");
    }
}

builder.Services.AddApiServices(builder.Configuration);

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    if (app.Environment.IsDevelopment())
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<ProductDbContext>();
        await dbContext.Database.MigrateAsync();
    }
    else if (app.Environment.IsEnvironment("Testing"))
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<ProductDbContext>();
        await dbContext.Database.EnsureCreatedAsync();
    }

    await IdentitySeed.SeedAsync(scope.ServiceProvider);
}

if (!app.Environment.IsDevelopment() && !app.Environment.IsEnvironment("Testing"))
{
    app.UseForwardedHeaders(CreateForwardedHeadersOptions(builder.Configuration));
    app.UseHsts();
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

static ForwardedHeadersOptions CreateForwardedHeadersOptions(IConfiguration configuration)
{
    var options = new ForwardedHeadersOptions
    {
        ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
        ForwardLimit = 1
    };

    var trustedProxies = configuration.GetSection("ReverseProxy:TrustedProxies")
        .Get<string[]>()?
        .Where(value => !string.IsNullOrWhiteSpace(value))
        .Select(value => value.Trim())
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray() ?? [];

    if (trustedProxies.Length == 0)
    {
        // ASP.NET Core's safe default trusts loopback only. Do not accept
        // spoofable X-Forwarded-* headers from arbitrary internet clients.
        return options;
    }

    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
    foreach (var proxy in trustedProxies)
    {
        if (!IPAddress.TryParse(proxy, out var address))
        {
            throw new InvalidOperationException(
                $"ReverseProxy:TrustedProxies contains an invalid IP address: '{proxy}'.");
        }

        options.KnownProxies.Add(address);
    }

    return options;
}

public partial class Program
{
}
