namespace HouseholdFinances.Domain.Errors;

/// <summary>
/// A backend failure that carries an <see cref="ErrorCode"/> and the identifier or number of
/// significance that was being reached. Its message follows the convention produced by
/// <see cref="ErrorFormatter"/>: <c>&lt;enum&gt; &lt;identifier&gt;</c>. Global exception
/// handling maps it to an HTTP status and a ProblemDetails response.
/// </summary>
public sealed class HouseholdFinancesException : Exception
{
    /// <summary>Gets the backend error category.</summary>
    public ErrorCode ErrorCode { get; }

    /// <summary>
    /// Gets the identifier or number of significance that was being reached when the failure
    /// occurred. This may be an entity id (Guid) or a numeric value.
    /// </summary>
    public object Identifier { get; }

    /// <summary>
    /// Creates the exception and derives its message from the error code and identifier.
    /// </summary>
    /// <param name="errorCode">The backend error category.</param>
    /// <param name="identifier">The identifier or number of significance that was being reached.</param>
    public HouseholdFinancesException(ErrorCode errorCode, object identifier)
        : this(errorCode, identifier, innerException: null)
    {
    }

    /// <summary>
    /// Creates the exception and derives its message from the error code and identifier.
    /// </summary>
    /// <param name="errorCode">The backend error category.</param>
    /// <param name="identifier">The identifier or number of significance that was being reached.</param>
    /// <param name="innerException">The exception that caused this failure, if any.</param>
    public HouseholdFinancesException(ErrorCode errorCode, object identifier, Exception? innerException)
        : base(ErrorFormatter.Format(errorCode, identifier), innerException)
    {
        ErrorCode = errorCode;
        Identifier = identifier;
    }
}
