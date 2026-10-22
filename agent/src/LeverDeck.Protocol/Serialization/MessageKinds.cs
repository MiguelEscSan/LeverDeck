using LeverDeck.Protocol.Messages;

namespace LeverDeck.Protocol.Serialization;

/// <summary>A value of the envelope's <c>kind</c> field and the message type it selects.</summary>
public readonly record struct MessageKind(string Kind, Type Type);

/// <summary>Every envelope kind. The converter reads and writes <c>kind</c> from this table; the TypeScript generator does too.</summary>
public static class MessageKinds
{
    public const string Command = "command";
    public const string Query = "query";
    public const string Ack = "ack";
    public const string Error = "error";
    public const string QueryResult = "queryResult";
    public const string Event = "event";

    public static IReadOnlyList<MessageKind> All { get; } =
    [
        new(Command, typeof(CommandMessage)),
        new(Query, typeof(QueryMessage)),
        new(Ack, typeof(AckMessage)),
        new(Error, typeof(ErrorMessage)),
        new(QueryResult, typeof(QueryResultMessage)),
        new(Event, typeof(EventMessage)),
    ];
}
