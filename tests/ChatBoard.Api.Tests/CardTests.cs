using ChatBoard.Api.Domain;

namespace ChatBoard.Api.Tests;

public class CardTests
{
    private static readonly DateTimeOffset T0 = new(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);

    private static Card NewCard(DateTimeOffset now, string title = "Fix the auth redirect") =>
        Card.Create(title, CardStatus.NeedsAction, BoardPosition.Gap, now);

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n")]
    public void Create_WithEmptyOrWhitespaceTitle_Throws(string title)
    {
        Assert.Throws<ArgumentException>(() =>
            Card.Create(title, CardStatus.NeedsAction, BoardPosition.Gap, T0));
    }

    [Fact]
    public void Create_TrimsTitle()
    {
        var card = Card.Create("  Ship the export reader  ", CardStatus.NeedsAction, BoardPosition.Gap, T0);
        Assert.Equal("Ship the export reader", card.Title);
    }

    [Fact]
    public void MoveTo_SetsStatusAndPosition_AndBumpsUpdatedAt()
    {
        var clock = new TestClock(T0);
        var card = NewCard(clock.GetUtcNow());
        clock.Advance(TimeSpan.FromHours(3));

        card.MoveTo(CardStatus.Waiting, 4096d, clock.GetUtcNow());

        Assert.Equal(CardStatus.Waiting, card.Status);
        Assert.Equal(4096d, card.Position);
        Assert.Equal(clock.GetUtcNow(), card.UpdatedAt);
        Assert.NotEqual(card.CreatedAt, card.UpdatedAt);
    }

    [Fact]
    public void Describe_BumpsUpdatedAt()
    {
        var clock = new TestClock(T0);
        var card = NewCard(clock.GetUtcNow());
        clock.Advance(TimeSpan.FromDays(1));

        card.Describe("Renamed", "https://claude.ai/chat/abc", "reply to the reviewer", clock.GetUtcNow());

        Assert.Equal("Renamed", card.Title);
        Assert.Equal(clock.GetUtcNow(), card.UpdatedAt);
    }

    [Fact]
    public void RecordChatActivity_SetsChatLastActiveAt_WithoutDisturbingAnUnrelatedField()
    {
        var clock = new TestClock(T0);
        var card = NewCard(clock.GetUtcNow());
        var originalNextAction = card.NextAction;
        var originalStatus = card.Status;
        var originalPosition = card.Position;
        var lastActive = new DateTimeOffset(2026, 6, 30, 12, 0, 0, TimeSpan.Zero);
        clock.Advance(TimeSpan.FromMinutes(5));

        card.RecordChatActivity(lastActive, clock.GetUtcNow());

        Assert.Equal(lastActive, card.ChatLastActiveAt);
        Assert.Equal(clock.GetUtcNow(), card.UpdatedAt);
        // Unrelated fields are untouched.
        Assert.Equal(originalNextAction, card.NextAction);
        Assert.Equal(originalStatus, card.Status);
        Assert.Equal(originalPosition, card.Position);
    }

    [Fact]
    public void ReplaceTags_DeduplicatesTrimsAndDropsEmpties()
    {
        var clock = new TestClock(T0);
        var card = NewCard(clock.GetUtcNow());

        card.ReplaceTags(new[] { " work ", "work", "", "  ", "urgent" }, clock.GetUtcNow());

        Assert.Equal(new[] { "work", "urgent" }, card.Tags);
    }
}
