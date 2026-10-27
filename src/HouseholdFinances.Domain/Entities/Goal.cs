namespace HouseholdFinances.Domain.Entities;

/// <summary>
/// A savings goal for a household. Matches the specification "Goals" schema, including the
/// Total field carried over from the bill implementation.
/// </summary>
public class Goal
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public double Value { get; set; }

    public DateTime StartDate { get; set; }

    public DateTime EndDate { get; set; }

    public bool Recurring { get; set; }

    /// <summary>
    /// The cadence used when <see cref="Recurring"/> is true; <see langword="null"/> for a one-off
    /// goal. The service clears this to <see langword="null"/> whenever the goal is not recurring, so
    /// storage has exactly one representation per kind and a stored cadence always means a recurring
    /// goal. This extends the nullable-interval decision for Income and Bills (issue #30) to Goals so
    /// the domain stays consistent.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The occurrence rule</b> (approved decision #5). A recurring goal's occurrences are
    /// <see cref="StartDate"/>, then <see cref="StartDate"/> stepped by this cadence one at a time,
    /// while the occurrence is on or before <see cref="EndDate"/>; <see cref="EndDate"/> is
    /// inclusive. Stepping is +1 day, +7 days, +14 days, +1 month, +3 months, or +1 year, and the
    /// calendar steps are anchored to the original <see cref="StartDate"/> so they never drift.
    /// </para>
    /// <para>
    /// <b>One-off goals.</b> A goal that is not recurring has exactly one occurrence, on
    /// <see cref="StartDate"/>. The same classification applies to a row whose <see cref="Recurring"/>
    /// is true but whose interval is null.
    /// </para>
    /// <para>
    /// Dates are compared as UTC calendar dates; time of day is ignored. An entry whose
    /// <see cref="EndDate"/> precedes its <see cref="StartDate"/> produces no occurrences, although
    /// the service rejects that input so only a pre-existing row could hold it.
    /// </para>
    /// <para>
    /// <see cref="Value"/> is the goal's target amount and is never multiplied by the number of
    /// occurrences: an occurrence is a schedule checkpoint, not a charge. <see cref="Total"/> is the
    /// amount contributed so far and is independent of the occurrence dates. The server returns raw
    /// rows; it computes no goal totals, and goals do not contribute to a household's incomes or
    /// payments.
    /// </para>
    /// </remarks>
    public Interval? Interval { get; set; }

    public Guid HouseholdId { get; set; }

    public Guid UserId { get; set; }

    /// <summary>Amount contributed toward the goal so far.</summary>
    public double Total { get; set; }
}
