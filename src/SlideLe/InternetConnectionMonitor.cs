using System.Net.NetworkInformation;

namespace SlideLe;

/// <summary>
/// Checks Internet reachability immediately before a network-dependent action.
/// </summary>
internal static class InternetConnectionChecker
{
    private static readonly Uri[] ConnectivityCheckUris =
    [
        new Uri("https://www.msftconnecttest.com/connecttest.txt"),
        new Uri("https://www.gstatic.com/generate_204"),
        new Uri("https://github.com/")
    ];
    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(8)
    };

    public static async Task<bool> CanReachInternetAsync(CancellationToken cancellationToken)
    {
        if (!NetworkInterface.GetIsNetworkAvailable())
        {
            return false;
        }

        bool[] results = await Task.WhenAll(ConnectivityCheckUris.Select(
            endpoint => CanReachEndpointAsync(endpoint, cancellationToken)));
        return results.Any(isReachable => isReachable);
    }

    private static async Task<bool> CanReachEndpointAsync(Uri endpoint, CancellationToken cancellationToken)
    {
        try
        {
            using HttpResponseMessage response = await HttpClient.GetAsync(
                endpoint,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            // Any HTTPS response proves that the public endpoint was reached.
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (HttpRequestException)
        {
            return false;
        }
        catch (TaskCanceledException)
        {
            return false;
        }
    }
}
