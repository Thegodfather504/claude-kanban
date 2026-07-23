namespace ChatBoard.Api.Domain;

/// <summary>
/// A chat (or manual note) tracked on the board. Behavioural, not a data bag:
/// callers command it through the mutators below, which own their own invariants
/// and bump <see cref="UpdatedAt"/> via <see cref="Touch"/>. Nothing sets a
/// property from the outside.
/// </summary>
public class Card
{
    private readonly List<string> _tags = new();

    // EF materialisation only.
    private Card() { }

    public Guid Id { get; private set; }

    /// <summary>The <c>claude.ai/chat/&lt;uuid&gt;</c> segment. Unique when present; manual cards may have none.</summary>
    public string? ChatUuid { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public string? Url { get; private set; }

    public CardStatus Status { get; private set; }

    /// <summary>The highest-value field on the card: what actually needs doing, in my words.</summary>
    public string? NextAction { get; private set; }

    /// <summary>Sort key within the column. See <see cref="BoardPosition"/>.</summary>
    public double Position { get; private set; }

    public IReadOnlyList<string> Tags => _tags;

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>Last activity in the underlying chat, set by import. Distinct from <see cref="UpdatedAt"/>, which tracks the card.</summary>
    public DateTimeOffset? ChatLastActiveAt { get; private set; }

    /// <summary>Create a card. Title is required (trimmed, non-empty); status and position are placed by the caller.</summary>
    public static Card Create(
        string title,
        CardStatus status,
        double position,
        DateTimeOffset now,
        string? url = null,
        string? chatUuid = null,
        string? nextAction = null,
        IEnumerable<string>? tags = null)
    {
        var card = new Card
        {
            Id = Guid.NewGuid(),
            Status = status,
            Position = position,
            ChatUuid = Normalize(chatUuid),
            Url = Normalize(url),
            NextAction = Normalize(nextAction),
            CreatedAt = now,
            UpdatedAt = now
        };
        card.Title = RequireTitle(title);
        if (tags is not null)
        {
            card._tags.AddRange(CleanTags(tags));
        }
        return card;
    }

    /// <summary>Change status and reorder in one operation — on the board they are the same move.</summary>
    public void MoveTo(CardStatus status, double position, DateTimeOffset now)
    {
        Status = status;
        Position = position;
        Touch(now);
    }

    /// <summary>Edit the card's details (not its status or board position).</summary>
    public void Describe(string title, string? url, string? nextAction, DateTimeOffset now)
    {
        Title = RequireTitle(title);
        Url = Normalize(url);
        NextAction = Normalize(nextAction);
        Touch(now);
    }

    /// <summary>Replace the full tag set.</summary>
    public void ReplaceTags(IEnumerable<string> tags, DateTimeOffset now)
    {
        _tags.Clear();
        _tags.AddRange(CleanTags(tags));
        Touch(now);
    }

    /// <summary>Record activity observed in the underlying chat, without disturbing anything else on the card.</summary>
    public void RecordChatActivity(DateTimeOffset lastActiveAt, DateTimeOffset now)
    {
        ChatLastActiveAt = lastActiveAt;
        Touch(now);
    }

    private void Touch(DateTimeOffset now) => UpdatedAt = now;

    private static string RequireTitle(string title)
    {
        var trimmed = title?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            throw new ArgumentException("Title is required and cannot be empty or whitespace.", nameof(title));
        }
        return trimmed;
    }

    private static string? Normalize(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    private static IEnumerable<string> CleanTags(IEnumerable<string> tags) =>
        tags.Select(t => t?.Trim())
            .Where(t => !string.IsNullOrEmpty(t))
            .Select(t => t!)
            .Distinct(StringComparer.OrdinalIgnoreCase);
}
