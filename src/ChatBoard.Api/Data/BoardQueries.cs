using ChatBoard.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace ChatBoard.Api.Data;

/// <summary>
/// Shared board queries that don't belong to any one feature. Kept here so appending
/// a card to the end of a column has a single definition used by both create and promote.
/// </summary>
public static class BoardQueries
{
    /// <summary>The position for a new card appended below everything in its column.</summary>
    public static async Task<double> AppendPositionAsync(this BoardDbContext db, CardStatus status)
    {
        var positions = await db.Cards
            .Where(c => c.Status == status)
            .Select(c => (double?)c.Position)
            .ToListAsync();
        var max = positions.Count == 0 ? null : positions.Max();
        return BoardPosition.Between(max, null);
    }
}
