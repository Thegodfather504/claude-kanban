using System.Text.Json;

namespace ChatBoard.Api.Features.Import;

/// <summary>
/// Parses a claude.ai "Export Data" conversations file into staging rows. The format
/// drifts in the wild, so this is deliberately tolerant: it sniffs the first
/// non-whitespace character to tell a JSON array (<c>[</c>) from JSONL (one object per
/// line), skips malformed records rather than failing the whole import, and reports
/// how many it skipped.
/// </summary>
public static class ConversationExportReader
{
    public static ConversationExportResult Read(Stream stream)
    {
        using var reader = new StreamReader(stream);
        return Read(reader.ReadToEnd());
    }

    public static ConversationExportResult Read(string content)
    {
        var firstChar = FirstNonWhitespace(content);
        return firstChar == '['
            ? ReadJsonArray(content)
            : ReadJsonLines(content);
    }

    private static char? FirstNonWhitespace(string content)
    {
        foreach (var ch in content)
        {
            if (!char.IsWhiteSpace(ch))
            {
                return ch;
            }
        }
        return null;
    }

    private static ConversationExportResult ReadJsonArray(string content)
    {
        var conversations = new List<ParsedConversation>();
        var skipped = 0;

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(content);
        }
        catch (JsonException)
        {
            // A whole array we can't even parse: nothing recovered, everything skipped is unknowable — treat as empty.
            return new ConversationExportResult(conversations, 0);
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return new ConversationExportResult(conversations, 0);
            }

            foreach (var element in document.RootElement.EnumerateArray())
            {
                if (TryParse(element, out var conversation))
                {
                    conversations.Add(conversation);
                }
                else
                {
                    skipped++;
                }
            }
        }

        return new ConversationExportResult(conversations, skipped);
    }

    private static ConversationExportResult ReadJsonLines(string content)
    {
        var conversations = new List<ParsedConversation>();
        var skipped = 0;

        foreach (var rawLine in content.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0)
            {
                continue;
            }

            try
            {
                using var document = JsonDocument.Parse(line);
                if (TryParse(document.RootElement, out var conversation))
                {
                    conversations.Add(conversation);
                }
                else
                {
                    skipped++;
                }
            }
            catch (JsonException)
            {
                skipped++;
            }
        }

        return new ConversationExportResult(conversations, skipped);
    }

    /// <summary>A record parses only if it has a usable uuid; every other field tolerates absence.</summary>
    private static bool TryParse(JsonElement element, out ParsedConversation conversation)
    {
        conversation = default!;

        if (element.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        var uuid = GetString(element, "uuid");
        if (string.IsNullOrWhiteSpace(uuid))
        {
            return false;
        }

        conversation = new ParsedConversation(
            uuid!.Trim(),
            GetString(element, "name") ?? string.Empty,
            GetDate(element, "created_at"),
            GetDate(element, "updated_at"),
            CountMessages(element));
        return true;
    }

    private static string? GetString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static DateTimeOffset GetDate(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value)
        && value.ValueKind == JsonValueKind.String
        && DateTimeOffset.TryParse(value.GetString(), out var parsed)
            ? parsed
            : DateTimeOffset.UnixEpoch;

    private static int CountMessages(JsonElement element) =>
        element.TryGetProperty("chat_messages", out var messages) && messages.ValueKind == JsonValueKind.Array
            ? messages.GetArrayLength()
            : 0;
}
