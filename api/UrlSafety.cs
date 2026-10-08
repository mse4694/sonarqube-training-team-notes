using System.Net;
using System.Net.Sockets;

namespace TeamNotes.Api;

public sealed class UnsafeUrlException(string message) : Exception(message);

/// <summary>Allow only http(s) URLs whose host resolves exclusively to public IP addresses.</summary>
public static class UrlSafety
{
    public static async Task<Uri> AssertPublicHttpUrlAsync(string raw)
    {
        if (!Uri.TryCreate(raw, UriKind.Absolute, out var url))
        {
            throw new UnsafeUrlException("invalid url");
        }
        if (url.Scheme != Uri.UriSchemeHttp && url.Scheme != Uri.UriSchemeHttps)
        {
            throw new UnsafeUrlException("only http(s) is allowed");
        }

        string host = url.DnsSafeHost;
        IPAddress[] addresses;
        try
        {
            addresses = IPAddress.TryParse(host, out var literal)
                ? [literal]
                : await Dns.GetHostAddressesAsync(host);
        }
        catch (SocketException)
        {
            throw new UnsafeUrlException("host does not resolve");
        }

        if (addresses.Length == 0)
        {
            throw new UnsafeUrlException("host does not resolve");
        }
        foreach (var address in addresses)
        {
            if (!IsPublic(address))
            {
                throw new UnsafeUrlException("internal address");
            }
        }
        return url;
    }

    // Loopback, private, link-local (incl. cloud metadata 169.254.169.254), CGNAT, multicast, reserved.
    private static bool IsPublic(IPAddress address)
    {
        IPAddress ip = address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;

        if (IPAddress.IsLoopback(ip) || ip.Equals(IPAddress.Any) || ip.Equals(IPAddress.IPv6Any))
        {
            return false;
        }

        if (ip.AddressFamily == AddressFamily.InterNetwork)
        {
            byte[] b = ip.GetAddressBytes();
            return !(b[0] == 10                                   // 10.0.0.0/8
                || (b[0] == 172 && b[1] >= 16 && b[1] <= 31)      // 172.16.0.0/12
                || (b[0] == 192 && b[1] == 168)                   // 192.168.0.0/16
                || (b[0] == 169 && b[1] == 254)                   // 169.254.0.0/16 (link-local + metadata)
                || (b[0] == 100 && b[1] >= 64 && b[1] <= 127)     // 100.64.0.0/10 (CGNAT)
                || b[0] == 0                                      // 0.0.0.0/8
                || b[0] >= 224);                                  // 224.0.0.0/3 multicast + reserved
        }

        if (ip.AddressFamily == AddressFamily.InterNetworkV6)
        {
            return !(ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal || ip.IsIPv6Multicast || ip.IsIPv6UniqueLocal);
        }

        return false;
    }
}
