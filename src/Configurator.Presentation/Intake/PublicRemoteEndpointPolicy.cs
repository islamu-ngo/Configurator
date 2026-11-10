namespace Configurator.Presentation.Intake;

using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Sockets;

/// <summary>
/// Enforces fail-closed public HTTPS URI and non-private IP address boundaries
/// for outbound remote definition retrieval.
/// </summary>
public static class PublicRemoteEndpointPolicy
{
    public const int MaximumUriLength = 2048;

    private static readonly IPNetwork GlobalUnicastIPv6 = IPNetwork.Parse("2000::/3");

    // IANA IPv4 Special-Purpose Address Registry blocks excluded from outbound intake.
    private static readonly IPNetwork[] NonPublicIPv4Ranges =
    [
        IPNetwork.Parse("0.0.0.0/8"),       // RFC 1122 "This host on this network"
        IPNetwork.Parse("10.0.0.0/8"),      // RFC 1918 Private-Use
        IPNetwork.Parse("100.64.0.0/10"),   // RFC 6598 Shared Address Space (CGNAT)
        IPNetwork.Parse("127.0.0.0/8"),     // RFC 1122 Loopback
        IPNetwork.Parse("169.254.0.0/16"),  // RFC 3927 Link-Local & cloud metadata (169.254.169.254)
        IPNetwork.Parse("172.16.0.0/12"),   // RFC 1918 Private-Use
        IPNetwork.Parse("192.0.0.0/24"),    // RFC 6890 IETF Protocol Assignments
        IPNetwork.Parse("192.0.2.0/24"),    // RFC 5737 Documentation (TEST-NET-1)
        IPNetwork.Parse("192.88.99.0/24"),  // RFC 7526 Deprecated 6to4 Relay Anycast
        IPNetwork.Parse("192.168.0.0/16"),  // RFC 1918 Private-Use
        IPNetwork.Parse("198.18.0.0/15"),   // RFC 2544 Benchmarking
        IPNetwork.Parse("198.51.100.0/24"), // RFC 5737 Documentation (TEST-NET-2)
        IPNetwork.Parse("203.0.113.0/24"),  // RFC 5737 Documentation (TEST-NET-3)
        IPNetwork.Parse("224.0.0.0/4"),     // RFC 5771 Multicast
        IPNetwork.Parse("240.0.0.0/4")      // RFC 1112 Reserved / Limited Broadcast
    ];

    // Special-purpose allocations carved out of IPv6 Global Unicast (2000::/3).
    private static readonly IPNetwork[] SpecialPurposeGlobalIPv6Ranges =
    [
        IPNetwork.Parse("2001::/32"),       // RFC 4380 Teredo tunneling
        IPNetwork.Parse("2001:2::/48"),     // RFC 5180 Benchmarking
        IPNetwork.Parse("2001:10::/28"),    // RFC 4843 Deprecated ORCHID
        IPNetwork.Parse("2001:20::/28"),    // RFC 7343 ORCHIDv2
        IPNetwork.Parse("2001:db8::/32"),   // RFC 3849 Documentation
        IPNetwork.Parse("2002::/16")        // RFC 3056 6to4
    ];

    /// <summary>
    /// Admits only credential-free, query/fragment-free public HTTPS DNS URIs
    /// within the bounded length limit.
    /// </summary>
    public static bool TryValidatePublicHttpsUri(string? value, [NotNullWhen(true)] out Uri? endpoint)
    {
        endpoint = null;
        if (string.IsNullOrWhiteSpace(value) || value.Length > MaximumUriLength)
            return false;
        if (!Uri.TryCreate(value, UriKind.Absolute, out Uri? candidate)
            || candidate.Scheme != Uri.UriSchemeHttps
            || candidate.HostNameType != UriHostNameType.Dns
            || candidate.UserInfo.Length != 0
            || candidate.Query.Length != 0
            || candidate.Fragment.Length != 0)
            return false;

        string host = candidate.IdnHost.TrimEnd('.');
        if (host.Length == 0
            || IPAddress.TryParse(host, out _)
            || !host.Contains('.', StringComparison.Ordinal)
            || host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".local", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".internal", StringComparison.OrdinalIgnoreCase))
            return false;

        endpoint = candidate;
        return true;
    }

    /// <summary>
    /// Returns true only when the resolved address is a globally routable public
    /// unicast IPv4 or IPv6 destination. Unassigned and special-purpose ranges fail closed.
    /// </summary>
    public static bool IsPublicAddress(IPAddress address)
    {
        ArgumentNullException.ThrowIfNull(address);
        if (address.IsIPv4MappedToIPv6)
            address = address.MapToIPv4();

        return address.AddressFamily switch
        {
            AddressFamily.InterNetwork => IsPublicIPv4(address),
            AddressFamily.InterNetworkV6 => IsPublicIPv6(address),
            _ => false
        };
    }

    private static bool IsPublicIPv4(IPAddress address)
    {
        foreach (IPNetwork range in NonPublicIPv4Ranges)
        {
            if (range.Contains(address))
                return false;
        }
        return true;
    }

    private static bool IsPublicIPv6(IPAddress address)
    {
        if (!GlobalUnicastIPv6.Contains(address))
            return false;

        foreach (IPNetwork range in SpecialPurposeGlobalIPv6Ranges)
        {
            if (range.Contains(address))
                return false;
        }
        return true;
    }
}
