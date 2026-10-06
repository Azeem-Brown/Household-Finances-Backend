using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using HouseholdFinances.Domain.Errors;
using Microsoft.AspNetCore.Mvc;

namespace HouseholdFinances.Api.Errors;

/// <summary>
/// Global exception handling. Converts a <see cref="HouseholdFinancesException"/> into a
/// ProblemDetails response carrying the numeric error code, the enum name, and the identifier of
/// significance, and converts every other exception into a generic 500 response that does not
/// leak stack traces or internal details.
/// </summary>
public sealed class ExceptionHandlingMiddleware
{
    private const string ProblemJsonContentType = "application/problem+json";

    // Deterministic camelCase output. Null members (for example an absent detail) are omitted so
    // unexpected failures never expose a message.
    private static readonly JsonSerializerOptions ProblemJsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    /// <summary>Creates the middleware.</summary>
    /// <param name="next">The next middleware in the pipeline.</param>
    /// <param name="logger">The logger used to record handled and unhandled failures.</param>
    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>Runs the pipeline and translates any thrown exception into an HTTP response.</summary>
    /// <param name="context">The current HTTP context.</param>
    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        try
        {
            await _next(context);
        }
        catch (HouseholdFinancesException exception)
        {
            _logger.LogWarning(
                exception,
                "Handled error {ErrorCode} for identifier {Identifier}.",
                exception.ErrorCode,
                exception.Identifier);

            await WriteHandledErrorAsync(context, exception);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unhandled exception.");
            await WriteUnexpectedErrorAsync(context);
        }
    }

    private static async Task WriteHandledErrorAsync(HttpContext context, HouseholdFinancesException exception)
    {
        if (context.Response.HasStarted)
        {
            return;
        }

        var status = ErrorStatusMapper.ToStatusCode(exception.ErrorCode);
        var problem = new ProblemDetails
        {
            Status = status,
            Title = ErrorStatusMapper.ToTitle(exception.ErrorCode),
            Detail = exception.Message
        };
        problem.Extensions["errorCode"] = (int)exception.ErrorCode;
        problem.Extensions["errorName"] = exception.ErrorCode.ToString();
        problem.Extensions["identifier"] = Convert.ToString(exception.Identifier, CultureInfo.InvariantCulture);

        await WriteProblemAsync(context, status, problem);
    }

    private static async Task WriteUnexpectedErrorAsync(HttpContext context)
    {
        if (context.Response.HasStarted)
        {
            return;
        }

        const int status = StatusCodes.Status500InternalServerError;
        var problem = new ProblemDetails
        {
            Status = status,
            Title = ErrorStatusMapper.ToTitle(ErrorCode.Unknown)
        };
        problem.Extensions["errorCode"] = (int)ErrorCode.Unknown;
        problem.Extensions["errorName"] = ErrorCode.Unknown.ToString();

        await WriteProblemAsync(context, status, problem);
    }

    private static async Task WriteProblemAsync(HttpContext context, int status, ProblemDetails problem)
    {
        context.Response.StatusCode = status;
        context.Response.ContentType = ProblemJsonContentType;

        await context.Response.WriteAsJsonAsync(problem, ProblemJsonOptions, ProblemJsonContentType);
    }
}
