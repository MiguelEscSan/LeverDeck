using LeverDeck.Domain.Apps;
using LeverDeck.Domain.Common;
using LeverDeck.Domain.Devices;
using LeverDeck.Domain.Profiles;

namespace LeverDeck.Infrastructure.ProtocolMapping;

/// <summary>Wire ids to domain ids; the empty GUID becomes a <c>validationFailed</c> result.</summary>
public static class IdMapper
{
    public static Result<ProfileId> ToProfileId(Guid id) => MappingBoundary.Run(() => new ProfileId(id));

    public static Result<ButtonId> ToButtonId(Guid id) => MappingBoundary.Run(() => new ButtonId(id));

    public static Result<DeviceId> ToDeviceId(Guid id) => MappingBoundary.Run(() => new DeviceId(id));

    public static Result<AppId> ToAppId(Guid id) => MappingBoundary.Run(() => new AppId(id));
}
