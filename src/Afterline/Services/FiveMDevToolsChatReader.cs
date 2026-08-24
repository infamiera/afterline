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

    private const string ReadChatExpression = """
        JSON.stringify((function(){
          function readColor(node){
            try {
              var element=node.nodeType===1?node:node.parentElement;
              var computed=window.getComputedStyle(element);
              var value=computed.color||'';
              var values=value.match(/[\d.]+/g)||[];
              var italic=computed.fontStyle==='italic'||computed.fontStyle==='oblique';
              if(values.length<3) return {Red:255,Green:255,Blue:255,Alpha:255,Italic:italic};
              return {
                Red:Math.max(0,Math.min(255,Math.round(Number(values[0])))),
                Green:Math.max(0,Math.min(255,Math.round(Number(values[1])))),
                Blue:Math.max(0,Math.min(255,Math.round(Number(values[2])))),
                Alpha:values.length>3?Math.max(0,Math.min(255,Math.round(Number(values[3])*255))):255,
                Italic:italic
              };
            } catch (_) {
              return {Red:255,Green:255,Blue:255,Alpha:255,Italic:false};
            }
          }
          function sameColor(left,right){
            return left.Red===right.Red&&left.Green===right.Green&&left.Blue===right.Blue&&
              left.Alpha===right.Alpha&&left.Italic===right.Italic;
          }
          function readRow(row){
            var chunks=[];
            function add(value,color){ if(value) chunks.push({text:value,color:color}); }
            function walk(node){
              if(node.nodeType===3){ add(node.nodeValue||'',readColor(node)); return; }
              if(node.nodeType!==1) return;
              var tag=(node.tagName||'').toUpperCase();
              if(tag==='SCRIPT'||tag==='STYLE'||tag==='NOSCRIPT') return;
              var computed=window.getComputedStyle(node);
              if(computed.display==='none'||computed.visibility==='hidden') return;
              if(tag==='BR'){ add(' ',readColor(node)); return; }
              var block=computed.display==='block'||computed.display==='flex'||computed.display==='grid'||
                computed.display==='list-item'||computed.display==='table-row';
              if(block&&chunks.length) add(' ',readColor(node));
              Array.prototype.forEach.call(node.childNodes,walk);
              if(block&&chunks.length) add(' ',readColor(node));
            }
            walk(row);
            var text='';
            var runs=[];
            var pendingSpace=false;
            function append(value,color){
              if(!value) return;
              var start=text.length;
              text+=value;
              var previous=runs.length?runs[runs.length-1]:null;
              if(previous&&previous.Start+previous.Length===start&&sameColor(previous,color)){
                previous.Length+=value.length;
              } else {
                runs.push({Start:start,Length:value.length,Red:color.Red,Green:color.Green,Blue:color.Blue,Alpha:color.Alpha,Italic:color.Italic});
              }
            }
            chunks.forEach(function(chunk){
              for(var i=0;i<chunk.text.length;i++){
                var character=chunk.text.charAt(i);
                if(/\s/.test(character)){
                  if(text.length) pendingSpace=true;
                  continue;
                }
                if(pendingSpace&&text.length) append(' ',chunk.color);
                pendingSpace=false;
                append(character,chunk.color);
              }
            });
            var legacyText=(row.innerText||'').replace(/\s+/g,' ').trim();
            return text===legacyText
              ? {Text:text,ColorRuns:runs}
              : {Text:legacyText,ColorRuns:[]};
          }
          return Array.from(document.querySelectorAll('.chat__messages > li'))
            .map(readRow)
            .filter(function(line){return line.Text.length>0;});
        })())
        """;

    private const string LegacyReadChatExpression =
        "JSON.stringify(Array.from(document.querySelectorAll('.chat__messages > li'))" +
        ".map(function(el){return (el.innerText || '').replace(/\\s+/g,' ').trim();})" +
        ".filter(function(x){return x.length > 0;}))";

    // Prefer FiveM's documented loading-screen handover data when it is exposed
    // in the evaluated NUI page. For already-connected sessions, keep only the
    // root `serverAddress` endpoint as a compatibility fallback. Friendly names
    // are resolved from documented handover fields or normal server info endpoints.
    private const string ReadServerStateExpression =
        "JSON.stringify((function(){" +
        "var h=(typeof window==='object'&&window&&typeof window.nuiHandoverData==='object'&&window.nuiHandoverData)?window.nuiHandoverData:{};" +
        "var d=(typeof h.serverAddress==='string')?h.serverAddress:'';" +
        "var a=(typeof serverAddress==='string')?serverAddress:'';" +
        "return {address:(d||a),name:(h.serverName||h.projectName||h.hostname||'')};" +
        "})())";

    private static readonly JsonSerializerOptions ServerHintJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly string[] ServerVariableNameProperties =
    {
        "sv_projectName",
        "sv_hostname",
        "serverName"
    };

    private static readonly string[] ServerRootNameProperties =
    {
        "serverName",
        "hostname",
        "name"
    };

    private static readonly JsonElement EmptyResult = CreateEmptyResult();

    private readonly HttpClient _http;
    private readonly Dictionary<string, string> _resolvedNames = new(StringComparer.OrdinalIgnoreCase);
    private readonly byte[] _receiveBuffer = new byte[8192];
    private ClientWebSocket? _socket;
    private int _contextId;
    private int _requestId;
    private ServerSessionInfo _currentServer = ServerSessionInfo.Unknown;
    private string? _lastResolutionAddress;
    private DateTime _lastResolutionAttemptUtc = DateTime.MinValue;
    private bool _exactColorFallbackLogged;
    private string[] _lastExactVisibleText = Array.Empty<string>();

    public ServerSessionInfo CurrentServer => _currentServer;

    public FiveMDevToolsChatReader()
    {
        var handler = new HttpClientHandler { UseProxy = false };
        _http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(2) };
    }

    public async Task<IReadOnlyList<CapturedChatLine>> ReadVisibleLinesAsync(CancellationToken cancellationToken)
    {
        await EnsureConnectedAsync(cancellationToken);
        await RefreshServerInfoAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(_currentServer.Address))
            throw new IOException("FiveM is running but is not currently connected to a server.");

        try
        {
            string? json = await EvaluateChatExpressionAsync(
                ReadChatExpression,
                cancellationToken);
            if (!string.IsNullOrWhiteSpace(json))
            {
                CapturedChatLine[] lines = JsonSerializer.Deserialize<CapturedChatLine[]>(json)
                    ?? Array.Empty<CapturedChatLine>();
                lines = await StabilizeNewChatRowsAsync(lines, cancellationToken);
                _exactColorFallbackLogged = false;
                return lines
                    .Where(line => !string.IsNullOrWhiteSpace(line.Text))
                    .Select(NormalizeCapturedLine)
                    .ToArray();
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            if (!_exactColorFallbackLogged)
            {
                _exactColorFallbackLogged = true;
                DiagnosticLogger.Error(
                    "FiveM exact-color extraction failed; falling back to plain chat capture.",
                    ex);
            }
        }

        string? legacyJson = await EvaluateChatExpressionAsync(
            LegacyReadChatExpression,
            cancellationToken);
        string[] legacyLines = string.IsNullOrWhiteSpace(legacyJson)
            ? Array.Empty<string>()
            : JsonSerializer.Deserialize<string[]>(legacyJson) ?? Array.Empty<string>();
        return legacyLines
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(line => new CapturedChatLine(line.Trim()))
            .ToArray();
    }

    private async Task<CapturedChatLine[]> StabilizeNewChatRowsAsync(
        CapturedChatLine[] initial,
        CancellationToken cancellationToken)
    {
        CapturedChatLine[] current = initial;
        string[] text = current.Select(line => line.Text ?? string.Empty).ToArray();
        bool visibleTextChanged = !_lastExactVisibleText.SequenceEqual(text, StringComparer.Ordinal);

        if (visibleTextChanged)
        {
            // FiveM can insert a complete text row several frames before the
            // nested action/speech spans receive their final computed colors.
            // Give every changed row four style passes; a still-flat leading
            // action receives one final guarded pass. Idle capture is untouched.
            for (int attempt = 0; attempt < 5; attempt++)
            {
                if (attempt == 4 && !ContainsFlattenedLeadingAction(current))
                    break;

                await Task.Delay(TimeSpan.FromMilliseconds(50), cancellationToken);
                string? retryJson = await EvaluateChatExpressionAsync(
                    ReadChatExpression,
                    cancellationToken);
                if (string.IsNullOrWhiteSpace(retryJson)) break;

                CapturedChatLine[] retry = JsonSerializer.Deserialize<CapturedChatLine[]>(retryJson)
                    ?? Array.Empty<CapturedChatLine>();
                if (retry.Length == 0) break;

                current = retry;
                text = current.Select(line => line.Text ?? string.Empty).ToArray();
            }
        }

        _lastExactVisibleText = text;
        return current;
    }

    internal static bool ContainsFlattenedLeadingAction(IEnumerable<CapturedChatLine> lines)
    {
        foreach (CapturedChatLine line in lines)
        {
            string text = line.Text ?? string.Empty;
            int bodyStart = 0;
            if (text.Length > 10 && text[0] == '[')
            {
                int closing = text.IndexOf(']');
                if (closing >= 0)
                {
                    bodyStart = closing + 1;
                    while (bodyStart < text.Length && char.IsWhiteSpace(text[bodyStart]))
                        bodyStart++;
                }
            }

            if (bodyStart >= text.Length || text[bodyStart] != '*') continue;
            IReadOnlyList<ChatColorRun> runs = ChatColorData.SliceRuns(
                text,
                line.ColorRuns,
                bodyStart,
                text.Length - bodyStart);
            if (runs.Count == 0 || !ChatColorData.HasCompleteCoverage(text[bodyStart..], runs))
                continue;

            if (runs.All(run =>
                    run.Alpha >= 128 &&
                    run.Red >= 135 &&
                    run.Blue >= 145 &&
                    run.Red - run.Green >= 15 &&
                    run.Blue - run.Green >= 20))
                return true;
        }

        return false;
    }

    private static CapturedChatLine NormalizeCapturedLine(CapturedChatLine line)
    {
        string source = line.Text ?? string.Empty;
        string text = source.Trim();
        int start = source.IndexOf(text, StringComparison.Ordinal);
        IReadOnlyList<ChatColorRun> runs = ChatColorData.SliceRuns(
            source,
            line.ColorRuns,
            Math.Max(0, start),
            text.Length);
        runs = ChatColorReliabilityService.EnsureExpectedAccents(text, runs);
        return new CapturedChatLine(text, runs);
    }

    private async Task<string?> EvaluateChatExpressionAsync(
        string expression,
        CancellationToken cancellationToken)
    {
        JsonElement result = await RequestAsync("Runtime.evaluate", new
        {
            expression,
            contextId = _contextId,
            returnByValue = true
        }, cancellationToken);

        if (!result.TryGetProperty("result", out JsonElement runtimeResult) ||
            !runtimeResult.TryGetProperty("value", out JsonElement valueElement))
            return null;

        return valueElement.GetString();
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
        _lastExactVisibleText = Array.Empty<string>();
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
            return JsonSerializer.Deserialize<ServerHint>(json, ServerHintJsonOptions) ?? new ServerHint();
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
                    foreach (string property in ServerVariableNameProperties)
                    {
                        string? parsed = TryGetCleanString(vars, property);
                        if (!string.IsNullOrWhiteSpace(parsed) && !ServerSessionInfo.IsGenericServerName(parsed))
                            return parsed;
                    }
                }

                foreach (string property in ServerRootNameProperties)
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
                : EmptyResult;
        }
    }

    private async Task<JsonElement> ReceiveAsync(CancellationToken cancellationToken)
    {
        if (_socket is null) throw new IOException("FiveM DevTools is not connected.");

        ValueWebSocketReceiveResult first = await _socket.ReceiveAsync(
            _receiveBuffer.AsMemory(),
            cancellationToken);
        if (first.MessageType == WebSocketMessageType.Close)
            throw new IOException("FiveM DevTools connection closed.");

        if (first.EndOfMessage)
        {
            using JsonDocument document = JsonDocument.Parse(
                _receiveBuffer.AsMemory(0, first.Count));
            return document.RootElement.Clone();
        }

        using var stream = new MemoryStream(Math.Max(16 * 1024, first.Count * 2));
        stream.Write(_receiveBuffer, 0, first.Count);

        while (true)
        {
            ValueWebSocketReceiveResult result = await _socket.ReceiveAsync(
                _receiveBuffer.AsMemory(),
                cancellationToken);
            if (result.MessageType == WebSocketMessageType.Close)
                throw new IOException("FiveM DevTools connection closed.");

            stream.Write(_receiveBuffer, 0, result.Count);
            if (result.EndOfMessage) break;
        }

        using JsonDocument doc = JsonDocument.Parse(
            stream.GetBuffer().AsMemory(0, checked((int)stream.Length)));
        return doc.RootElement.Clone();
    }

    private static JsonElement CreateEmptyResult()
    {
        using JsonDocument document = JsonDocument.Parse("{}");
        return document.RootElement.Clone();
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
