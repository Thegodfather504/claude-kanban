using ChatBoard.Api.Data;
using ChatBoard.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace ChatBoard.Api.Features.Cards;

public static class CardEndpoints
{
    /// <summary>The three columns always shown, in board order. Closed is added only on request.</summary>
    private static readonly CardStatus[] OpenStatuses =
        { CardStatus.NeedsAction, CardStatus.Waiting, CardStatus.Reference };

    public static void MapCardEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/board", GetBoard);
        app.MapPost("/api/cards", CreateCard);
        app.MapGet("/api/cards/{id:guid}", GetCard);
        app.MapPut("/api/cards/{id:guid}", UpdateCard);
        app.MapPatch("/api/cards/{id:guid}/move", MoveCard);
        app.MapDelete("/api/cards/{id:guid}", DeleteCard);
    }

    private static async Task<IResult> GetBoard(BoardDbContext db, bool includeClosed = false)
    {
        var statuses = includeClosed
            ? OpenStatuses.Append(CardStatus.Closed).ToArray()
            : OpenStatuses;

        var cards = await db.Cards
            .Where(c => statuses.Contains(c.Status))
            .OrderBy(c => c.Status)
            .ThenBy(c => c.Position)
            .ToListAsync();

        var columns = statuses
            .Select(status => new BoardColumn(
                status,
                cards.Where(c => c.Status == status)
                    .Select(CardResponse.From)
                    .ToList()))
            .ToList();

        return Results.Ok(new BoardResponse(columns));
    }

    private static async Task<IResult> CreateCard(CreateCardRequest request, BoardDbContext db, TimeProvider clock)
    {
        var status = request.Status ?? CardStatus.NeedsAction;
        var now = clock.GetUtcNow();

        var position = await db.AppendPositionAsync(status);

        Card card;
        try
        {
            card = Card.Create(
                request.Title,
                status,
                position,
                now,
                request.Url,
                request.ChatUuid,
                request.NextAction,
                request.Tags);
        }
        catch (ArgumentException ex)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(request.Title)] = new[] { ex.Message }
            });
        }

        db.Cards.Add(card);
        await db.SaveChangesAsync();
        return Results.Created($"/api/cards/{card.Id}", CardResponse.From(card));
    }

    private static async Task<IResult> GetCard(Guid id, BoardDbContext db)
    {
        var card = await db.Cards.FindAsync(id);
        return card is null ? Results.NotFound() : Results.Ok(CardResponse.From(card));
    }

    private static async Task<IResult> UpdateCard(Guid id, UpdateCardRequest request, BoardDbContext db, TimeProvider clock)
    {
        var card = await db.Cards.FindAsync(id);
        if (card is null)
        {
            return Results.NotFound();
        }

        var now = clock.GetUtcNow();
        try
        {
            card.Describe(request.Title, request.Url, request.NextAction, now);
        }
        catch (ArgumentException ex)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(request.Title)] = new[] { ex.Message }
            });
        }

        card.ReplaceTags(request.Tags ?? Array.Empty<string>(), now);
        await db.SaveChangesAsync();
        return Results.Ok(CardResponse.From(card));
    }

    private static async Task<IResult> MoveCard(Guid id, MoveCardRequest request, BoardDbContext db, TimeProvider clock)
    {
        var card = await db.Cards.FindAsync(id);
        if (card is null)
        {
            return Results.NotFound();
        }

        var now = clock.GetUtcNow();

        // Rebalance-then-place must be one unit of work: a renumber followed by a
        // placement that read stale positions would corrupt the column order.
        await using var tx = await db.Database.BeginTransactionAsync();

        var position = await ResolvePosition(db, request, excludeCardId: id, now);
        card.MoveTo(request.Status, position, now);

        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(CardResponse.From(card));
    }

    private static async Task<IResult> DeleteCard(Guid id, BoardDbContext db)
    {
        var card = await db.Cards.FindAsync(id);
        if (card is null)
        {
            return Results.NotFound();
        }

        db.Cards.Remove(card);
        await db.SaveChangesAsync();
        return Results.NoContent();
    }

    /// <summary>
    /// Work out where the card lands from its neighbour ids, rebalancing the target
    /// column first if the neighbours have converged too closely to split.
    /// </summary>
    private static async Task<double> ResolvePosition(
        BoardDbContext db, MoveCardRequest request, Guid excludeCardId, DateTimeOffset now)
    {
        var (above, below) = await NeighbourPositions(db, request);

        if (above is { } a && below is { } b && BoardPosition.NeedsRebalance(a, b))
        {
            await RebalanceColumn(db, request.Status, excludeCardId, now);
            (above, below) = await NeighbourPositions(db, request);
        }

        return BoardPosition.Between(above, below);
    }

    private static async Task<(double? Above, double? Below)> NeighbourPositions(
        BoardDbContext db, MoveCardRequest request)
    {
        var above = request.AboveId is { } aboveId
            ? (await db.Cards.FindAsync(aboveId))?.Position
            : null;
        var below = request.BelowId is { } belowId
            ? (await db.Cards.FindAsync(belowId))?.Position
            : null;
        return (above, below);
    }

    private static async Task RebalanceColumn(
        BoardDbContext db, CardStatus status, Guid excludeCardId, DateTimeOffset now)
    {
        var column = await db.Cards
            .Where(c => c.Status == status && c.Id != excludeCardId)
            .OrderBy(c => c.Position)
            .ToListAsync();

        var positions = BoardPosition.Rebalanced(column.Count).ToList();
        for (var i = 0; i < column.Count; i++)
        {
            column[i].MoveTo(status, positions[i], now);
        }
    }
}
