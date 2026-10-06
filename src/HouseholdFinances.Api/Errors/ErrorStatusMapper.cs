using HouseholdFinances.Domain.Errors;

namespace HouseholdFinances.Api.Errors;

/// <summary>
/// Maps a <see cref="ErrorCode"/> to the HTTP status code and ProblemDetails title returned to
/// clients. Unclassified (<see cref="ErrorCode.Unknown"/>) failures map to 500.
/// </summary>
public static class ErrorStatusMapper
{
    /// <summary>Returns the HTTP status code for <paramref name="errorCode"/>.</summary>
    public static int ToStatusCode(ErrorCode errorCode) => errorCode switch
    {
        ErrorCode.NotFound => StatusCodes.Status404NotFound,
        ErrorCode.InvalidInput => StatusCodes.Status400BadRequest,
        ErrorCode.Unauthorized => StatusCodes.Status401Unauthorized,
        ErrorCode.Conflict => StatusCodes.Status409Conflict,
        _ => StatusCodes.Status500InternalServerError
    };

    /// <summary>Returns the human-readable ProblemDetails title for <paramref name="errorCode"/>.</summary>
    public static string ToTitle(ErrorCode errorCode) => errorCode switch
    {
        ErrorCode.NotFound => "Not Found",
        ErrorCode.InvalidInput => "Invalid Input",
        ErrorCode.Unauthorized => "Unauthorized",
        ErrorCode.Conflict => "Conflict",
        _ => "An unexpected error occurred."
    };
}
