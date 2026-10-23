using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using System.Runtime.Versioning;

namespace LeverDeck.App.Desktop;

// The few WebView2 COM interfaces the editor window needs, as source-generated COM (Native AOT safe: no built-in COM
// interop). IIDs and vtable order come from build/native/include/WebView2.h of the Microsoft.Web.WebView2 package
// (Directory.Packages.props). Each interface declares only the prefix of its vtable up to the last method used; the
// methods named Unused* are placeholders that keep the slots in place and are never called. Never reorder them.

/// <summary>The WebView2 loader: statically linked into the AOT exe, <c>WebView2Loader.dll</c> next to a JIT build.</summary>
[SupportedOSPlatform("windows")]
internal static partial class WebView2Loader
{
    /// <summary>HRESULT_FROM_WIN32(ERROR_FILE_NOT_FOUND): the loader found no WebView2 Runtime.</summary>
    public const int RuntimeNotFound = unchecked((int)0x80070002);

    [LibraryImport("WebView2Loader", StringMarshalling = StringMarshalling.Utf16)]
    public static partial int CreateCoreWebView2EnvironmentWithOptions(
        string? browserExecutableFolder,
        string userDataFolder,
        nint environmentOptions,
        ICoreWebView2CreateCoreWebView2EnvironmentCompletedHandler environmentCreatedHandler);
}

[GeneratedComInterface]
[Guid("b96d755e-0319-4e92-a296-23436f46a1fc")]
internal partial interface ICoreWebView2Environment
{
    void CreateCoreWebView2Controller(nint parentWindow, ICoreWebView2CreateCoreWebView2ControllerCompletedHandler handler);
}

[GeneratedComInterface]
[Guid("4e8a3389-c9d8-4bd2-b6b5-124fee6cc14d")]
internal partial interface ICoreWebView2CreateCoreWebView2EnvironmentCompletedHandler
{
    [PreserveSig]
    int Invoke(int errorCode, ICoreWebView2Environment? result);
}

[GeneratedComInterface]
[Guid("6c4819f3-c9b7-4260-8127-c9f5bde7f68c")]
internal partial interface ICoreWebView2CreateCoreWebView2ControllerCompletedHandler
{
    [PreserveSig]
    int Invoke(int errorCode, ICoreWebView2Controller? result);
}

[GeneratedComInterface]
[Guid("4d00c0d1-9434-4eb6-8078-8697a560334f")]
internal partial interface ICoreWebView2Controller
{
    void UnusedGetIsVisible();

    void SetIsVisible([MarshalAs(UnmanagedType.Bool)] bool isVisible);

    void UnusedGetBounds();

    void SetBounds(NativeEditor.Rect bounds);

    void UnusedGetZoomFactor();

    void UnusedPutZoomFactor();

    void UnusedAddZoomFactorChanged();

    void UnusedRemoveZoomFactorChanged();

    void UnusedSetBoundsAndZoomFactor();

    /// <summary>COREWEBVIEW2_MOVE_FOCUS_REASON: 0 is programmatic.</summary>
    void MoveFocus(int reason);

    void UnusedAddMoveFocusRequested();

    void UnusedRemoveMoveFocusRequested();

    void UnusedAddGotFocus();

    void UnusedRemoveGotFocus();

    void UnusedAddLostFocus();

    void UnusedRemoveLostFocus();

    void UnusedAddAcceleratorKeyPressed();

    void UnusedRemoveAcceleratorKeyPressed();

    void UnusedGetParentWindow();

    void UnusedPutParentWindow();

    void UnusedNotifyParentWindowPositionChanged();

    void Close();

    ICoreWebView2 GetCoreWebView2();
}

[GeneratedComInterface(StringMarshalling = StringMarshalling.Utf16)]
[Guid("76eceacb-0462-4d94-ac83-423a6793775e")]
internal partial interface ICoreWebView2
{
    void UnusedGetSettings();

    void UnusedGetSource();

    void Navigate(string uri);
}

/// <summary>Hands the environment-created callback to a delegate. WebView2 may call it before the create call returns.</summary>
[GeneratedComClass]
internal sealed partial class EnvironmentCompletedHandler(Action<int, ICoreWebView2Environment?> completed)
    : ICoreWebView2CreateCoreWebView2EnvironmentCompletedHandler
{
    public int Invoke(int errorCode, ICoreWebView2Environment? result)
    {
        completed(errorCode, result);
        return 0;
    }
}

[GeneratedComClass]
internal sealed partial class ControllerCompletedHandler(Action<int, ICoreWebView2Controller?> completed)
    : ICoreWebView2CreateCoreWebView2ControllerCompletedHandler
{
    public int Invoke(int errorCode, ICoreWebView2Controller? result)
    {
        completed(errorCode, result);
        return 0;
    }
}
