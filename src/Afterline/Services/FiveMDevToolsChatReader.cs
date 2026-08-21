using System.Net;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Afterline.Models;

namespace Afterline.Services;

public sealed class FiveMDevToolsChatReader : IAsyncDisposable
{
    private static readonly Uri TargetsUri = new("http://127.0.0.1:13172/json");
    private const string RootUiUrl = "nui://game/ui/root.html";
    private const string ClientFramePrefix = "https://cfx-nui-client/";

    private const string ReadChatExpression =
        "JSON.stringify(Array.from(document.querySelectorAll('.chat__messages > li'))" +
        ".map(function(el){return (el.innerText || '').replace(/\\s+/g,' ').trim();})" +
        ".filter(function(x){return x.length > 0;}))";

    // FiveM's root NUI keeps the current endpoint in the lexical `serverAddress`
    // variable. It is updated by FiveM through the rootCall/setServerAddress path.
    // Do not use document.title here: the root page title is "CitizenFX root UI"
    // and is not server metadata.
    private const string ReadServerStateExpression =
        "JSON.stringify((function(){" +
        "var h=(typeof handoverBlob==='object'&&handoverBlob)?handoverBlob:{};" +
        "var a=(typeof serverAddress==='string')?serverAddress:'';" +
        "return {address:(a||h.serverAddress||h.endpoint||'')," +
        "name:(h.serverName||h.projectName||h.hostname||'')};" +
        "})())";

    private readonly HttpClient _http;
    private readonly Dictionary<string, string> _resolvedNames = new(StringComparer.OrdinalIgnoreCase);
    private ClientWebSocket? _socket;
    private int _contextId;
    private int _requestId;
    private ServerSessionInfo _currentServer = ServerSessionInfo.Unknown;
    private string? _lastResolutionAddress;
    private DateTime _lastResolutionAttemptUtc = DateTime.MinValue;

    public ServerSessionInfo CurrentServer => _currentServer;

    public FiveMDevToolsChatReader()
    {
        var handler = new HttpClientHandler { UseProxy = false };
        _http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(2) };
    }

    public async Task<IReadOnlyList<string>> ReadVisibleLinesAsync(CancellationToken cancellationToken)
    {
        await EnsureConnectedAsync(cancellationToken);
        await RefreshServerInfoAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(_currentServer.Address))
            throw new IOException("FiveM is running but is not currently connected to a server.");

        JsonElement result = await RequestAsync("Runtime.evaluate", new
        {
            expression = ReadChatExpression,
            contextId = _contextId,
            returnByValue = true
        }, cancellationToken);

        if (!result.TryGetProperty("result", out JsonElement runtimeResult) ||
            !runtimeResult.TryGetProperty("value", out JsonElement valueElement))
            return Array.Empty<string>();

        string? json = valueElement.GetString();
        if (string.IsNullOrWhiteSpace(json)) return Array.Empty<string>();

        return JsonSerializer.Deserialize<string[]>(json)?
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .ToArray() ?? Array.Empty<string>();
    }

    public async Task ResetAsync()
    {
        if (_socket is not null)
        {
            try
            {
                if (_socket.State == WebSocketState.Open)
                    await _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "reset", CancellationToken.None);
            }
            catch { }

            _socket.Dispose();
            _socket = null;
        }

        _contextId = 0;
        _requestId = 0;
        _currentServer = ServerSessionInfo.Unknown;
        _lastResolutionAddress = null;
        _lastResolutionAttemptUtc = DateTime.MinValue;
    }

    private async Task EnsureConnectedAsync(CancellationToken cancellationToken)
    {
        if (_socket?.State == WebSocketState.Open && _contextId != 0) return;
        await ResetAsync();

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        linked.CancelAfter(TimeSpan.FromSeconds(2));

        string targetJson = await _http.GetStringAsync(TargetsUri, linked.Token);
        using JsonDocument targets = JsonDocument.Parse(targetJson);

        string? debuggerUrl = null;
        foreach (JsonElement target in targets.RootElement.EnumerateArray())
        {
            if (!target.TryGetProperty("url", out JsonElement url) ||
                !string.Equals(url.GetString(), RootUiUrl, StringComparison.OrdinalIgnoreCase))
                continue;

            if (target.TryGetProperty("webSocketDebuggerUrl", out JsonElement ws))
                debuggerUrl = ws.GetString();
            break;
        }

        if (string.IsNullOrWhiteSpace(debuggerUrl))
            throw new IOException("FiveM root NUI target is not available yet.");

        Uri socketUri = new(debuggerUrl);
        if (!IsLoopback(socketUri))
            throw new IOException("Refusing a non-local FiveM DevTools WebSocket endpoint.");

        _socket = new ClientWebSocket();
        _socket.Options.Proxy = null;
        await _socket.ConnectAsync(socketUri, linked.Token);

        JsonElement frameTree = await RequestAsync("Page.getFrameTree", new { }, linked.Token);
        string? frameId = FindClientFrameId(frameTree);
        if (string.IsNullOrWhiteSpace(frameId))
            throw new IOException("FiveM client chat frame is not available yet.");

        JsonElement isolatedWorld = await RequestAsync("Page.createIsolatedWorld", new
        {
            frameId,
            worldName = "afterline-reader",
            grantUniveralAccess = false
        }, linked.Token);

        if (!isolatedWorld.TryGetProperty("executionContextId", out JsonElement context))
            throw new IOException("FiveM chat execution context is unavailable.");

        _contextId = context.GetInt32();
    }

    private async Task RefreshServerInfoAsync(CancellationToken cancellationToken)
    {
        ServerHint hint = await ReadServerHintAsync(cancellationToken);
        string? address = NullIfBlank(hint.Address);
        string? name = CleanServerName(NullIfBlank(hint.Name));

        if (string.IsNullOrWhiteSpace(address))
        {
            _currentServer = ServerSessionInfo.Unknown;
            _lastResolutionAddress = null;
            return;
        }

        string normalizedAddress = NormalizeAddress(address);
        if (ServerSessionInfo.IsGenericServerName(name)) name = null;

        if (string.IsNullOrWhiteSpace(name) && _resolvedNames.TryGetValue(normalizedAddress, out string? cachedName))
            name = cachedName;

        bool addressChanged = !string.Equals(
            normalizedAddress,
            _lastResolutionAddress,
            StringComparison.OrdinalIgnoreCase);

        bool shouldResolve = string.IsNullOrWhiteSpace(name) &&
            (addressChanged || DateTime.UtcNow - _lastResolutionAttemptUtc >= TimeSpan.FromSeconds(5));

        if (shouldResolve)
        {
            _lastResolutionAddress = normalizedAddress;
            _lastResolutionAttemptUtc = DateTime.UtcNow;

            string? resolved = await TryResolveServerNameAsync(address, cancellationToken);
            if (!string.IsNullOrWhiteSpace(resolved))
            {
                name = resolved;
                _resolvedNames[normalizedAddress] = resolved;
            }
        }
        else if (addressChanged)
        {
            _lastResolutionAddress = normalizedAddress;
        }

        _currentServer = new ServerSessionInfo
        {
            Address = address,
            Name = name
        };
    }

    private async Task<ServerHint> ReadServerHintAsync(CancellationToken cancellationToken)
    {
        JsonElement result = await RequestAsync("Runtime.evaluate", new
        {
            expression = ReadServerStateExpression,
            returnByValue = true
        }, cancellationToken);

        if (!result.TryGetProperty("result", out JsonElement runtimeResult) ||
            !runtimeResult.TryGetProperty("value", out JsonElement valueElement))
            return new ServerHint();

        string? json = valueElement.GetString();
        if (string.IsNullOrWhiteSpace(json)) return new ServerHint();

        try
        {
            return JsonSerializer.Deserialize<ServerHint>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? new ServerHint();
        }
        catch
        {
            return new ServerHint();
        }
    }

    private async Task<string?> TryResolveServerNameAsync(string address, CancellationToken cancellationToken)
    {
        if (!TryBuildServerBaseUri(address, out Uri? baseUri))
            return null;

        // Project name is usually available from info.json.
        JsonDocument? info = await TryReadJsonAsync(new Uri(baseUri, "/info.json"), cancellationToken);
        if (info is not null)
        {
            using (info)
            {
                if (info.RootElement.TryGetProperty("vars", out JsonElement vars) && vars.ValueKind == JsonValueKind.Object)
                {
                    foreach (string property in new[] { "sv_projectName", "sv_hostname", "serverName" })
                    {
                        string? parsed = TryGetCleanString(vars, property);
                        if (!string.IsNullOrWhiteSpace(parsed) && !ServerSessionInfo.IsGenericServerName(parsed))
                            return parsed;
                    }
                }

                foreach (string property in new[] { "serverName", "hostname", "name" })
                {
                    string? parsed = TryGetCleanString(info.RootElement, property);
                    if (!string.IsNullOrWhiteSpace(parsed) && !ServerSessionInfo.IsGenericServerName(parsed))
                        return parsed;
                }
            }
        }

        // FiveM's dynamic endpoint explicitly exposes sv_hostname and is a useful
        // fallback when info.json does not include a friendly name.
        JsonDocument? dynamic = await TryReadJsonAsync(new Uri(baseUri, "/dynamic.json"), cancellationToken);
        if (dynamic is not null)
        {
            using (dynamic)
            {
                string? parsed = TryGetCleanString(dynamic.RootElement, "hostname");
                if (!string.IsNullOrWhiteSpace(parsed) && !ServerSessionInfo.IsGenericServerName(parsed))
                    return parsed;
            }
        }

        return null;
    }

    private async Task<JsonDocument?> TryReadJsonAsync(Uri uri, CancellationToken cancellationToken)
    {
        try
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            linked.CancelAfter(TimeSpan.FromMilliseconds(1000));
            string json = await _http.GetStringAsync(uri, linked.Token);
            return JsonDocument.Parse(json);
        }
        catch
        {
            return null;
        }
    }

    private static bool TryBuildServerBaseUri(
        string address,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out Uri? baseUri)
    {
        baseUri = null;
        string candidate = address.Trim();

        if (candidate.StartsWith("udp://", StringComparison.OrdinalIgnoreCase) ||
            candidate.StartsWith("tcp://", StringComparison.OrdinalIgnoreCase))
            candidate = candidate[(candidate.IndexOf("://", StringComparison.Ordinal) + 3)..];

        if (!candidate.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !candidate.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            candidate = "http://" + candidate;

        if (!Uri.TryCreate(candidate, UriKind.Absolute, out Uri? parsed) ||
            (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps))
            return false;

        baseUri = parsed;
        return true;
    }

    private static string? TryGetCleanString(JsonElement parent, string property)
    {
        if (!parent.TryGetProperty(property, out JsonElement value)) return null;
        if (value.ValueKind != JsonValueKind.String) return null;
        return CleanServerName(NullIfBlank(value.GetString()));
    }

    private static string? CleanServerName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        var builder = new StringBuilder(value.Length);
        for (int i = 0; i < value.Length; i++)
        {
            if (value[i] == '^' && i + 1 < value.Length && char.IsDigit(value[i + 1]))
            {
                i++;
                continue;
            }
            builder.Append(value[i]);
        }

        string cleaned = string.Join(" ", builder.ToString()
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

        if (string.IsNullOrWhiteSpace(cleaned) || ServerSessionInfo.IsGenericServerName(cleaned))
            return null;

        return cleaned.Trim();
    }

    private static string NormalizeAddress(string address)
        => address.Trim().TrimEnd('/').ToLowerInvariant();

    private static string? NullIfBlank(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool IsLoopback(Uri uri)
    {
        if (string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase)) return true;
        return IPAddress.TryParse(uri.Host, out IPAddress? address) && IPAddress.IsLoopback(address);
    }

    private async Task<JsonElement> RequestAsync(string method, object parameters, CancellationToken cancellationToken)
    {
        if (_socket is null || _socket.State != WebSocketState.Open)
            throw new IOException("FiveM DevTools is not connected.");

        int id = Interlocked.Increment(ref _requestId);
        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(new { id, method, @params = parameters });

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        linked.CancelAfter(TimeSpan.FromSeconds(2));
        await _socket.SendAsync(payload.AsMemory(), WebSocketMessageType.Text, true, linked.Token);

        while (true)
        {
            JsonElement response = await ReceiveAsync(linked.Token);
            if (!response.TryGetProperty("id", out JsonElement responseId) || responseId.GetInt32() != id)
                continue;

            if (response.TryGetProperty("error", out _))
                throw new IOException($"FiveM DevTools rejected {method}.");

            return response.TryGetProperty("result", out JsonElement result)
                ? result.Clone()
                : JsonDocument.Parse("{}").RootElement.Clone();
        }
    }

    private async Task<JsonElement> ReceiveAsync(CancellationToken cancellationToken)
    {
        if (_socket is null) throw new IOException("FiveM DevTools is not connected.");

        byte[] buffer = new byte[8192];
        using var stream = new MemoryStream();

        while (true)
        {
            ValueWebSocketReceiveResult result = await _socket.ReceiveAsync(buffer.AsMemory(), cancellationToken);
            if (result.MessageType == WebSocketMessageType.Close)
                throw new IOException("FiveM DevTools connection closed.");

            stream.Write(buffer, 0, result.Count);
            if (result.EndOfMessage) break;
        }

        using JsonDocument doc = JsonDocument.Parse(stream.ToArray());
        return doc.RootElement.Clone();
    }

    private static string? FindClientFrameId(JsonElement result)
    {
        if (!result.TryGetProperty("frameTree", out JsonElement frameTree)) return null;
        return FindClientFrameIdRecursive(frameTree);
    }

    private static string? FindClientFrameIdRecursive(JsonElement tree)
    {
        if (tree.TryGetProperty("frame", out JsonElement frame) &&
            frame.TryGetProperty("url", out JsonElement url) &&
            url.GetString()?.StartsWith(ClientFramePrefix, StringComparison.OrdinalIgnoreCase) == true &&
            frame.TryGetProperty("id", out JsonElement id))
            return id.GetString();

        if (!tree.TryGetProperty("childFrames", out JsonElement children) || children.ValueKind != JsonValueKind.Array)
            return null;

        foreach (JsonElement child in children.EnumerateArray())
        {
            string? match = FindClientFrameIdRecursive(child);
            if (match is not null) return match;
        }

        return null;
    }

    public async ValueTask DisposeAsync()
    {
        await ResetAsync();
        _http.Dispose();
    }

    private sealed class ServerHint
    {
        public string? Address { get; set; }
        public string? Name { get; set; }
    }
}
