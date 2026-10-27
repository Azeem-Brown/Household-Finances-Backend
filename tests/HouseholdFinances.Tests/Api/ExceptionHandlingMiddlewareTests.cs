using System.Globalization;
using System.Text.Json;
using HouseholdFinances.Api.Errors;
using HouseholdFinances.Domain.Errors;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace HouseholdFinances.Tests.Api;

public class ExceptionHandlingMiddlewareTests
{
    [Theory]
    [InlineData(ErrorCode.NotFound, 42, StatusCodes.Status404NotFound)]
    [InlineData(ErrorCode.InvalidInput, 7, StatusCodes.Status400BadRequest)]
    [InlineData(ErrorCode.Unauthorized, 99, StatusCodes.Status401Unauthorized)]
    [InlineData(ErrorCode.Conflict, 5, StatusCodes.Status409Conflict)]
    public async Task InvokeAsync_MapsTypedExceptionToStatusAndBody(
        ErrorCode errorCode,
        int identifier,
        int expectedStatus)
    {
        var (status, body, contentType) =
            await InvokeAsync(new HouseholdFinancesException(errorCode, identifier));

        Assert.Equal(expectedStatus, status);
        Assert.StartsWith("application/problem+json", contentType);
        Assert.Equal(expectedStatus, body.GetProperty("status").GetInt32());
        Assert.Equal((int)errorCode, body.GetProperty("errorCode").GetInt32());
        Assert.Equal(errorCode.ToString(), body.GetProperty("errorName").GetString());
        Assert.Equal(
            identifier.ToString(CultureInfo.InvariantCulture),
            body.GetProperty("identifier").GetString());
        Assert.Equal($"{errorCode} {identifier}", body.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task InvokeAsync_MapsGuidIdentifierToNotFound()
    {
        var id = Guid.Parse("11111111-2222-3333-4444-555555555555");

        var (status, body, _) = await InvokeAsync(new HouseholdFinancesException(ErrorCode.NotFound, id));

        Assert.Equal(StatusCodes.Status404NotFound, status);
        Assert.Equal(id.ToString(), body.GetProperty("identifier").GetString());
        Assert.Equal($"NotFound {id}", body.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task InvokeAsync_UnexpectedException_ReturnsGenericProblemWithoutInternalDetails()
    {
        var (status, body, contentType) =
            await InvokeAsync(new InvalidOperationException("Secret internal detail"));

        Assert.Equal(StatusCodes.Status500InternalServerError, status);
        Assert.StartsWith("application/problem+json", contentType);
        Assert.Equal(StatusCodes.Status500InternalServerError, body.GetProperty("status").GetInt32());
        Assert.Equal((int)ErrorCode.Unknown, body.GetProperty("errorCode").GetInt32());
        Assert.Equal(ErrorCode.Unknown.ToString(), body.GetProperty("errorName").GetString());

        // No detail (message, stack trace, or identifier) must be present.
        Assert.False(body.TryGetProperty("detail", out _));
        Assert.False(body.TryGetProperty("identifier", out _));
        Assert.DoesNotContain("Secret internal detail", body.GetRawText());
    }

    private static async Task<(int StatusCode, JsonElement Body, string ContentType)> InvokeAsync(
        Exception exception)
    {
        var context = new DefaultHttpContext
        {
            Response = { Body = new MemoryStream() }
        };

        var middleware = new ExceptionHandlingMiddleware(
            _ => throw exception,
            NullLogger<ExceptionHandlingMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var document = await JsonDocument.ParseAsync(context.Response.Body);

        return (context.Response.StatusCode, document.RootElement.Clone(), context.Response.ContentType ?? string.Empty);
    }
}
