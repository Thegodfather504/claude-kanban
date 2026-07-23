namespace ChatBoard.Api.Domain;

/// <summary>
/// Where a chat sits on the "who is blocking" axis. Persisted as a string so the
/// SQLite file stays readable by hand.
/// </summary>
public enum CardStatus
{
    /// <summary>Ball is in my court.</summary>
    NeedsAction,

    /// <summary>Blocked on someone or something else.</summary>
    Waiting,

    /// <summary>Keep findable, requires nothing.</summary>
    Reference,

    /// <summary>Loop closed.</summary>
    Closed
}
