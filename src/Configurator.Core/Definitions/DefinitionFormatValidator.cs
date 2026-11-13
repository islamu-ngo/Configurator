namespace Configurator.Core.Definitions;

using System.Collections.Frozen;
using Configurator.Core.Dotenv;

/// <summary>
/// Closed registry of asserted string formats. Every registered format is checked automatically during answer
/// validation by a hand-written single-pass scanner: no regular expressions, no backtracking and no allocation
/// proportional to input beyond bounded splits, so validation stays linear in the value length.
/// </summary>
/// <remarks>
/// Checks are lexical only. A well-formed hostname, URL or image reference says nothing about reachability,
/// ownership or target acceptance; the target application keeps that authority.
/// </remarks>
public static class DefinitionFormatValidator
{
    private static readonly FrozenDictionary<string, Func<string, bool>> Validators =
        new Dictionary<string, Func<string, bool>>(StringComparer.Ordinal)
        {
            ["hostname"] = Hostname,
            ["ipv4"] = IPv4,
            ["ipv6"] = IPv6,
            ["ip-address"] = value => IPv4(value) || IPv6(value),
            ["cidr"] = Cidr,
            ["uri"] = Uri,
            ["http-url"] = value => HttpUrl(value, httpsOnly: false),
            ["https-url"] = value => HttpUrl(value, httpsOnly: true),
            ["origin"] = Origin,
            ["email-address"] = EmailAddress,
            ["slug"] = Slug,
            ["env-key"] = DotenvCodec.IsEnvironmentKey,
            ["bcp47-language-tag"] = LanguageTag,
            ["iso3166-alpha2"] = value => value.Length == 2 && char.IsAsciiLetterUpper(value[0]) && char.IsAsciiLetterUpper(value[1]),
            ["uuid"] = Uuid,
            ["semver"] = SemanticVersion,
            ["duration"] = Duration,
            ["byte-size"] = ByteSize,
            ["unix-path"] = UnixPath,
            ["relative-path"] = RelativePath,
            ["oci-image"] = OciImage,
            ["sha256-hex"] = value => value.Length == 64 && value.All(char.IsAsciiHexDigitLower),
            ["cron-5"] = Cron
        }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>Every asserted format token, in ordinal order.</summary>
    public static IReadOnlyList<string> Formats { get; } =
        Array.AsReadOnly(Validators.Keys.Order(StringComparer.Ordinal).ToArray());

    /// <summary>Returns whether <paramref name="format"/> is an asserted format token.</summary>
    public static bool IsKnown(string format) => Validators.ContainsKey(format);

    /// <summary>Checks a value against an asserted format. Unknown formats are not asserted and return true.</summary>
    public static bool IsValid(string format, string value)
    {
        ArgumentNullException.ThrowIfNull(format);
        ArgumentNullException.ThrowIfNull(value);
        return !Validators.TryGetValue(format, out Func<string, bool>? validator) || validator(value);
    }

    /// <summary>Accumulates an already-verified short ASCII digit span without culture-sensitive parsing.</summary>
    private static int Number(ReadOnlySpan<char> digits)
    {
        int number = 0;
        foreach (char digit in digits) number = number * 10 + (digit - '0');
        return number;
    }

    private static bool Hostname(string value)
    {
        ReadOnlySpan<char> host = value.EndsWith('.') ? value.AsSpan(0, value.Length - 1) : value;
        if (host.Length is 0 or > 253) return false;
        int labelStart = 0;
        for (int index = 0; index <= host.Length; index++)
        {
            if (index < host.Length && host[index] != '.')
            {
                if (!char.IsAsciiLetterOrDigit(host[index]) && host[index] != '-') return false;
                continue;
            }
            int length = index - labelStart;
            if (length is 0 or > 63 || host[labelStart] == '-' || host[index - 1] == '-') return false;
            labelStart = index + 1;
        }
        return true;
    }

    private static bool IPv4(string value) => IPv4(value.AsSpan());

    private static bool IPv4(ReadOnlySpan<char> value)
    {
        if (value.Length is < 7 or > 15) return false;
        int octets = 0;
        int start = 0;
        for (int index = 0; index <= value.Length; index++)
        {
            if (index < value.Length && value[index] != '.')
            {
                if (!char.IsAsciiDigit(value[index])) return false;
                continue;
            }
            ReadOnlySpan<char> octet = value[start..index];
            if (octet.Length is 0 or > 3 || octet.Length > 1 && octet[0] == '0' || Number(octet) > 255) return false;
            octets++;
            start = index + 1;
        }
        return octets == 4;
    }

    private static bool IPv6(string value) => IPv6(value.AsSpan());

    /// <summary>RFC 4291 text form: eight hex groups, at most one <c>::</c> compression, optional IPv4 tail.</summary>
    private static bool IPv6(ReadOnlySpan<char> value)
    {
        if (value.Length is < 2 or > 45) return false;
        int compression = value.IndexOf("::");
        if (compression < 0) return Groups(value, ipv4Tail: true) == 8;
        if (value[(compression + 1)..].IndexOf("::") >= 0) return false;
        int head = Groups(value[..compression], ipv4Tail: false);
        int tail = Groups(value[(compression + 2)..], ipv4Tail: true);
        return head >= 0 && tail >= 0 && head + tail <= 7;

        // Returns the number of 16-bit groups, or -1 when the span is malformed. An empty span has zero groups.
        static int Groups(ReadOnlySpan<char> text, bool ipv4Tail)
        {
            if (text.Length == 0) return 0;
            int count = 0;
            int start = 0;
            for (int index = 0; index <= text.Length; index++)
            {
                if (index < text.Length && text[index] != ':') continue;
                ReadOnlySpan<char> group = text[start..index];
                if (ipv4Tail && index == text.Length && group.Contains('.'))
                    return IPv4(group) ? count + 2 : -1;
                if (group.Length is 0 or > 4) return -1;
                foreach (char character in group)
                    if (!char.IsAsciiHexDigit(character)) return -1;
                count++;
                start = index + 1;
            }
            return count;
        }
    }

    private static bool Cidr(string value)
    {
        int slash = value.IndexOf('/');
        if (slash < 0 || value.IndexOf('/', slash + 1) >= 0) return false;
        ReadOnlySpan<char> prefix = value.AsSpan(slash + 1);
        if (prefix.Length is 0 or > 3 || prefix.Length > 1 && prefix[0] == '0') return false;
        foreach (char character in prefix)
            if (!char.IsAsciiDigit(character)) return false;
        int bits = Number(prefix);
        ReadOnlySpan<char> address = value.AsSpan(0, slash);
        return IPv4(address) ? bits <= 32 : IPv6(address) && bits <= 128;
    }

    /// <summary>RFC 3986 absolute URI: scheme, colon, then only unreserved, reserved or percent-encoded characters.</summary>
    private static bool Uri(string value)
    {
        int colon = SchemeLength(value);
        if (colon <= 0 || colon == value.Length - 1) return false;
        return UriCharacters(value.AsSpan(colon + 1));
    }

    private static int SchemeLength(string value)
    {
        if (value.Length == 0 || !char.IsAsciiLetter(value[0])) return -1;
        for (int index = 1; index < value.Length; index++)
        {
            char character = value[index];
            if (character == ':') return index;
            if (!char.IsAsciiLetterOrDigit(character) && character is not ('+' or '-' or '.')) return -1;
        }
        return -1;
    }

    private static bool UriCharacters(ReadOnlySpan<char> text)
    {
        for (int index = 0; index < text.Length; index++)
        {
            char character = text[index];
            if (character == '%')
            {
                if (index + 2 >= text.Length || !char.IsAsciiHexDigit(text[index + 1]) || !char.IsAsciiHexDigit(text[index + 2]))
                    return false;
                index += 2;
                continue;
            }
            if (!char.IsAsciiLetterOrDigit(character) && "-._~:/?#[]@!$&'()*+,;=".IndexOf(character) < 0) return false;
        }
        return true;
    }

    private static bool HttpUrl(string value, bool httpsOnly)
    {
        if (!TryAuthority(value, httpsOnly, out int end)) return false;
        if (end == value.Length) return true;
        return value[end] is '/' or '?' or '#' && UriCharacters(value.AsSpan(end));
    }

    /// <summary>A web origin: scheme, host and optional port with nothing after the authority.</summary>
    private static bool Origin(string value) => TryAuthority(value, httpsOnly: false, out int end) && end == value.Length;

    private static bool TryAuthority(string value, bool httpsOnly, out int end)
    {
        end = 0;
        int start = value.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ? 8
            : !httpsOnly && value.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ? 7 : -1;
        if (start < 0) return false;
        int stop = value.AsSpan(start).IndexOfAny("/?#");
        stop = stop < 0 ? value.Length : start + stop;
        ReadOnlySpan<char> authority = value.AsSpan(start, stop - start);
        if (authority.Length == 0 || authority.Contains('@')) return false;
        ReadOnlySpan<char> host = authority;
        ReadOnlySpan<char> port = [];
        if (authority[0] == '[')
        {
            int close = authority.IndexOf(']');
            if (close < 0 || !IPv6(authority[1..close])) return false;
            ReadOnlySpan<char> rest = authority[(close + 1)..];
            if (rest.Length > 0 && (rest[0] != ':' || (port = rest[1..]).Length == 0)) return false;
        }
        else
        {
            int colon = authority.IndexOf(':');
            if (colon >= 0)
            {
                host = authority[..colon];
                port = authority[(colon + 1)..];
                if (port.Length == 0) return false;
            }
            if (!Hostname(host.ToString()) && !IPv4(host)) return false;
        }
        if (port.Length > 0 && !Port(port)) return false;
        end = stop;
        return true;
    }

    private static bool Port(ReadOnlySpan<char> port)
    {
        if (port.Length > 5 || port[0] == '0') return false;
        foreach (char character in port)
            if (!char.IsAsciiDigit(character)) return false;
        return Number(port) <= 65535;
    }

    private static bool EmailAddress(string value)
    {
        int separator = value.IndexOf('@');
        if (separator is < 1 or > 64 || separator != value.LastIndexOf('@') || value.Length > 254) return false;
        ReadOnlySpan<char> local = value.AsSpan(0, separator);
        if (local[0] == '.' || local[^1] == '.' || local.Contains("..", StringComparison.Ordinal)) return false;
        foreach (char character in local)
            if (!char.IsAsciiLetterOrDigit(character) && ".!#$%&'*+-/=?^_`{|}~".IndexOf(character) < 0) return false;
        string domain = value[(separator + 1)..];
        return !domain.EndsWith('.') && Hostname(domain);
    }

    private static bool Slug(string value)
    {
        if (value.Length == 0 || value[0] == '-' || value[^1] == '-') return false;
        for (int index = 0; index < value.Length; index++)
        {
            char character = value[index];
            if (character == '-' ? value[index - 1] == '-' : !char.IsAsciiLetterLower(character) && !char.IsAsciiDigit(character))
                return false;
        }
        return true;
    }

    /// <summary>RFC 5646 well-formedness: language, optional script, region, variants, extensions and private use.</summary>
    private static bool LanguageTag(string value)
    {
        if (value.Length is 0 or > 128) return false;
        string[] subtags = value.Split('-');
        if (subtags.Any(subtag => subtag.Length is 0 or > 8 || !subtag.All(char.IsAsciiLetterOrDigit))) return false;
        int index = 0;
        if (subtags[0] is "x" or "X") return PrivateUse(subtags, 0);
        if (!(subtags[0].Length is 2 or 3 or >= 5 && subtags[0].All(char.IsAsciiLetter))) return false;
        index++;
        if (index < subtags.Length && subtags[index].Length == 4 && subtags[index].All(char.IsAsciiLetter)) index++;
        if (index < subtags.Length && (subtags[index].Length == 2 && subtags[index].All(char.IsAsciiLetter)
            || subtags[index].Length == 3 && subtags[index].All(char.IsAsciiDigit))) index++;
        while (index < subtags.Length && (subtags[index].Length >= 5
            || subtags[index].Length == 4 && char.IsAsciiDigit(subtags[index][0]))) index++;
        while (index < subtags.Length && subtags[index].Length == 1 && subtags[index] is not ("x" or "X"))
        {
            int first = ++index;
            while (index < subtags.Length && subtags[index].Length >= 2) index++;
            if (index == first) return false;
        }
        return index == subtags.Length || PrivateUse(subtags, index);

        static bool PrivateUse(string[] subtags, int start) =>
            subtags[start] is "x" or "X" && subtags.Length > start + 1;
    }

    private static bool Uuid(string value)
    {
        if (value.Length != 36) return false;
        for (int index = 0; index < value.Length; index++)
            if (index is 8 or 13 or 18 or 23 ? value[index] != '-' : !char.IsAsciiHexDigit(value[index])) return false;
        return true;
    }

    /// <summary>Semantic Versioning 2.0.0 without a leading <c>v</c>.</summary>
    private static bool SemanticVersion(string value)
    {
        int build = value.IndexOf('+');
        string core = build < 0 ? value : value[..build];
        if (build >= 0 && !Identifiers(value[(build + 1)..], numericLeadingZero: true)) return false;
        int release = core.IndexOf('-');
        if (release >= 0 && !Identifiers(core[(release + 1)..], numericLeadingZero: false)) return false;
        string[] numbers = (release < 0 ? core : core[..release]).Split('.');
        return numbers.Length == 3 && numbers.All(number => Numeric(number) && (number.Length == 1 || number[0] != '0'));

        static bool Numeric(string text) => text.Length > 0 && text.All(char.IsAsciiDigit);
        static bool Identifiers(string text, bool numericLeadingZero) => text.Length > 0 && text.Split('.').All(identifier =>
            identifier.Length > 0 && identifier.All(character => char.IsAsciiLetterOrDigit(character) || character == '-')
            && (numericLeadingZero || !Numeric(identifier) || identifier.Length == 1 || identifier[0] != '0'));
    }

    /// <summary>RFC 3339 Appendix A duration: <c>PnW</c>, or date and/or time components in descending order.</summary>
    private static bool Duration(string value)
    {
        if (value.Length < 3 || value[0] != 'P') return false;
        if (value[^1] == 'W') return value.Length > 2 && value.AsSpan(1, value.Length - 2).ToArray().All(char.IsAsciiDigit);
        int time = value.IndexOf('T');
        if (time == value.Length - 1) return false;
        string date = time < 0 ? value[1..] : value[1..time];
        string clock = time < 0 ? "" : value[(time + 1)..];
        return (date.Length > 0 || clock.Length > 0) && Components(date, "YMD") && Components(clock, "HMS");

        static bool Components(string text, string units)
        {
            int unit = 0;
            int digits = 0;
            foreach (char character in text)
            {
                if (char.IsAsciiDigit(character)) { digits++; continue; }
                int position = units.IndexOf(character, unit);
                if (digits == 0 || position < 0) return false;
                unit = position + 1;
                digits = 0;
            }
            return digits == 0;
        }
    }

    private static bool ByteSize(string value)
    {
        int index = 0;
        while (index < value.Length && char.IsAsciiDigit(value[index])) index++;
        if (index == 0) return false;
        if (index < value.Length && value[index] == '.')
        {
            int fraction = ++index;
            while (index < value.Length && char.IsAsciiDigit(value[index])) index++;
            if (index == fraction) return false;
        }
        return value[index..] is "" or "B" or "kB" or "KB" or "MB" or "GB" or "TB" or "PB"
            or "KiB" or "MiB" or "GiB" or "TiB" or "PiB";
    }

    private static bool UnixPath(string value) => value.Length is > 0 and <= 4096 && value[0] == '/'
        && (value.Length == 1 || Segments(value.AsSpan(1, value[^1] == '/' ? value.Length - 2 : value.Length - 1), allowDot: false));

    private static bool RelativePath(string value) => value.Length is > 0 and <= 4096 && value[0] != '/'
        && !(value.Length >= 2 && char.IsAsciiLetter(value[0]) && value[1] == ':')
        && Segments(value.AsSpan(0, value[^1] == '/' && value.Length > 1 ? value.Length - 1 : value.Length), allowDot: true);

    private static bool Segments(ReadOnlySpan<char> path, bool allowDot)
    {
        int start = 0;
        for (int index = 0; index <= path.Length; index++)
        {
            if (index < path.Length && path[index] != '/')
            {
                if (char.IsControl(path[index]) || path[index] == '\\') return false;
                continue;
            }
            ReadOnlySpan<char> segment = path[start..index];
            if (segment.Length == 0 || segment is ".." || segment is "." && (!allowDot || start != 0)) return false;
            start = index + 1;
        }
        return true;
    }

    /// <summary>OCI distribution reference: optional registry host, lowercase repository path, tag and digest.</summary>
    private static bool OciImage(string value)
    {
        if (value.Length is 0 or > 4096) return false;
        string remainder = value;
        int at = remainder.IndexOf('@');
        if (at >= 0)
        {
            string digest = remainder[(at + 1)..];
            int colon = digest.IndexOf(':');
            if (colon <= 0 || !digest[..colon].All(character => char.IsAsciiLetterLower(character) || char.IsAsciiDigit(character)
                    || character is '+' or '.' or '_' or '-')
                || digest.Length - colon - 1 < 32 || !digest[(colon + 1)..].All(char.IsAsciiHexDigitLower))
                return false;
            remainder = remainder[..at];
        }
        int slash = remainder.LastIndexOf('/');
        int tag = remainder.LastIndexOf(':');
        if (tag > slash)
        {
            string label = remainder[(tag + 1)..];
            if (label.Length is 0 or > 128 || !(char.IsAsciiLetterOrDigit(label[0]) || label[0] == '_')
                || !label.All(character => char.IsAsciiLetterOrDigit(character) || character is '_' or '.' or '-'))
                return false;
            remainder = remainder[..tag];
        }
        string[] components = remainder.Split('/');
        int first = 0;
        if (components.Length > 1 && (components[0].Contains('.') || components[0].Contains(':') || components[0] == "localhost"))
        {
            string registry = components[0];
            int port = registry.IndexOf(':');
            if (port >= 0 && (port == registry.Length - 1 || !Port(registry.AsSpan(port + 1)))) return false;
            if (!Hostname(port < 0 ? registry : registry[..port])) return false;
            first = 1;
        }
        return components.Skip(first).All(Component) && components.Length > first;

        static bool Component(string component)
        {
            if (component.Length == 0 || !char.IsAsciiLetterLower(component[0]) && !char.IsAsciiDigit(component[0])
                || !char.IsAsciiLetterLower(component[^1]) && !char.IsAsciiDigit(component[^1])) return false;
            for (int index = 1; index < component.Length; index++)
            {
                char character = component[index];
                if (char.IsAsciiLetterLower(character) || char.IsAsciiDigit(character)) continue;
                if (character is not ('.' or '_' or '-')) return false;
                bool repeated = character is '_' or '-' && component[index - 1] == character;
                if (!repeated && !char.IsAsciiLetterOrDigit(component[index - 1])) return false;
            }
            return true;
        }
    }

    private static readonly (int Minimum, int Maximum, string[] Names)[] CronFields =
    [
        (0, 59, []), (0, 23, []), (1, 31, []),
        (1, 12, ["JAN", "FEB", "MAR", "APR", "MAY", "JUN", "JUL", "AUG", "SEP", "OCT", "NOV", "DEC"]),
        (0, 7, ["SUN", "MON", "TUE", "WED", "THU", "FRI", "SAT"])
    ];

    /// <summary>Five-field POSIX cron with lists, ranges, steps and English month/day abbreviations.</summary>
    private static bool Cron(string value)
    {
        if (value.Length > 1024) return false;
        string[] fields = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (fields.Length != 5) return false;
        for (int index = 0; index < fields.Length; index++)
        {
            (int minimum, int maximum, string[] names) = CronFields[index];
            foreach (string item in fields[index].Split(','))
            {
                int slash = item.IndexOf('/');
                string range = slash < 0 ? item : item[..slash];
                if (slash >= 0 && (!int.TryParse(item.AsSpan(slash + 1), System.Globalization.NumberStyles.None,
                    System.Globalization.CultureInfo.InvariantCulture, out int step) || step < 1 || step > maximum + 1)) return false;
                if (range == "*") continue;
                int dash = range.IndexOf('-');
                if (dash < 0 ? !Value(range, out _) : !Value(range[..dash], out int low) || !Value(range[(dash + 1)..], out int high) || low > high)
                    return false;
            }

            bool Value(string text, out int number)
            {
                int position = Array.FindIndex(names, name => name.Equals(text, StringComparison.OrdinalIgnoreCase));
                number = position >= 0 ? position + minimum : -1;
                if (position >= 0) return true;
                return text.Length is > 0 and <= 2 && int.TryParse(text, System.Globalization.NumberStyles.None,
                    System.Globalization.CultureInfo.InvariantCulture, out number) && number >= minimum && number <= maximum;
            }
        }
        return true;
    }
}
