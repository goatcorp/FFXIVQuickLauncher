namespace XIVLauncher.Common.Dalamud.Rpc.Types;

// WARNING: Do not alter this file without coordinating with the Dalamud team.

public record ClientHelloRequest
{
    /// <summary>
    /// Gets the API version this client is expecting.
    /// </summary>
    public string ApiVersion { get; init; } = "1.0";

    /// <summary>
    /// Gets the user agent of the client.
    /// </summary>
    public string UserAgent { get; init; } = "XIVLauncher/1.0";
}

public record ClientHelloResponse
{
    /// <summary>
    /// Gets the API version this server has offered.
    /// </summary>
    public string? ApiVersion { get; init; }

    /// <summary>
    /// Gets the current Dalamud version.
    /// </summary>
    public string? DalamudVersion { get; init; }

    /// <summary>
    /// Gets the current game version.
    /// </summary>
    public string? GameVersion { get; init; }

    /// <summary>
    /// Gets the process ID of this client.
    /// </summary>
    public int? ProcessId { get; init; }

    /// <summary>
    /// Gets the time this process started.
    /// </summary>
    public long? ProcessStartTime { get; init; }

    /// <summary>
    /// Gets an identifier for this client.
    /// </summary>
    public string? ClientState { get; init; }
}
