using System.Net.Http.Json;
using ChatBoard.Api.Domain;
using ChatBoard.Api.Features.Cards;

namespace ChatBoard.Api.Tests;

public class MoveEndpointTests : IDisposable
{
    private readonly BoardApiFactory _factory = new();
    private readonly HttpClient _client;

    public MoveEndpointTests() => _client = _factory.CreateClient();

    [Fact]
    public async Task Move_ReorderWithinColumn_PlacesCardBetweenItsNeighbours()
    {
        var a = await CreateCard("Auth redirect");
        var b = await CreateCard("Export reader");
        var c = await CreateCard("Rebalance logic");

        // Move C up to sit between A and B.
        var moved = await Move(c.Id, new MoveCardRequest(CardStatus.NeedsAction, a.Id, b.Id));

        Assert.True(moved.Position > a.Position && moved.Position < b.Position);
        Assert.Equal(CardStatus.NeedsAction, moved.Status);
        Assert.Equal(new[] { a.Id, c.Id, b.Id }, await ColumnOrder(CardStatus.NeedsAction));
    }

    [Fact]
    public async Task Move_AcrossColumns_ChangesStatusAndLeavesTheOldColumn()
    {
        var card = await CreateCard("Waiting on review");

        var moved = await Move(card.Id, new MoveCardRequest(CardStatus.Waiting, null, null));

        Assert.Equal(CardStatus.Waiting, moved.Status);
        Assert.Contains(card.Id, await ColumnOrder(CardStatus.Waiting));
        Assert.DoesNotContain(card.Id, await ColumnOrder(CardStatus.NeedsAction));
    }

    private async Task<CardResponse> CreateCard(string title)
    {
        var response = await _client.PostAsJsonAsync(
            "/api/cards",
            new CreateCardRequest(title, null, null, CardStatus.NeedsAction, null, null),
            BoardApiFactory.Json);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CardResponse>(BoardApiFactory.Json))!;
    }

    private async Task<CardResponse> Move(Guid id, MoveCardRequest request)
    {
        var response = await _client.PatchAsJsonAsync($"/api/cards/{id}/move", request, BoardApiFactory.Json);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CardResponse>(BoardApiFactory.Json))!;
    }

    private async Task<Guid[]> ColumnOrder(CardStatus status)
    {
        var board = await _client.GetFromJsonAsync<BoardResponse>("/api/board?includeClosed=true", BoardApiFactory.Json);
        return board!.Columns.Single(c => c.Status == status).Cards.Select(c => c.Id).ToArray();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }
}
