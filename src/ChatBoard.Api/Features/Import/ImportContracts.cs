using ChatBoard.Api.Domain;

namespace ChatBoard.Api.Features.Import;

/// <summary>A single conversation parsed out of an export file, before it is staged.</summary>
public record ParsedConversation(
    string ChatUuid,
    string Name,
    DateTimeOffset ChatCreatedAt,
    DateTimeOffset ChatUpdatedAt,
    int MessageCount);

/// <summary>The outcome of reading an export: what parsed, and how many records were skipped as malformed.</summary>
public record ConversationExportResult(
    IReadOnlyList<ParsedConversation> Conversations,
    int Skipped);

/// <summary>Response for an upload: how many chats were staged (new + refreshed) and how many were skipped.</summary>
public record ImportResultResponse(int Imported, int Skipped);

/// <summary>A staged chat awaiting a decision to promote.</summary>
public record PendingChatResponse(
    string ChatUuid,
    string Name,
    DateTimeOffset ChatCreatedAt,
    DateTimeOffset ChatUpdatedAt,
    int MessageCount,
    DateTimeOffset ImportedAt)
{
    public static PendingChatResponse From(ImportedChat chat) => new(
        chat.ChatUuid,
        chat.Name,
        chat.ChatCreatedAt,
        chat.ChatUpdatedAt,
        chat.MessageCount,
        chat.ImportedAt);
}
