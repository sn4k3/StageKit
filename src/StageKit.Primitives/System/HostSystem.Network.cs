using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.NetworkInformation;

namespace StageKit.Primitives.System;

public static partial class HostSystem
{
    private const string InternetProbeContent = "Microsoft Connect Test";
    private static readonly Uri InternetProbeUri = new("http://www.msftconnecttest.com/connecttest.txt");
    private static readonly TimeSpan InternetProbeTimeout = TimeSpan.FromSeconds(3);

    [field: AllowNull]
    [field: MaybeNull]
    private static HttpClient InternetProbeClient => field ??= new HttpClient
    {
        MaxResponseContentBufferSize = 64,
        Timeout = Timeout.InfiniteTimeSpan
    };

    /// <summary>
    /// Determines whether the host has an available network connection.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> when a non-loopback, non-tunnel network interface is operational; otherwise,
    /// <see langword="false"/>.
    /// </returns>
    /// <remarks>
    /// This is a local availability check and does not contact an external service. A result of
    /// <see langword="true"/> does not guarantee internet access, DNS resolution, or reachability of a particular
    /// endpoint. The current state is queried on every call.
    /// </remarks>
    public static bool IsNetworkAvailable()
    {
        try
        {
#pragma warning disable CA1416 // Unsupported hosts throw PlatformNotSupportedException, which is handled below.
            return NetworkInterface.GetIsNetworkAvailable();
#pragma warning restore CA1416
        }
        catch (Exception exception) when (exception is NetworkInformationException or PlatformNotSupportedException)
        {
            return false;
        }
    }

    /// <summary>
    /// Determines whether the host can reach the internet.
    /// </summary>
    /// <param name="cancellationToken">A token that can cancel the connectivity check.</param>
    /// <returns>
    /// A task whose result is <see langword="true"/> when the internet probe succeeds with its expected response;
    /// otherwise, <see langword="false"/>.
    /// </returns>
    /// <remarks>
    /// The check sends a small HTTP request to Microsoft's connectivity-test service and validates its response. It
    /// times out after three seconds and detects common captive-portal responses. Caller-requested cancellation is
    /// propagated.
    /// </remarks>
    public static async Task<bool> IsInternetAvailableAsync(CancellationToken cancellationToken = default)
    {
        if (!IsNetworkAvailable())
        {
            return false;
        }

        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(InternetProbeTimeout);

        try
        {
            using var response = await InternetProbeClient
                .GetAsync(InternetProbeUri, HttpCompletionOption.ResponseContentRead, timeoutSource.Token)
                .ConfigureAwait(false);

            if (response.StatusCode != HttpStatusCode.OK)
            {
                return false;
            }

            var content = await response.Content.ReadAsStringAsync(timeoutSource.Token).ConfigureAwait(false);
            return string.Equals(content, InternetProbeContent, StringComparison.Ordinal);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return false;
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException)
        {
            return false;
        }
    }
}