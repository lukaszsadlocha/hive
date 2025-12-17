using System.Net;
using System.Text.Json;
using Microsoft.Azure.Cosmos;

namespace Hive.Api.Middleware;

/// <summary>
/// Global exception handling middleware for catching and formatting all unhandled exceptions
/// </summary>
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly IHostEnvironment _environment;

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger,
        IHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unhandled exception occurred: {Message}", ex.Message);
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";

        var errorResponse = new ErrorResponse
        {
            TraceId = context.TraceIdentifier,
            Timestamp = DateTime.UtcNow
        };

        // Handle specific exception types with appropriate status codes and messages
        // Note: More specific exceptions must come before more general ones
        switch (exception)
        {
            case ArgumentNullException nullEx:
                context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                errorResponse.Error = "ValidationError";
                errorResponse.Message = $"Required parameter '{nullEx.ParamName}' is missing";
                errorResponse.Details = new { parameter = nullEx.ParamName };
                break;

            case ArgumentException argEx:
                context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                errorResponse.Error = "ValidationError";
                errorResponse.Message = argEx.Message;
                errorResponse.Details = new { parameter = argEx.ParamName };
                break;

            case KeyNotFoundException notFoundEx:
                context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                errorResponse.Error = "NotFound";
                errorResponse.Message = notFoundEx.Message;
                break;

            case UnauthorizedAccessException _:
                context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                errorResponse.Error = "Unauthorized";
                errorResponse.Message = "Authentication required";
                break;

            case InvalidOperationException invalidEx:
                context.Response.StatusCode = (int)HttpStatusCode.Conflict;
                errorResponse.Error = "InvalidOperation";
                errorResponse.Message = invalidEx.Message;
                break;

            case CosmosException cosmosEx:
                context.Response.StatusCode = (int)cosmosEx.StatusCode;
                errorResponse.Error = "DatabaseError";
                errorResponse.Message = cosmosEx.StatusCode switch
                {
                    HttpStatusCode.NotFound => "Resource not found",
                    HttpStatusCode.Conflict => "Resource already exists",
                    HttpStatusCode.TooManyRequests => "Rate limit exceeded, please retry later",
                    HttpStatusCode.RequestEntityTooLarge => "Request payload too large",
                    _ => "Database operation failed"
                };
                errorResponse.Details = new
                {
                    activityId = cosmosEx.ActivityId,
                    requestCharge = cosmosEx.RequestCharge,
                    retryAfter = cosmosEx.RetryAfter?.TotalSeconds
                };
                break;

            case TimeoutException timeoutEx:
                context.Response.StatusCode = (int)HttpStatusCode.RequestTimeout;
                errorResponse.Error = "Timeout";
                errorResponse.Message = "The operation timed out";
                break;

            case OperationCanceledException _:
                context.Response.StatusCode = (int)HttpStatusCode.RequestTimeout;
                errorResponse.Error = "RequestCancelled";
                errorResponse.Message = "The request was cancelled";
                break;

            default:
                context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                errorResponse.Error = "InternalServerError";
                errorResponse.Message = "An unexpected error occurred";

                // In development, include more details
                if (_environment.IsDevelopment())
                {
                    errorResponse.Details = new
                    {
                        type = exception.GetType().Name,
                        message = exception.Message,
                        stackTrace = exception.StackTrace,
                        innerException = exception.InnerException?.Message
                    };
                }
                break;
        }

        // Serialize and write response
        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = _environment.IsDevelopment()
        };

        var json = JsonSerializer.Serialize(errorResponse, jsonOptions);
        await context.Response.WriteAsync(json);
    }
}

/// <summary>
/// Standard error response format
/// </summary>
public class ErrorResponse
{
    public string Error { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public object? Details { get; set; }
    public string TraceId { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// Extension method for easy middleware registration
/// </summary>
public static class ExceptionHandlingMiddlewareExtensions
{
    public static IApplicationBuilder UseGlobalExceptionHandler(
        this IApplicationBuilder app)
    {
        return app.UseMiddleware<ExceptionHandlingMiddleware>();
    }
}
