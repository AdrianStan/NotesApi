using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace NotesApi.Infrastructure;

public class NotFoundException(string message) : Exception(message);
public class ConflictException(string message) : Exception(message);
public class ForbiddenException(string message) : Exception(message);
public class UnauthorizedException(string message) : Exception(message);

public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        var (status, title) = exception switch
        {
            UnauthorizedException => (401, "Unauthorized"),
            ForbiddenException => (403, "Forbidden"),
            NotFoundException => (404, "Not found"),
            ConflictException => (409, "Conflict"),
            _ => (500, "An unexpected error occurred")
        };

        if (status == 500)
            _logger.LogError(exception, "Unhandled error on {Method} {Path}", context.Request.Method, context.Request.Path);

        context.Response.StatusCode = status;
        await context.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = status == 500 ? null : exception.Message,
            Instance = context.Request.Path
        }, ct);

        return true;
    }
}