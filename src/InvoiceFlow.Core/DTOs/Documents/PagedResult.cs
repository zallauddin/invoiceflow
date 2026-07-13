namespace InvoiceFlow.Core.DTOs.Documents;

/// <summary>Generic paged result wrapper for paginated list responses.</summary>
/// <typeparam name="T">The type of items in the paged result.</typeparam>
public sealed record PagedResult<T>
{
    /// <summary>The items in the current page.</summary>
    public required IReadOnlyList<T> Items { get; init; }

    /// <summary>Total number of items matching the query.</summary>
    public required int TotalCount { get; init; }

    /// <summary>Current page number (1-based).</summary>
    public required int Page { get; init; }

    /// <summary>Number of items per page.</summary>
    public required int PageSize { get; init; }

    /// <summary>Total number of pages available.</summary>
    public required int TotalPages { get; init; }
}
