using Api.Services;
using Infrastructure.DbContextt;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Api.IntegrationTests;

public sealed class TestApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _databasePath = Path.Combine(
        Path.GetTempPath(),
        $"bse-integration-{Guid.NewGuid():N}.db");

    private readonly string _webRootPath = Path.Combine(
        Path.GetTempPath(),
        $"bse-integration-wwwroot-{Guid.NewGuid():N}");

    public const string StripeWebhookSecret = "whsec_integration_test_secret";

    public string WebRootPath => _webRootPath;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        Directory.CreateDirectory(_webRootPath);

        builder.UseEnvironment("Testing");
        builder.UseWebRoot(_webRootPath);
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = $"Data Source={_databasePath}",
                ["AllowedHosts"] = "localhost",
                ["Cors:AllowedOrigins:0"] = "http://localhost",
                ["Jwt:Issuer"] = "BSE.IntegrationTests",
                ["Jwt:Audience"] = "BSE.IntegrationTests",
                ["Jwt:SecretKey"] = "integration-tests-secret-key-must-be-at-least-32-bytes",
                ["Seed:SuperAdminPassword"] = "IntegrationTestPassword!1",
                ["Stripe:WebhookSecret"] = StripeWebhookSecret,
                ["SupportBot:Enabled"] = "false"
            });
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<ProductDbContext>>();
            services.RemoveAll<ProductDbContext>();
            services.RemoveAll<IDbContextOptionsConfiguration<ProductDbContext>>();
            services.AddDbContext<ProductDbContext>(options => options.UseSqlite($"Data Source={_databasePath}"));

            // The order lifecycle worker is unrelated to request-level security
            // tests and could mutate data while a test is asserting it.
            services.RemoveAll<IHostedService>();

            services.AddAuthentication(TestAuthHandler.AuthenticationSchemeName)
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.AuthenticationSchemeName, _ => { });
            services.PostConfigure<AuthenticationOptions>(options =>
            {
                options.DefaultAuthenticateScheme = TestAuthHandler.AuthenticationSchemeName;
                options.DefaultChallengeScheme = TestAuthHandler.AuthenticationSchemeName;
                options.DefaultScheme = TestAuthHandler.AuthenticationSchemeName;
            });
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (!disposing)
        {
            return;
        }

        TryDeleteFile(_databasePath);
        TryDeleteDirectory(_webRootPath);
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
            // The test runner will remove its temporary directory at a later time.
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (IOException)
        {
            // Same best-effort cleanup as the SQLite file above.
        }
    }
}
