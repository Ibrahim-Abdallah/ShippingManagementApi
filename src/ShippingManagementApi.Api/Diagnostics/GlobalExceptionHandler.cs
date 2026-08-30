using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace ShippingManagementApi.Api.Diagnostics;

internal sealed class GlobalExceptionHandler(
    ILogger<GlobalExceptionHandler> logger,
    IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (IsFrameworkBadRequest(exception))
        {
            logger.LogWarning("Invalid request received for {RequestMethod} {RequestPath}",
                httpContext.Request.Method, httpContext.Request.Path);

            httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
            {
                HttpContext = httpContext,
                ProblemDetails = new ProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "Invalid request.",
                    Detail = "The request could not be parsed or contains an invalid value.",
                    Type = "https://tools.ietf.org/html/rfc9110#section-15.5.1"
                }
            });
        }

        logger.LogError(exception, "An unhandled exception occurred while processing {RequestMethod} {RequestPath}",
            httpContext.Request.Method, httpContext.Request.Path);

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "An unexpected error occurred.",
                Detail = "The server could not complete the request.",
                Type = "https://tools.ietf.org/html/rfc9110#section-15.6.1"
            }
        });
    }

    private static bool IsFrameworkBadRequest(Exception exception) =>
        exception is BadHttpRequestException { StatusCode: StatusCodes.Status400BadRequest };
}
