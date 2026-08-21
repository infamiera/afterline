using System.Net;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace Afterline.Services;

public sealed class FiveMDevToolsChatReader : IAsyncDisposable
{
    private static readonly Uri TargetsUri = new("http://127.0.0.1:13172/json");
    private const string RootUiUrl = "nui://game/ui/root.html";
    private const string ClientFramePrefix = "https://cfx-nui-client/";

    // Deliberately fixed at compile time. Nothing from chat or the network is interpolated into it.
    private const string ReadChatExpression =
        "JSON.stringify(Array.from(document.querySelectorAll('.chat__messages > li'))" +
        ".map(function(el){return (el.innerText || '').replace(/\\s+/g,' ').trim();})" +
        ".filter(function(x){return x.length > 0;}))";

    private readonly HttpClient _http;
    private ClientWebSocket? _socket;
    private int _contextId;
    private int _requestId;

    public FiveMDevToolsChatReader()
    {
        var handler = new HttpClientHandler { UseProxy = false };
        _http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(2) };
    }

    public async Task<IReadOnlyList<string>> ReadVisibleLinesAsync(CancellationToken cancellationToken)
    {
        await EnsureConnectedAsync(cancellationToken);

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
}
