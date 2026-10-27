using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using HouseholdFinances.Domain.Errors;
using Microsoft.AspNetCore.Mvc;

namespace HouseholdFinances.Api.Errors;

/// <summary>
/// Writes the repository's error convention: an <c>application/problem+json</c> body carrying the
/// numeric error code, the enum name, and the identifier of significance.
/// </summary>
/// <remarks>
/// Shared by <see cref="ExceptionHandlingMiddleware"/> and the rate limiter, so a throttled request
/// looks exactly like every other failure rather than inventing a second error shape.
/// </remarks>
internal static class ProblemResponse
{
    private const string ContentType = "application/problem+json";

    // Deterministic camelCase output. Null members are omitted so a response never exposes a
    // message that was not deliberately set.
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>Builds a ProblemDetails body for an error code.</summary>
    /// <param name="errorCode">The failure category, which supplies the status and title.</param>
    /// <param name="identifier">The identifier of significance for the failure.</param>
    /// <param name="detail">An optional detail message.</param>
    /// <returns>The populated ProblemDetails.</returns>
    public static ProblemDetails Describe(ErrorCode errorCode, object? identifier, string? detail = null)
    {
        ProblemDetails problem = new ProblemDetails
        {
            Status = ErrorStatusMapper.ToStatusCode(errorCode),
            Title = ErrorStatusMapper.ToTitle(errorCode),
            Detail = detail
        };
        problem.Extensions["errorCode"] = (int)errorCode;
        problem.Extensions["errorName"] = errorCode.ToString();
        problem.Extensions["identifier"] = Convert.ToString(identifier, CultureInfo.InvariantCulture);

        return problem;
    }

    /// <summary>Writes the problem body with the supplied status.</summary>
    /// <param name="context">The current HTTP context.</param>
    /// <param name="status">The HTTP status to return.</param>
    /// <param name="problem">The body to write.</param>
    public static async Task WriteAsync(HttpContext context, int status, ProblemDetails problem)
    {
        if (context.Response.HasStarted)
        {
            return;
        }

        context.Response.StatusCode = status;
        context.Response.ContentType = ContentType;

        await context.Response.WriteAsJsonAsync(problem, Options, ContentType);
    }
}
