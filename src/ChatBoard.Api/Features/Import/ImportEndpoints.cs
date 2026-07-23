using ChatBoard.Api.Data;
using ChatBoard.Api.Domain;
using ChatBoard.Api.Features.Cards;
using Microsoft.EntityFrameworkCore;

namespace ChatBoard.Api.Features.Import;

public static class ImportEndpoints
{
    public static void MapImportEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/import/conversations", ImportConversations);
        app.MapGet("/api/import/pending", GetPending);
        app.MapPost("/api/import/{chatUuid}/promote", Promote);
    }

    /// <summary>Upload the export's conversations file. Upserts staging rows by uuid; creates no cards.</summary>
    private static async Task<IResult> ImportConversations(HttpRequest request, BoardDbContext db, TimeProvider clock)
    {
        await using var body = await OpenUploadStream(request);
        var result = ConversationExportReader.Read(body);
        var now = clock.GetUtcNow();

        var uuids = result.Conversations.Select(p => p.ChatUuid).ToList();
        var existing = await db.ImportedChats
            .Where(c => uuids.Contains(c.ChatUuid))
            .ToDictionaryAsync(c => c.ChatUuid);

        foreach (var parsed in result.Conversations)
        {
            if (existing.TryGetValue(parsed.ChatUuid, out var chat))
            {
                chat.Refresh(parsed.Name, parsed.ChatCreatedAt, parsed.ChatUpdatedAt, parsed.MessageCount, now);
            }
            else
            {
                db.ImportedChats.Add(ImportedChat.Create(
                    parsed.ChatUuid, parsed.Name, parsed.ChatCreatedAt, parsed.ChatUpdatedAt, parsed.MessageCount, now));
            }
        }

        await db.SaveChangesAsync();
        return Results.Ok(new ImportResultResponse(result.Conversations.Count, result.Skipped));
    }

    private static async Task<IResult> GetPending(BoardDbContext db)
    {
        var pending = await db.ImportedChats
            .Where(c => c.PromotedCardId == null)
            .OrderByDescending(c => c.ImportedAt)
            .ThenByDescending(c => c.ChatUpdatedAt)
            .Select(c => PendingChatResponse.From(c))
            .ToListAsync();

        return Results.Ok(pending);
    }

    /// <summary>Promote a staged chat to a NeedsAction card. Idempotent: a second call returns the existing card.</summary>
    private static async Task<IResult> Promote(string chatUuid, BoardDbContext db, TimeProvider clock)
    {
        var chat = await db.ImportedChats.FindAsync(chatUuid);
        if (chat is null)
        {
            return Results.NotFound();
        }

        if (chat.PromotedCardId is { } existingCardId)
        {
            var existingCard = await db.Cards.FindAsync(existingCardId);
            return existingCard is null
                ? Results.NotFound()
                : Results.Ok(CardResponse.From(existingCard));
        }

        var now = clock.GetUtcNow();
        var position = await db.AppendPositionAsync(CardStatus.NeedsAction);

        var card = Card.Create(
            chat.Name,
            CardStatus.NeedsAction,
            position,
            now,
            url: ChatUrl(chat.ChatUuid),
            chatUuid: chat.ChatUuid);
        card.RecordChatActivity(chat.ChatUpdatedAt, now);

        db.Cards.Add(card);
        chat.MarkPromoted(card.Id);
        await db.SaveChangesAsync();

        return Results.Created($"/api/cards/{card.Id}", CardResponse.From(card));
    }

    private static async Task<Stream> OpenUploadStream(HttpRequest request)
    {
        // Accept either a multipart file upload or the raw file as the request body.
        if (request.HasFormContentType)
        {
            var form = await request.ReadFormAsync();
            var file = form.Files.GetFile("file") ?? form.Files.FirstOrDefault();
            if (file is not null)
            {
                return file.OpenReadStream();
            }
        }
        return request.Body;
    }

    private static string ChatUrl(string chatUuid) => $"https://claude.ai/chat/{chatUuid}";
}
