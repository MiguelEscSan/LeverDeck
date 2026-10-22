namespace LeverDeck.Protocol.Tests;

public sealed class RoundTripTests
{
    public static TheoryData<int, object> Cases()
    {
        var data = new TheoryData<int, object>();
        var index = 0;
        foreach (var sample in Samples.All)
        {
            data.Add(index++, sample);
        }

        return data;
    }

    // The index keeps every case distinct in the test explorer, even for two samples of the same type.
    [Theory]
    [MemberData(nameof(Cases))]
    public void Value_SurvivesRoundTrip(int index, object value)
    {
        var json = Json.Serialize(value);

        var back = Json.Deserialize(json, Json.RootOf(value));

        Assert.True(index >= 0);
        Assert.NotNull(back);
        Assert.IsType(value.GetType(), back);
        Assert.Equal(json, Json.Serialize(back));
    }
}
