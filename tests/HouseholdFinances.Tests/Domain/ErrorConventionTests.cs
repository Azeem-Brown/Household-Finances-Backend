using HouseholdFinances.Domain.Errors;

namespace HouseholdFinances.Tests.Domain;

public class ErrorConventionTests
{
    [Fact]
    public void Format_PlacesEnumBeforeIdentifier()
    {
        // Representative enum value and numeric identifier of significance.
        var message = ErrorFormatter.Format(ErrorCode.NotFound, 42);

        Assert.Equal("NotFound 42", message);
        Assert.StartsWith("NotFound", message);
        Assert.EndsWith("42", message);
    }

    [Fact]
    public void Format_SupportsGuidIdentifiers()
    {
        var id = Guid.Parse("11111111-2222-3333-4444-555555555555");

        var message = ErrorFormatter.Format(ErrorCode.Conflict, id);

        Assert.Equal($"Conflict {id}", message);
        Assert.Contains("Conflict", message);
        Assert.Contains(id.ToString(), message);
    }

    [Fact]
    public void Format_RejectsNullIdentifier()
    {
        Assert.Throws<ArgumentNullException>(
            () => ErrorFormatter.Format(ErrorCode.InvalidInput, null!));
    }

    [Fact]
    public void Exception_MessageContainsEnumAndIdentifier()
    {
        var exception = new HouseholdFinancesException(ErrorCode.Unauthorized, 7);

        Assert.Equal(ErrorCode.Unauthorized, exception.ErrorCode);
        Assert.Equal(7, exception.Identifier);
        Assert.Equal("Unauthorized 7", exception.Message);
    }

    [Fact]
    public void Exception_ThrownAndCaught_PreservesFormattedMessage()
    {
        // Demonstrates the convention end to end: throw, catch, and read the message.
        HouseholdFinancesException? caught = null;
        try
        {
            throw new HouseholdFinancesException(ErrorCode.NotFound, 42);
        }
        catch (HouseholdFinancesException exception)
        {
            caught = exception;
        }

        Assert.NotNull(caught);
        Assert.Contains("NotFound", caught!.Message);
        Assert.Contains("42", caught.Message);
    }

    [Fact]
    public void ErrorCode_ValuesAreStableAndMatchTheFrontendConvention()
    {
        // Explicit numeric values keep the codes stable on the wire and match the sibling
        // frontend's categories (Unknown, NotFound, InvalidInput, Unauthorized, Conflict).
        Assert.Equal(0, (int)ErrorCode.Unknown);
        Assert.Equal(1, (int)ErrorCode.NotFound);
        Assert.Equal(2, (int)ErrorCode.InvalidInput);
        Assert.Equal(3, (int)ErrorCode.Unauthorized);
        Assert.Equal(4, (int)ErrorCode.Conflict);

        var expected = new[]
        {
            ErrorCode.Unknown,
            ErrorCode.NotFound,
            ErrorCode.InvalidInput,
            ErrorCode.Unauthorized,
            ErrorCode.Conflict
        };

        Assert.Equal(expected, Enum.GetValues<ErrorCode>());
    }
}
