using System.Text.Json.Serialization;
using LeverDeck.Protocol.Serialization;

namespace LeverDeck.Protocol.Dtos;

// Every enum travels as a camelCase string (for example "pageUp"); integers are rejected.

[JsonConverter(typeof(CamelCaseEnumConverter<KeyColorDto>))]
public enum KeyColorDto
{
    Red,
    Blue,
    Orange,
    Purple,
    Teal,
    Dark,
}

[JsonConverter(typeof(CamelCaseEnumConverter<KeyModifierDto>))]
public enum KeyModifierDto
{
    Ctrl,
    Shift,
    Alt,
    Win,
}

/// <summary>Allow-list of keys a hotkey may press; mirrors the domain <c>VirtualKey</c>.</summary>
[JsonConverter(typeof(CamelCaseEnumConverter<VirtualKeyDto>))]
public enum VirtualKeyDto
{
    A,
    B,
    C,
    D,
    E,
    F,
    G,
    H,
    I,
    J,
    K,
    L,
    M,
    N,
    O,
    P,
    Q,
    R,
    S,
    T,
    U,
    V,
    W,
    X,
    Y,
    Z,
    D0,
    D1,
    D2,
    D3,
    D4,
    D5,
    D6,
    D7,
    D8,
    D9,
    F1,
    F2,
    F3,
    F4,
    F5,
    F6,
    F7,
    F8,
    F9,
    F10,
    F11,
    F12,
    F13,
    F14,
    F15,
    F16,
    F17,
    F18,
    F19,
    F20,
    F21,
    F22,
    F23,
    F24,
    Up,
    Down,
    Left,
    Right,
    Enter,
    Escape,
    Space,
    Tab,
    Backspace,
    Delete,
    Insert,
    Home,
    End,
    PageUp,
    PageDown,
    PrintScreen,
}

[JsonConverter(typeof(CamelCaseEnumConverter<MediaKeyDto>))]
public enum MediaKeyDto
{
    PlayPause,
    Next,
    Previous,
    Stop,
    VolumeUp,
    VolumeDown,
    Mute,
}

[JsonConverter(typeof(CamelCaseEnumConverter<GameStoreDto>))]
public enum GameStoreDto
{
    Steam,
    Epic,
}

/// <summary>Where a <c>changePage</c> action goes; <c>specific</c> needs a <c>pageIndex</c>.</summary>
[JsonConverter(typeof(CamelCaseEnumConverter<PageTargetDto>))]
public enum PageTargetDto
{
    Next,
    Previous,
    Specific,
}

/// <summary>Error codes the agent sends; several domain errors may share one wire code.</summary>
[JsonConverter(typeof(CamelCaseEnumConverter<ErrorCodeDto>))]
public enum ErrorCodeDto
{
    Unauthorized,
    Forbidden,
    ProtocolMismatch,
    ValidationFailed,
    NotFound,
    PairingCodeInvalid,
    PairingCodeExpired,
    DeviceRevoked,
    ActionFailed,
    Internal,
}
