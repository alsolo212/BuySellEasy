using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;

namespace Api.Middleware;

public class ApiExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ApiExceptionMiddleware> _logger;

    public ApiExceptionMiddleware(RequestDelegate next, ILogger<ApiExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task Invoke(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception exception)
        {
            var requestId = Guid.NewGuid().ToString("N");
            _logger.LogError(
                exception,
                "Unhandled API exception. RequestId: {RequestId}, Method: {Method}, Path: {Path}",
                requestId,
                context.Request.Method,
                context.Request.Path);

            if (!context.Response.HasStarted)
            {
                await WriteProblemDetailsAsync(context, exception, requestId);
            }
        }
    }

    private static async Task WriteProblemDetailsAsync(HttpContext context, Exception exception, string requestId)
    {
        var (statusCode, title) = exception switch
        {
            KeyNotFoundException => (StatusCodes.Status404NotFound, "Resource not found"),
            ValidationException => (StatusCodes.Status400BadRequest, "Validation failed"),
            UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, "Unauthorized"),
            DbUpdateException => (StatusCodes.Status409Conflict, "Operation cannot be completed"),
            InvalidOperationException => (StatusCodes.Status409Conflict, "Operation cannot be completed"),
            _ => (StatusCodes.Status500InternalServerError, "Internal server error")
        };

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json";
        context.Response.Headers["X-Request-Id"] = requestId;

        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = statusCode switch
            {
                StatusCodes.Status400BadRequest => "The request is invalid.",
                StatusCodes.Status401Unauthorized => "Authentication is required or the credentials are invalid.",
                StatusCodes.Status404NotFound => "The requested resource was not found.",
                StatusCodes.Status409Conflict => "The operation could not be completed.",
                _ => "An unexpected error occurred."
            },
            Instance = context.Request.Path
        };
        problem.Extensions["requestId"] = requestId;

        await context.Response.WriteAsJsonAsync(problem);
    }
}
