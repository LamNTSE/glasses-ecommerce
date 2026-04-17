using System.Net;
using System.Text.Json;
using OpticalStore.API.Responses;
using OpticalStore.BLL.Exceptions;

namespace OpticalStore.API.Middleware;

public sealed class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (AppException ex)
        {
            context.Response.StatusCode = (int)ex.StatusCode;
            context.Response.ContentType = "application/json";

            var response = new ApiResponse<object>
            {
                Code = (int)ex.StatusCode,
                Message = ex.Message,
                Result = null
            };

            await context.Response.WriteAsync(JsonSerializer.Serialize(response));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception.");

            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            context.Response.ContentType = "application/json";

            var response = new ApiResponse<object>
            {
                Code = 500,
                Message = "Internal server error.",
                Result = null
            };

            await context.Response.WriteAsync(JsonSerializer.Serialize(response));
        }
    }
}
