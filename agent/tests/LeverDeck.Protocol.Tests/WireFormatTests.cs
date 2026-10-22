using System.Text.Json;
using LeverDeck.Protocol.Dtos;
using LeverDeck.Protocol.Messages;

namespace LeverDeck.Protocol.Tests;

/// <summary>Golden JSON for the exact wire shape documented in docs/ROADMAP.md (Phase 3.2 and 3.3).</summary>
public sealed class WireFormatTests
{
    private const string ProfileId = "11111111-1111-1111-1111-111111111111";
    private const string ButtonId = "22222222-2222-2222-2222-222222222222";
    private const string DeviceId = "33333333-3333-3333-3333-333333333333";
    private const string AppId = "44444444-4444-4444-4444-444444444444";

    [Fact]
    public void Command_PutsKindTypeRequestIdAndPayloadAtTheTop()
    {
        Message message = new ExecuteActionCommand("r2", new ExecuteActionPayload(Samples.ProfileId, 0, Samples.ButtonId));

        Assert.Equal(
            $$$"""{"kind":"command","type":"executeAction","requestId":"r2","payload":{"profileId":"{{{ProfileId}}}","pageIndex":0,"buttonId":"{{{ButtonId}}}"}}""",
            Json.Serialize(message));
    }

    [Fact]
    public void EmptyPayload_IsAnEmptyObject()
    {
        Assert.Equal(
            """{"kind":"query","type":"getProfiles","requestId":"q1","payload":{}}""",
            Json.Serialize(new GetProfilesQuery("q1")));
    }

    [Fact]
    public void Ack_OmitsPayloadUnlessItCarriesOne()
    {
        Assert.Equal("""{"kind":"ack","requestId":"r1"}""", Json.Serialize(new AckMessage("r1")));
        Assert.Equal(
            $$$"""{"kind":"ack","requestId":"r10","payload":{"appId":"{{{AppId}}}"}}""",
            Json.Serialize(new AckMessage("r10", new AppRegisteredPayload(Samples.AppId))));
    }

    [Fact]
    public void Error_CarriesCamelCaseCodeAndMessage()
    {
        Assert.Equal(
            """{"kind":"error","requestId":"r2","code":"pairingCodeExpired","message":"Expired."}""",
            Json.Serialize(new ErrorMessage("r2", ErrorCodeDto.PairingCodeExpired, "Expired.")));
    }

    [Fact]
    public void QueryResult_WritesNullSessionExplicitly()
    {
        Assert.Equal(
            """{"kind":"queryResult","type":"pairingSession","requestId":"q4","payload":{"session":null}}""",
            Json.Serialize(new PairingSessionResult("q4", new PairingSessionPayload(null))));
    }

    [Fact]
    public void Event_HasNoRequestId()
    {
        Assert.Equal(
            $$$"""{"kind":"event","type":"deviceRevoked","payload":{"deviceId":"{{{DeviceId}}}"}}""",
            Json.Serialize(new DeviceRevokedEvent(new DeviceIdPayload(Samples.DeviceId))));
    }

    [Fact]
    public void Action_IsDiscriminatedByTypeWithCamelCaseEnums()
    {
        Assert.Equal(
            """{"type":"hotkey","modifiers":["ctrl","shift"],"key":"pageUp"}""",
            Json.Serialize(Samples.Hotkey));
        Assert.Equal(
            """{"type":"changePage","target":"next","pageIndex":null}""",
            Json.Serialize(new ChangePageActionDto(PageTargetDto.Next, null)));
    }

    [Fact]
    public void Dates_AreIso8601UtcWithMilliseconds()
    {
        var device = Samples.Device with { LastSeenAt = new DateTimeOffset(2026, 10, 22, 12, 30, 0, 125, TimeSpan.FromHours(2)) };

        Assert.Equal(
            $$$"""{"id":"{{{DeviceId}}}","name":"Pixel","pairedAt":"2026-10-22T08:30:00.125Z","lastSeenAt":"2026-10-22T10:30:00.125Z","connected":true,"revoked":false}""",
            Json.Serialize(device));
    }

    [Fact]
    public void Settings_ExposeOnlyWhetherAnObsPasswordIsSet()
    {
        var json = Json.Serialize(Samples.Settings);

        Assert.Contains("\"obs\":{\"host\":\"localhost\",\"port\":4455,\"hasPassword\":true}", json, StringComparison.Ordinal);
        Assert.DoesNotContain("password\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Read_AcceptsPropertiesInAnyOrder()
    {
        var json = $$$"""{"payload":{"buttonId":"{{{ButtonId}}}","pageIndex":1,"profileId":"{{{ProfileId}}}"},"type":"executeAction","requestId":"r2","kind":"command"}""";

        var message = Json.Deserialize<Message>(json);

        Assert.Equal(new ExecuteActionCommand("r2", new ExecuteActionPayload(Samples.ProfileId, 1, Samples.ButtonId)), message);
    }

    [Fact]
    public void Read_NormalizesDatesToUtc()
    {
        var json = $$$"""{"id":"{{{DeviceId}}}","name":"Pixel","pairedAt":"2026-10-22T10:30:00.125+02:00","lastSeenAt":"2026-10-22T08:30:00.125Z","connected":false,"revoked":true}""";

        var device = Json.Deserialize<DeviceDto>(json);

        Assert.Equal(Samples.PairedAt, device.PairedAt);
        Assert.Equal(TimeSpan.Zero, device.PairedAt.Offset);
    }

    [Theory]
    [InlineData("""{"kind":"shout","requestId":"r1"}""")]
    [InlineData("""{"requestId":"r1"}""")]
    [InlineData("""{"kind":"command","requestId":"r1","payload":{}}""")]
    [InlineData("""{"kind":"command","type":"launchMissiles","requestId":"r1","payload":{}}""")]
    [InlineData("""{"kind":"query","type":"executeAction","requestId":"r1","payload":{}}""")]
    [InlineData("""{"kind":"command","type":"deleteProfile","payload":{"profileId":"11111111-1111-1111-1111-111111111111"}}""")]
    [InlineData("""{"kind":"command","type":"deleteProfile","requestId":null,"payload":{"profileId":"11111111-1111-1111-1111-111111111111"}}""")]
    [InlineData("""{"kind":"command","type":"saveProfile","requestId":"r1","payload":{"profile":null}}""")]
    [InlineData("""{"kind":"error","requestId":"r1","code":3,"message":"x"}""")]
    [InlineData("""[]""")]
    public void Read_RejectsMalformedMessagesWithJsonException(string json)
    {
        Assert.Throws<JsonException>(() => Json.Deserialize<Message>(json));
    }

    [Fact]
    public void Read_RejectsAnUnknownActionType()
    {
        Assert.Throws<JsonException>(() => Json.Deserialize<ActionDto>("""{"type":"runCommand","command":"format c:"}"""));
    }
}
