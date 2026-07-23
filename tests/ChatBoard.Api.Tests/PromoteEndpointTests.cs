using System.Net;
using System.Net.Http.Json;
using ChatBoard.Api.Domain;
using ChatBoard.Api.Features.Cards;

namespace ChatBoard.Api.Tests;

public class PromoteEndpointTests : IDisposable
{
    private const string ChatUuid = "chat-123";
    private readonly BoardApiFactory _factory = new();
    private readonly HttpClient _client;

    public PromoteEndpointTests() => _client = _factory.CreateClient();

    [Fact]
    public async Task Promote_IsIdempotent_SecondCallReturnsSameCardAndCreatesNoDuplicate()
    {
        await StageChat();

        var first = await Promote();
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        var firstCard = (await first.Content.ReadFromJsonAsync<CardResponse>(BoardApiFactory.Json))!;
        Assert.Equal(CardStatus.NeedsAction, firstCard.Status);

        var second = await Promote();
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var secondCard = (await second.Content.ReadFromJsonAsync<CardResponse>(BoardApiFactory.Json))!;

        Assert.Equal(firstCard.Id, secondCard.Id);

        await _factory.WithDbAsync(async db =>
        {
            Assert.Equal(1, db.Cards.Count(c => c.ChatUuid == ChatUuid));
            var chat = await db.ImportedChats.FindAsync(ChatUuid);
            Assert.Equal(firstCard.Id, chat!.PromotedCardId);
        });
    }

    [Fact]
    public async Task Promote_DropsThePromotedChatFromThePendingList()
    {
        await StageChat();

        await Promote();

        var pending = await _client.GetFromJsonAsync<List<PendingChatResponse>>("/api/import/pending", BoardApiFactory.Json);
        Assert.DoesNotContain(pending!, c => c.ChatUuid == ChatUuid);
    }

    private async Task StageChat()
    {
        await _factory.WithDbAsync(async db =>
        {
            db.ImportedChats.Add(ImportedChat.Create(
                ChatUuid,
                "Auth redirect thread",
                new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero),
                12,
                new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero)));
            await db.SaveChangesAsync();
        });
    }

    private Task<HttpResponseMessage> Promote() =>
        _client.PostAsync($"/api/import/{ChatUuid}/promote", null);

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }
}
