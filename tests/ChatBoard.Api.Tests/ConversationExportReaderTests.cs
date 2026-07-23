using ChatBoard.Api.Features.Import;

namespace ChatBoard.Api.Tests;

public class ConversationExportReaderTests
{
    [Fact]
    public void Read_JsonArrayInput_ParsesEveryConversation()
    {
        const string json = """
        [
          { "uuid": "aaa", "name": "Auth redirect", "created_at": "2026-01-01T10:00:00Z", "updated_at": "2026-01-02T10:00:00Z", "chat_messages": [ {}, {}, {} ] },
          { "uuid": "bbb", "name": "Export reader", "created_at": "2026-01-03T10:00:00Z", "updated_at": "2026-01-04T10:00:00Z", "chat_messages": [ {} ] }
        ]
        """;

        var result = ConversationExportReader.Read(json);

        Assert.Equal(0, result.Skipped);
        Assert.Collection(result.Conversations,
            c => { Assert.Equal("aaa", c.ChatUuid); Assert.Equal("Auth redirect", c.Name); Assert.Equal(3, c.MessageCount); },
            c => { Assert.Equal("bbb", c.ChatUuid); Assert.Equal(1, c.MessageCount); });
        Assert.Equal(new DateTimeOffset(2026, 1, 2, 10, 0, 0, TimeSpan.Zero), result.Conversations[0].ChatUpdatedAt);
    }

    [Fact]
    public void Read_JsonLinesInput_ParsesOneObjectPerLine()
    {
        // Leading whitespace, then one object per line (no enclosing array).
        const string jsonl = "\n  {\"uuid\":\"aaa\",\"name\":\"One\",\"chat_messages\":[{},{}]}\n{\"uuid\":\"bbb\",\"name\":\"Two\",\"chat_messages\":[]}\n";

        var result = ConversationExportReader.Read(jsonl);

        Assert.Equal(0, result.Skipped);
        Assert.Equal(2, result.Conversations.Count);
        Assert.Equal("aaa", result.Conversations[0].ChatUuid);
        Assert.Equal(2, result.Conversations[0].MessageCount);
        Assert.Equal(0, result.Conversations[1].MessageCount);
    }

    [Fact]
    public void Read_WithMissingOptionalFields_StillParsesUsingDefaults()
    {
        // Only uuid present: name, dates and messages all absent.
        const string json = """[ { "uuid": "only-uuid" } ]""";

        var result = ConversationExportReader.Read(json);

        var conversation = Assert.Single(result.Conversations);
        Assert.Equal("only-uuid", conversation.ChatUuid);
        Assert.Equal(string.Empty, conversation.Name);
        Assert.Equal(0, conversation.MessageCount);
        Assert.Equal(DateTimeOffset.UnixEpoch, conversation.ChatCreatedAt);
        Assert.Equal(0, result.Skipped);
    }

    [Fact]
    public void Read_MalformedRecords_AreSkippedAndCounted_WithoutLosingGoodOnes()
    {
        // Line 2 is not valid JSON; line 3 is valid JSON but has no uuid. Both are skipped.
        const string jsonl =
            "{\"uuid\":\"good\",\"name\":\"Keep me\"}\n" +
            "{ this is not json \n" +
            "{\"name\":\"No uuid here\"}\n";

        var result = ConversationExportReader.Read(jsonl);

        var kept = Assert.Single(result.Conversations);
        Assert.Equal("good", kept.ChatUuid);
        Assert.Equal(2, result.Skipped);
    }

    [Fact]
    public void Read_ArrayWithMalformedElement_SkipsAndCountsIt()
    {
        // A non-object element and an object missing its uuid are both unusable.
        const string json = """[ { "uuid": "good" }, 42, { "name": "no uuid" } ]""";

        var result = ConversationExportReader.Read(json);

        Assert.Single(result.Conversations);
        Assert.Equal(2, result.Skipped);
    }
}
