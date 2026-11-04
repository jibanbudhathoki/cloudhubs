using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace CloudHubs.Web.Middleware;

public sealed class GlobalExceptionHandler(
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
        {
            logger.LogDebug(
                "Request {Method} {Path} was canceled by the client.",
                httpContext.Request.Method,
                httpContext.Request.Path);
            return true;
        }

        if (httpContext.Response.HasStarted)
        {
            logger.LogError(
                exception,
                "An exception occurred after the response started for {Method} {Path}.",
                httpContext.Request.Method,
                httpContext.Request.Path);
            return false;
        }

        var (statusCode, title, detail) = MapException(exception);
        var traceId = httpContext.TraceIdentifier;
        if (statusCode >= StatusCodes.Status500InternalServerError)
        {
            logger.LogError(
                exception,
                "Unhandled exception processing {Method} {Path}. TraceId: {TraceId}",
                httpContext.Request.Method,
                httpContext.Request.Path,
                traceId);
        }
        else
        {
            logger.LogInformation(
                "Request {Method} {Path} failed with {StatusCode}: {ExceptionType}. TraceId: {TraceId}",
                httpContext.Request.Method,
                httpContext.Request.Path,
                statusCode,
                exception.GetType().Name,
                traceId);
        }

        httpContext.Response.StatusCode = statusCode;
        if (IsApiRequest(httpContext.Request))
        {
            httpContext.Response.ContentType = "application/problem+json";
            var problem = new ProblemDetails
            {
                Status = statusCode,
                Title = title,
                Detail = detail,
                Instance = httpContext.Request.Path
            };
            problem.Extensions["traceId"] = traceId;
            await httpContext.Response.WriteAsJsonAsync(
                problem,
                options: null,
                contentType: "application/problem+json",
                cancellationToken: httpContext.RequestAborted);
            return true;
        }

        var safeTitle = HtmlEncoder.Default.Encode(title);
        var safeTraceId = HtmlEncoder.Default.Encode(traceId);
        httpContext.Response.ContentType = "text/html; charset=utf-8";
        await httpContext.Response.WriteAsync(
            $"<!doctype html><html lang=\"en\"><meta charset=\"utf-8\"><title>{statusCode} - CloudHubs</title>" +
            $"<body><main><h1>{statusCode} - {safeTitle}</h1><p>The request could not be completed.</p>" +
            $"<p>Reference: <code>{safeTraceId}</code></p><a href=\"/\">Return home</a></main></body></html>",
            httpContext.RequestAborted);
        return true;
    }

    private static (int StatusCode, string Title, string? Detail) MapException(Exception exception) =>
        exception switch
        {
            ArgumentException => (StatusCodes.Status400BadRequest, "Invalid request", exception.Message),
            UnauthorizedAccessException => (StatusCodes.Status403Forbidden, "Access denied", null),
            KeyNotFoundException => (StatusCodes.Status404NotFound, "Resource not found", null),
            InvalidOperationException => (StatusCodes.Status409Conflict, "Request conflict", exception.Message),
            _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred", null)
        };

    private static bool IsApiRequest(HttpRequest request) =>
        request.Path.StartsWithSegments("/api")
        || request.Headers.Accept.Any(value =>
            value?.Contains("application/json", StringComparison.OrdinalIgnoreCase) == true
            || value?.Contains("application/problem+json", StringComparison.OrdinalIgnoreCase) == true);
}
