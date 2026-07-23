using ChatBoard.Api.Domain;

namespace ChatBoard.Api.Features.Cards;

/// <summary>
/// DTOs for the card API. Kept separate from the entities so the wire contract does
/// not move every time the schema does.
/// </summary>
public record CreateCardRequest(
    string Title,
    string? Url,
    string? ChatUuid,
    CardStatus? Status,
    string? NextAction,
    IReadOnlyList<string>? Tags);

public record UpdateCardRequest(
    string Title,
    string? Url,
    string? NextAction,
    IReadOnlyList<string> Tags);

/// <summary>Move is status-change and reorder in one: place the card between two neighbours.</summary>
public record MoveCardRequest(
    CardStatus Status,
    Guid? AboveId,
    Guid? BelowId);

public record CardResponse(
    Guid Id,
    string? ChatUuid,
    string Title,
    string? Url,
    CardStatus Status,
    string? NextAction,
    double Position,
    IReadOnlyList<string> Tags,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? ChatLastActiveAt)
{
    public static CardResponse From(Card card) => new(
        card.Id,
        card.ChatUuid,
        card.Title,
        card.Url,
        card.Status,
        card.NextAction,
        card.Position,
        card.Tags,
        card.CreatedAt,
        card.UpdatedAt,
        card.ChatLastActiveAt);
}

/// <summary>One column of the board.</summary>
public record BoardColumn(CardStatus Status, IReadOnlyList<CardResponse> Cards);

public record BoardResponse(IReadOnlyList<BoardColumn> Columns);
