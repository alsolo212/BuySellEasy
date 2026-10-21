using System.Text;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Api.Hubs;
using Api.Options;
using Api.Services;
using Domain.IdentityEntities;
using Infrastructure.DbContextt;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Options;
using System.Text.Json.Serialization;

namespace Api.Extensions;

public static class ApiServiceCollectionExtensions
{
    public const string FrontendCorsPolicyName = "Frontend";
    public const string AuthRateLimitPolicyName = "auth-endpoints";
    public const string FileUploadRateLimitPolicyName = "file-uploads";
    public const string SupportBotRateLimitPolicyName = "support-bot";

    public static IServiceCollection AddApiServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.Configure<GoogleAuthOptions>(configuration.GetSection(GoogleAuthOptions.SectionName));
        services.Configure<SupportBotOptions>(configuration.GetSection(SupportBotOptions.SectionName));
        services.Configure<StripeOptions>(configuration.GetSection(StripeOptions.SectionName));
        services.Configure<FormOptions>(options =>
        {
            options.MultipartBodyLengthLimit = FileUploadSecurityService.MaxListingRequestBytes;
        });

        services
            .AddControllers()
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
            });
        services.AddSignalR()
            .AddJsonProtocol(options =>
            {
                options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter());
            });

        services.AddRateLimiter(options =>
        {
            var ddosProtection = configuration
                .GetSection(DdosProtectionOptions.SectionName)
                .Get<DdosProtectionOptions>() ?? new DdosProtectionOptions();
            var requestsPerMinute = Math.Clamp(ddosProtection.RequestsPerMinute, 60, 10_000);
            var burstSize = Math.Clamp(ddosProtection.BurstSize, 1, requestsPerMinute);
            var supportBotRequestsPerMinute = Math.Clamp(
                configuration.GetValue<int?>("SupportBot:RequestsPerMinute") ?? 20,
                1,
                120);

            // This policy applies to every request, including endpoints that do
            // not have a more specific policy. The key is the actual socket IP,
            // or the client IP supplied by a configured, trusted reverse proxy.
            // A zero-length queue ensures a flood is rejected instead of tying
            // up request workers waiting for a permit.
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                RateLimitPartition.GetTokenBucketLimiter(
                    GetClientPartitionKey(context),
                    _ => new TokenBucketRateLimiterOptions
                    {
                        TokenLimit = burstSize,
                        TokensPerPeriod = Math.Max(1, (int)Math.Ceiling(requestsPerMinute / 60d)),
                        ReplenishmentPeriod = TimeSpan.FromSeconds(1),
                        QueueLimit = 0,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        AutoReplenishment = true
                    }));

            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, cancellationToken) =>
            {
                if (context.HttpContext.Response.HasStarted)
                {
                    return;
                }

                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter =
                        Math.Max(1, Math.Ceiling(retryAfter.TotalSeconds)).ToString();
                }

                await context.HttpContext.Response.WriteAsJsonAsync(new ProblemDetails
                {
                    Status = StatusCodes.Status429TooManyRequests,
                    Title = "Too many requests",
                    Detail = "Please try again later.",
                    Instance = context.HttpContext.Request.Path
                }, options: null, contentType: "application/problem+json", cancellationToken: cancellationToken);
            };

            options.AddPolicy(AuthRateLimitPolicyName, context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    GetClientPartitionKey(context),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 10,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                        AutoReplenishment = true
                    }));

            options.AddPolicy(FileUploadRateLimitPolicyName, context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    GetAuthenticatedClientPartitionKey(context),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 20,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                        AutoReplenishment = true
                    }));

            // Support replies are invoked by the chat-message endpoint. Applying
            // this per-account limit there prevents a user from repeatedly
            // invoking the local model while remaining generous for normal chat.
            options.AddPolicy(SupportBotRateLimitPolicyName, context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    GetAuthenticatedClientPartitionKey(context),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = supportBotRequestsPerMinute,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                        AutoReplenishment = true
                    }));
        });

        services.Configure<ApiBehaviorOptions>(options =>
        {
            options.InvalidModelStateResponseFactory = context =>
            {
                var errors = context.ModelState
                    .Where(x => x.Value?.Errors.Count > 0)
                    .ToDictionary(
                        x => x.Key,
                        x => x.Value!.Errors.Select(error => error.ErrorMessage).ToArray());

                return new BadRequestObjectResult(new ValidationProblemDetails(errors)
                {
                    Title = "Validation failed",
                    Status = StatusCodes.Status400BadRequest,
                    Instance = context.HttpContext.Request.Path
                });
            };
        });

        services.AddCors(options =>
        {
            options.AddPolicy(FrontendCorsPolicyName, policy =>
            {
                var origins = (configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>())
                    .Select(origin => origin.Trim().TrimEnd('/'))
                    .Where(origin => !string.IsNullOrWhiteSpace(origin))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                policy
                    .WithOrigins(origins)
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials()
                    .WithExposedHeaders("X-Request-Id", "X-Total-Count", "X-Page", "X-Page-Size");
            });
        });

        services.AddDbContext<ProductDbContext>(options =>
        {
            options.UseNpgsql(
                configuration.GetConnectionString("DefaultConnection"),
                npgsqlOptions => npgsqlOptions.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery));
        });

        services.AddIdentity<User, Role>(options =>
            {
                options.Password.RequireDigit = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequiredLength = 6;
                options.User.AllowedUserNameCharacters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._@+ ";
                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            })
            .AddEntityFrameworkStores<ProductDbContext>()
            .AddDefaultTokenProviders()
            .AddUserStore<UserStore<User, Role, ProductDbContext, Guid>>()
            .AddRoleStore<RoleStore<Role, ProductDbContext, Guid>>();

        services
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                var jwtOptions = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
                var key = Encoding.UTF8.GetBytes(jwtOptions.SecretKey);

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtOptions.Issuer,
                    ValidAudience = jwtOptions.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ClockSkew = TimeSpan.Zero
                };

                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var accessToken = context.Request.Query["access_token"];
                        var path = context.HttpContext.Request.Path;
                        if (!string.IsNullOrWhiteSpace(accessToken) &&
                            (path.StartsWithSegments("/hubs/chat") ||
                             path.StartsWithSegments("/hubs/orders")))
                        {
                            context.Token = accessToken;
                        }

                        return Task.CompletedTask;
                    }
                };
            });

        services.AddAuthorization();
        services.AddHttpContextAccessor();

        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<StripePaymentService>();
        services.AddScoped<InventoryService>();
        services.AddScoped<OrderRealtimeNotifier>();
        services.AddScoped<OrderPaymentLifecycleService>();
        services.AddSingleton<FileUploadSecurityService>();
        services.AddHostedService<OrderLifecycleBackgroundService>();
        services.AddHttpClient<SupportBotService>((serviceProvider, client) =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<SupportBotOptions>>().Value;
            client.Timeout = TimeSpan.FromSeconds(Math.Clamp(options.TimeoutSeconds, 5, 60));
        });

        return services;
    }

    private static string GetClientPartitionKey(HttpContext context)
    {
        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown-client";
    }

    private static string GetAuthenticatedClientPartitionKey(HttpContext context)
    {
        return context.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? GetClientPartitionKey(context);
    }
}
