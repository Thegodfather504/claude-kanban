namespace ChatBoard.Api.Domain;

/// <summary>
/// A conversation staged from an export file. Import fills this table; it does not
/// create cards. I promote the chats I care about — most need nothing, and a card
/// per conversation would flood the board.
/// </summary>
public class ImportedChat
{
    // EF materialisation only.
    private ImportedChat() { }

    public string ChatUuid { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public DateTimeOffset ChatCreatedAt { get; private set; }

    public DateTimeOffset ChatUpdatedAt { get; private set; }

    public int MessageCount { get; private set; }

    public DateTimeOffset ImportedAt { get; private set; }

    /// <summary>Null until a card is made from this chat.</summary>
    public Guid? PromotedCardId { get; private set; }

    public static ImportedChat Create(
        string chatUuid,
        string name,
        DateTimeOffset chatCreatedAt,
        DateTimeOffset chatUpdatedAt,
        int messageCount,
        DateTimeOffset importedAt)
    {
        return new ImportedChat
        {
            ChatUuid = chatUuid,
            Name = name,
            ChatCreatedAt = chatCreatedAt,
            ChatUpdatedAt = chatUpdatedAt,
            MessageCount = messageCount,
            ImportedAt = importedAt
        };
    }

    /// <summary>Refresh the staged snapshot from a fresh export, keeping any existing promotion.</summary>
    public void Refresh(
        string name,
        DateTimeOffset chatCreatedAt,
        DateTimeOffset chatUpdatedAt,
        int messageCount,
        DateTimeOffset importedAt)
    {
        Name = name;
        ChatCreatedAt = chatCreatedAt;
        ChatUpdatedAt = chatUpdatedAt;
        MessageCount = messageCount;
        ImportedAt = importedAt;
    }

    /// <summary>Mark this chat as promoted to a card. Idempotent-friendly: callers check <see cref="PromotedCardId"/> first.</summary>
    public void MarkPromoted(Guid cardId) => PromotedCardId = cardId;
}
