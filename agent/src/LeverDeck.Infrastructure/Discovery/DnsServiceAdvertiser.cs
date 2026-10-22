using System.ComponentModel;
using System.Net;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using LeverDeck.Application.Common.Ports;
using LeverDeck.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Windows.Win32;
using Windows.Win32.NetworkManagement.Dns;

namespace LeverDeck.Infrastructure.Discovery;

/// <summary>
/// Advertises the agent over mDNS with the native Windows 10+ responder (<c>DnsServiceRegister</c> in dnsapi.dll):
/// a <c>_http._tcp</c> service instance whose SRV target is <see cref="HostName"/>, carrying the LAN IPv4 address and
/// <see cref="LeverDeckOptions.Port"/>. Windows withdraws the registration itself when the process exits.
/// </summary>
public sealed partial class DnsServiceAdvertiser(ILogger<DnsServiceAdvertiser> logger) : IServiceAdvertiser, IDisposable
{
    public const string HostName = "leverdeck.local";

    public const string ServiceName = "LeverDeck._http._tcp.local";

    // The DnsService* APIs exist from Windows 10 (build 10240).
    private const string MinimumWindows = "windows10.0.10240";

    private const uint RequestVersion1 = 1; // DNS_QUERY_REQUEST_VERSION1
    private const uint Success = 0;

    private static readonly TimeSpan CallbackTimeout = TimeSpan.FromSeconds(10);

    private readonly SemaphoreSlim _gate = new(1, 1);
    private nint _instance; // DNS_SERVICE_INSTANCE* of the live registration, or 0

    public async Task AdvertiseAsync(CancellationToken cancellationToken)
    {
        if (!IsSupported)
        {
            throw NotSupported();
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_instance != 0)
            {
                return;
            }

            var address = LanAddressSelector.Select(LanAddressSelector.FromSystem())
                ?? throw new InvalidOperationException("No LAN IPv4 address is available to advertise.");
            var instance = Construct(address);
            var status = await CallAsync(instance, register: true, cancellationToken).ConfigureAwait(false);
            if (status != Success)
            {
                Free(instance);
                throw new Win32Exception((int)status, $"DnsServiceRegister failed with status {status}.");
            }

            _instance = instance;
            LogAdvertised(HostName, address, LeverDeckOptions.Port);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task WithdrawAsync(CancellationToken cancellationToken)
    {
        if (!IsSupported)
        {
            throw NotSupported();
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_instance == 0)
            {
                return;
            }

            var instance = _instance;
            _instance = 0;
            var status = await CallAsync(instance, register: false, cancellationToken).ConfigureAwait(false);
            Free(instance);
            if (status != Success)
            {
                throw new Win32Exception((int)status, $"DnsServiceDeRegister failed with status {status}.");
            }

            LogWithdrawn(HostName);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Releases the lock only: the registration itself ends with the process.</summary>
    public void Dispose() => _gate.Dispose();

    [SupportedOSPlatformGuard(MinimumWindows)]
    private static bool IsSupported => OperatingSystem.IsWindowsVersionAtLeast(10, 0, 10240);

    private static PlatformNotSupportedException NotSupported() => new("mDNS advertisement needs Windows 10 or later.");

    [SupportedOSPlatform(MinimumWindows)]
    private static unsafe nint Construct(IPAddress address)
    {
        var instance = PInvoke.DnsServiceConstructInstance(
            ServiceName,
            HostName,
            LanAddressSelector.ToIp4Address(address),
            pIp6: null,
            LeverDeckOptions.Port,
            wPriority: 0,
            wWeight: 0,
            keys: [],
            values: []);
        return instance is null
            ? throw new InvalidOperationException("DnsServiceConstructInstance returned no instance.")
            : (nint)instance;
    }

    [SupportedOSPlatform(MinimumWindows)]
    private static unsafe void Free(nint instance) => PInvoke.DnsServiceFreeInstance((DNS_SERVICE_INSTANCE*)instance);

    /// <summary>
    /// Starts a registration or deregistration and waits for its completion callback. The request lives in native
    /// memory and the context in a GC handle, both released by the callback, because Windows may call back after this
    /// method has given up waiting.
    /// </summary>
    [SupportedOSPlatform(MinimumWindows)]
    private static unsafe Task<uint> CallAsync(nint instance, bool register, CancellationToken cancellationToken)
    {
        var completion = new TaskCompletionSource<uint>(TaskCreationOptions.RunContinuationsAsynchronously);
        var context = GCHandle.Alloc(new PendingCall(completion));
        var request = (DNS_SERVICE_REGISTER_REQUEST*)NativeMemory.AllocZeroed((nuint)sizeof(DNS_SERVICE_REGISTER_REQUEST));
        request->Version = RequestVersion1;
        request->InterfaceIndex = 0; // every interface
        request->pServiceInstance = (DNS_SERVICE_INSTANCE*)instance;
        request->pRegisterCompletionCallback = &OnCompleted;
        request->pQueryContext = (void*)GCHandle.ToIntPtr(context);
        request->unicastEnabled = false; // mDNS, not unicast DNS
        ((PendingCall)context.Target!).Request = (nint)request;

        var status = register ? PInvoke.DnsServiceRegister(request, null) : PInvoke.DnsServiceDeRegister(request, null);
        if (status != PInvoke.DNS_REQUEST_PENDING)
        {
            // No callback will come: release what it would have released.
            context.Free();
            NativeMemory.Free(request);
            return Task.FromResult(status);
        }

        return completion.Task.WaitAsync(CallbackTimeout, cancellationToken);
    }

    [SupportedOSPlatform(MinimumWindows)]
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static unsafe void OnCompleted(uint status, void* queryContext, DNS_SERVICE_INSTANCE* instance)
    {
        // The callback receives its own copy of the instance, which the caller must free.
        if (instance is not null)
        {
            PInvoke.DnsServiceFreeInstance(instance);
        }

        var context = GCHandle.FromIntPtr((nint)queryContext);
        var pending = (PendingCall)context.Target!;
        context.Free();
        NativeMemory.Free((void*)pending.Request);
        pending.Completion.TrySetResult(status);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Advertising {HostName} at {Address}:{Port} over mDNS.")]
    private partial void LogAdvertised(string hostName, IPAddress address, int port);

    [LoggerMessage(Level = LogLevel.Information, Message = "Withdrew the mDNS advertisement of {HostName}.")]
    private partial void LogWithdrawn(string hostName);

    private sealed class PendingCall(TaskCompletionSource<uint> completion)
    {
        public TaskCompletionSource<uint> Completion { get; } = completion;

        public nint Request { get; set; }
    }
}
