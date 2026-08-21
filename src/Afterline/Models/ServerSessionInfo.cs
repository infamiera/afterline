namespace Afterline.Models;

public sealed class ServerSessionInfo
{
    private static readonly string[] GenericServerNames =
    {
        "CitizenFX root UI",
        "CitizenFX",
        "FiveM",
        "Cfx.re",
        "root",
        "NUI",
        "cfx-nui-client"
    };

    public string? Name { get; init; }
    public string? Address { get; init; }

    public bool HasFriendlyName =>
        !string.IsNullOrWhiteSpace(Name) &&
        !IsGenericServerName(Name);

    public string DisplayName => HasFriendlyName
        ? Name!.Trim()
        : "Unknown Server";

    public string ArchiveLabel
    {
        get
        {
            if (HasFriendlyName) return DisplayName;
            if (!string.IsNullOrWhiteSpace(Address))
                return $"Unresolved Server {ShortHash(NormalizeAddress(Address))}";
            return "Unknown Server";
        }
    }

    public static ServerSessionInfo Unknown { get; } = new();

    public bool HasSameAddress(ServerSessionInfo? other)
    {
        if (other is null || string.IsNullOrWhiteSpace(Address) || string.IsNullOrWhiteSpace(other.Address))
            return false;

        return string.Equals(NormalizeAddress(Address), NormalizeAddress(other.Address), StringComparison.OrdinalIgnoreCase);
    }

    public bool HasDifferentKnownAddress(ServerSessionInfo? other)
    {
        if (other is null || string.IsNullOrWhiteSpace(Address) || string.IsNullOrWhiteSpace(other.Address))
            return false;

        return !HasSameAddress(other);
    }

    public string StableKey
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(Address))
                return "address:" + NormalizeAddress(Address);

            if (HasFriendlyName)
                return "name:" + DisplayName.ToUpperInvariant();

            return "unknown";
        }
    }

    public static bool IsGenericServerName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return true;
        string normalized = value.Trim();

        return GenericServerNames.Any(name =>
            string.Equals(normalized, name, StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizeAddress(string address)
        => address.Trim().TrimEnd('/').ToLowerInvariant();

    private static string ShortHash(string value)
    {
        uint hash = 2166136261;
        foreach (char c in value)
        {
            hash ^= c;
            hash *= 16777619;
        }
        return hash.ToString("X8");
    }
}

public sealed class ServerSessionChangedEventArgs : EventArgs
{
    public ServerSessionInfo? Server { get; }

    public bool IsConnected => Server is not null;

    public ServerSessionChangedEventArgs(ServerSessionInfo? server)
    {
        Server = server;
    }
}
