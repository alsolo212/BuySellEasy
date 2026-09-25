using Api.Extensions;
using Infrastructure.DbContextt;
using Microsoft.EntityFrameworkCore;

namespace Api.Middleware;

public class BlockedUserMiddleware
{
    private readonly RequestDelegate _next;

    public BlockedUserMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ProductDbContext dbContext)
    {
        if (!context.User.Identity?.IsAuthenticated ?? true)
        {
            await _next(context);
            return;
        }

        var userId = context.User.GetRequiredUserId();
        var isBlocked = await dbContext.Users
            .AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(user => user.IsBlocked)
            .FirstOrDefaultAsync();

        if (isBlocked)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new
            {
                message = "This account is blocked."
            });
            return;
        }

        await _next(context);
    }
}
