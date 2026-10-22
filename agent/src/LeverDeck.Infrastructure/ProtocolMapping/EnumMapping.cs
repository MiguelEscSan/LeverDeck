namespace LeverDeck.Infrastructure.ProtocolMapping;

/// <summary>
/// Maps a wire enum to its domain twin (and back) by member name. Tests assert both sides declare the same names,
/// so a rename on one side fails the build of the tests instead of silently shifting values.
/// </summary>
internal static class EnumMapping
{
    public static TTo Convert<TFrom, TTo>(TFrom value)
        where TFrom : struct, Enum
        where TTo : struct, Enum
    {
        if (!Enum.IsDefined(value)
            || !Enum.TryParse<TTo>(value.ToString(), ignoreCase: false, out var converted)
            || !Enum.IsDefined(converted))
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, $"Unknown {typeof(TFrom).Name} value '{value}'.");
        }

        return converted;
    }
}
