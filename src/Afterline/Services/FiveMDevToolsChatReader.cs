using System.Collections.Concurrent;
using System.Net;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Afterline.Models;

namespace Afterline.Services;

internal sealed record ObservedChatSnapshot(
    IReadOnlyList<CapturedChatLine> Lines,
    DateTimeOffset ObservedAtUtc);

public sealed class FiveMDevToolsChatReader : IAsyncDisposable
{
    private static readonly Uri TargetsUri = new("http://127.0.0.1:13172/json");
    private const string RootUiUrl = "nui://game/ui/root.html";
    private const string ClientFramePrefix = "https://cfx-nui-client/";
    private const string ChatChangedBinding = "afterlineChatChanged";

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
          function readPseudoColor(node,pseudo){
            try {
              var computed=window.getComputedStyle(node,pseudo);
              var value=computed.color||'';
              var values=value.match(/[\d.]+/g)||[];
              var italic=computed.fontStyle==='italic'||computed.fontStyle==='oblique';
              if(values.length<3) return {Red:168,Green:178,Blue:190,Alpha:255,Italic:italic};
              return {
                Red:Math.max(0,Math.min(255,Math.round(Number(values[0])))),
                Green:Math.max(0,Math.min(255,Math.round(Number(values[1])))),
                Blue:Math.max(0,Math.min(255,Math.round(Number(values[2])))),
                Alpha:values.length>3?Math.max(0,Math.min(255,Math.round(Number(values[3])*255))):255,
                Italic:italic
              };
            } catch (_) {
              return {Red:168,Green:178,Blue:190,Alpha:255,Italic:false};
            }
          }
          function hiddenTimestamp(row){
            var nodes=[row].concat(Array.from(row.querySelectorAll('*')));
            var pattern=/\b\d{1,2}:\d{2}:\d{2}\b/;
            for(var i=0;i<nodes.length;i++){
              var node=nodes[i];
              var attributes=Array.from(node.attributes||[]);
              for(var j=0;j<attributes.length;j++){
                var attributeMatch=String(attributes[j].value||'').match(pattern);
                if(attributeMatch) return {value:attributeMatch[0],color:readColor(node)};
              }
              var before=String(window.getComputedStyle(node,'::before').content||'');
              var beforeMatch=before.match(pattern);
              if(beforeMatch) return {value:beforeMatch[0],color:readPseudoColor(node,'::before')};
              var after=String(window.getComputedStyle(node,'::after').content||'');
              var afterMatch=after.match(pattern);
              if(afterMatch) return {value:afterMatch[0],color:readPseudoColor(node,'::after')};
            }
            return null;
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
            var result=text===legacyText
              ? {Text:text,ColorRuns:runs}
              : {Text:legacyText,ColorRuns:[]};
            if(!/^\[\d{1,2}:\d{2}:\d{2}\]/.test(result.Text)){
              var hidden=hiddenTimestamp(row);
              if(hidden){
                var prefix='['+hidden.value+'] ';
                result.ColorRuns=result.ColorRuns.map(function(run){
                  run.Start+=prefix.length;
                  return run;
                });
                result.ColorRuns.unshift({Start:0,Length:prefix.length,Red:hidden.color.Red,Green:hidden.color.Green,Blue:hidden.color.Blue,Alpha:hidden.color.Alpha,Italic:hidden.color.Italic});
                result.Text=prefix+result.Text;
              }
            }
            return result;
          }
          return Array.from(document.querySelectorAll('.chat__messages > li'))
            .map(readRow)
            .filter(function(line){return line.Text.length>0;});
        })())
        """;

    private static readonly string InstallChatObserverExpression =
        "(function(){" +
        "var binding=window['" + ChatChangedBinding + "'];" +
        "if(typeof binding!=='function') return false;" +
        "var previous=window.__afterlineChatObserver;" +
        "if(previous){try{previous.chatObserver&&previous.chatObserver.disconnect();}catch(_){}try{previous.rootObserver&&previous.rootObserver.disconnect();}catch(_){}try{previous.messageHandler&&window.removeEventListener('message',previous.messageHandler,true);}catch(_){}}" +
        "var state={chat:null,chatObserver:null,rootObserver:null,messageHandler:null,timer:0,frame:0,firstMutationAt:0};" +
        "function emit(){state.timer=0;state.frame=0;state.firstMutationAt=0;try{var lines=" + ReadChatExpression + ";binding(JSON.stringify({ObservedAtUnixMilliseconds:Date.now(),LinesJson:lines}));}catch(_){}}" +
        "function schedule(){var now=Date.now();if(!state.firstMutationAt)state.firstMutationAt=now;if(state.timer)clearTimeout(state.timer);if(state.frame)cancelAnimationFrame(state.frame);var remaining=Math.max(0,200-(now-state.firstMutationAt));var quietDelay=Math.min(50,remaining);state.timer=setTimeout(function(){state.timer=0;state.frame=requestAnimationFrame(emit);},quietDelay);}" +
        "function attach(){var chat=document.querySelector('.chat__messages');if(chat===state.chat)return;if(state.chatObserver)state.chatObserver.disconnect();state.chat=chat;state.chatObserver=null;if(chat){state.chatObserver=new MutationObserver(schedule);state.chatObserver.observe(chat,{childList:true,subtree:true,characterData:true,attributes:true,attributeFilter:['class','style','data-time','data-timestamp','title']});schedule();}}" +
        "state.rootObserver=new MutationObserver(attach);state.rootObserver.observe(document.documentElement,{childList:true,subtree:true});" +
        "state.messageHandler=schedule;window.addEventListener('message',state.messageHandler,true);" +
        "window.__afterlineChatObserver=state;attach();return true;" +
        "})()";

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

    private static readonly string[] ServerTimeZoneProperties =
    {
        "sv_timezone",
        "server_timezone",
        "serverTimeZone",
        "timezone",
        "timeZone",
        "tz",
        "utc_offset",
        "utcOffset"
    };

    private static readonly JsonElement EmptyResult = CreateEmptyResult();

    private readonly HttpClient _http;
    private readonly ConcurrentDictionary<int, TaskCompletionSource<JsonElement>> _pendingRequests = new();
    private readonly SemaphoreSlim _connectionGate = new(1, 1);
    private readonly SemaphoreSlim _sendGate = new(1, 1);
    private readonly Channel<ObservedChatSnapshot> _observedSnapshots = Channel.CreateUnbounded<ObservedChatSnapshot>(
        new UnboundedChannelOptions { SingleReader = true, SingleWriter = true });
    private readonly Dictionary<string, string> _resolvedNames = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ServerTimeZoneHint> _resolvedTimeZones = new(StringComparer.OrdinalIgnoreCase);
    private readonly byte[] _receiveBuffer = new byte[8192];
    private ClientWebSocket? _socket;
    private CancellationTokenSource? _connectionCts;
    private Task? _receivePump;
    private int _contextId;
    private int _requestId;
    private ServerSessionInfo _currentServer = ServerSessionInfo.Unknown;
    private string? _lastResolutionAddress;
    private DateTime _lastResolutionAttemptUtc = DateTime.MinValue;
    private string? _lastTimeZoneResolutionAddress;
    private DateTime _lastTimeZoneResolutionAttemptUtc = DateTime.MinValue;
    private bool _exactColorFallbackLogged;
    private bool _observerFallbackLogged;
    private bool _eventCaptureAvailable;
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

    public async Task<ObservedChatSnapshot?> WaitForVisibleLinesChangedAsync(
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        await EnsureConnectedAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(_currentServer.Address))
            return null;

        if (!_eventCaptureAvailable)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);
            return null;
        }

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        linked.CancelAfter(timeout);
        try
        {
            ObservedChatSnapshot snapshot = await _observedSnapshots.Reader.ReadAsync(linked.Token);
            CapturedChatLine[] normalized = snapshot.Lines
                .Where(line => !string.IsNullOrWhiteSpace(line.Text))
                .Select(NormalizeCapturedLine)
                .ToArray();
            _lastExactVisibleText = normalized.Select(line => line.Text).ToArray();
            return new ObservedChatSnapshot(normalized, snapshot.ObservedAtUtc);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
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
        CancellationTokenSource? connectionCts = Interlocked.Exchange(ref _connectionCts, null);
        Task? receivePump = Interlocked.Exchange(ref _receivePump, null);
        ClientWebSocket? socket = Interlocked.Exchange(ref _socket, null);
        connectionCts?.Cancel();
        if (socket is not null)
        {
            try
            {
                // This is a recovery path. A graceful close can itself wait on
                // the unresponsive DevTools endpoint that prompted the reset.
                socket.Abort();
            }
            catch { }
            finally
            {
                socket.Dispose();
            }
        }

        if (receivePump is not null)
        {
            try { await receivePump; }
            catch { }
        }
        connectionCts?.Dispose();

        var resetException = new IOException("FiveM DevTools connection was reset.");
        foreach ((int id, TaskCompletionSource<JsonElement> completion) in _pendingRequests)
        {
            if (_pendingRequests.TryRemove(id, out _))
                completion.TrySetException(resetException);
        }
        while (_observedSnapshots.Reader.TryRead(out _)) { }

        _contextId = 0;
        _requestId = 0;
        _currentServer = ServerSessionInfo.Unknown;
        _lastResolutionAddress = null;
        _lastResolutionAttemptUtc = DateTime.MinValue;
        _lastTimeZoneResolutionAddress = null;
        _lastTimeZoneResolutionAttemptUtc = DateTime.MinValue;
        _lastExactVisibleText = Array.Empty<string>();
        _eventCaptureAvailable = false;
    }

    private async Task EnsureConnectedAsync(CancellationToken cancellationToken)
    {
        if (_socket?.State == WebSocketState.Open && _contextId != 0) return;

        await _connectionGate.WaitAsync(cancellationToken);
        try
        {
            if (_socket?.State == WebSocketState.Open && _contextId != 0) return;
            await EnsureConnectedCoreAsync(cancellationToken);
        }
        finally
        {
            _connectionGate.Release();
        }
    }

    private async Task EnsureConnectedCoreAsync(CancellationToken cancellationToken)
    {
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

        var socket = new ClientWebSocket();
        socket.Options.Proxy = null;
        await socket.ConnectAsync(socketUri, linked.Token);
        _socket = socket;
        var connectionCts = new CancellationTokenSource();
        _connectionCts = connectionCts;
        _receivePump = Task.Run(() => ReceivePumpAsync(socket, connectionCts.Token));

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

        try
        {
            await RequestAsync("Runtime.enable", new { }, linked.Token);
            await RequestAsync("Runtime.addBinding", new
            {
                name = ChatChangedBinding,
                executionContextId = _contextId
            }, linked.Token);

            JsonElement observer = await RequestAsync("Runtime.evaluate", new
            {
                expression = InstallChatObserverExpression,
                contextId = _contextId,
                returnByValue = true
            }, linked.Token);
            _eventCaptureAvailable =
                observer.TryGetProperty("result", out JsonElement observerResult) &&
                observerResult.TryGetProperty("value", out JsonElement observerValue) &&
                observerValue.ValueKind == JsonValueKind.True;
            if (!_eventCaptureAvailable)
                throw new IOException("FiveM chat observer could not be installed.");

            _observerFallbackLogged = false;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _eventCaptureAvailable = false;
            if (!_observerFallbackLogged)
            {
                _observerFallbackLogged = true;
                DiagnosticLogger.Warn(
                    $"Immediate FiveM chat events are unavailable; using rapid reconciliation capture. {ex.Message}");
            }
        }
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

        ServerTimeZoneHint timeZone = _resolvedTimeZones.TryGetValue(
            normalizedAddress,
            out ServerTimeZoneHint cachedTimeZone)
            ? cachedTimeZone
            : ServerTimeZoneHint.Empty;
        bool shouldResolveTimeZone = timeZone == ServerTimeZoneHint.Empty &&
            (!string.Equals(
                 normalizedAddress,
                 _lastTimeZoneResolutionAddress,
                 StringComparison.OrdinalIgnoreCase) ||
             DateTime.UtcNow - _lastTimeZoneResolutionAttemptUtc >= TimeSpan.FromMinutes(15));
        if (shouldResolveTimeZone)
        {
            _lastTimeZoneResolutionAddress = normalizedAddress;
            _lastTimeZoneResolutionAttemptUtc = DateTime.UtcNow;
            ServerTimeZoneHint resolved = await TryResolveServerTimeZoneAsync(
                address,
                cancellationToken);
            if (resolved != ServerTimeZoneHint.Empty)
            {
                timeZone = resolved;
                _resolvedTimeZones[normalizedAddress] = resolved;
            }
        }

        _currentServer = new ServerSessionInfo
        {
            Address = address,
            Name = name,
            TimeZoneIdHint = timeZone.TimeZoneId,
            UtcOffsetMinutesHint = timeZone.UtcOffsetMinutes
        };
    }

    private async Task<ServerTimeZoneHint> TryResolveServerTimeZoneAsync(
        string address,
        CancellationToken cancellationToken)
    {
        if (!TryBuildServerBaseUri(address, out Uri? baseUri))
            return ServerTimeZoneHint.Empty;

        JsonDocument? info = await TryReadJsonAsync(
            new Uri(baseUri, "/info.json"),
            cancellationToken);
        if (info is null) return ServerTimeZoneHint.Empty;

        using (info)
        {
            IEnumerable<JsonElement> sources = info.RootElement
                .TryGetProperty("vars", out JsonElement vars) && vars.ValueKind == JsonValueKind.Object
                ? new[] { vars, info.RootElement }
                : new[] { info.RootElement };

            foreach (JsonElement source in sources)
            {
                foreach (string property in ServerTimeZoneProperties)
                {
                    string? value = TryGetRawString(source, property);
                    if (ServerTimeService.TryParseServerHint(
                            value,
                            out string? timeZoneId,
                            out int? utcOffsetMinutes))
                        return new ServerTimeZoneHint(timeZoneId, utcOffsetMinutes);
                }
            }
        }

        return ServerTimeZoneHint.Empty;
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

    private static string? TryGetRawString(JsonElement parent, string property)
    {
        if (!parent.TryGetProperty(property, out JsonElement value)) return null;
        return value.ValueKind switch
        {
            JsonValueKind.String => NullIfBlank(value.GetString()),
            JsonValueKind.Number => value.GetRawText(),
            _ => null
        };
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
        ClientWebSocket? socket = _socket;
        if (socket is null || socket.State != WebSocketState.Open)
            throw new IOException("FiveM DevTools is not connected.");

        int id = Interlocked.Increment(ref _requestId);
        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(new { id, method, @params = parameters });
        var completion = new TaskCompletionSource<JsonElement>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_pendingRequests.TryAdd(id, completion))
            throw new IOException("FiveM DevTools request identifier collision.");

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        linked.CancelAfter(TimeSpan.FromSeconds(2));
        try
        {
            await _sendGate.WaitAsync(linked.Token);
            try
            {
                await socket.SendAsync(
                    payload.AsMemory(),
                    WebSocketMessageType.Text,
                    true,
                    linked.Token);
            }
            finally
            {
                _sendGate.Release();
            }

            JsonElement response = await completion.Task.WaitAsync(linked.Token);

            if (response.TryGetProperty("error", out _))
                throw new IOException($"FiveM DevTools rejected {method}.");

            return response.TryGetProperty("result", out JsonElement result)
                ? result.Clone()
                : EmptyResult;
        }
        finally
        {
            _pendingRequests.TryRemove(id, out _);
        }
    }

    private async Task ReceivePumpAsync(
        ClientWebSocket socket,
        CancellationToken cancellationToken)
    {
        Exception? failure = null;
        try
        {
            while (!cancellationToken.IsCancellationRequested &&
                   socket.State == WebSocketState.Open)
            {
                JsonElement message = await ReceiveAsync(socket, cancellationToken);
                if (message.TryGetProperty("id", out JsonElement responseId) &&
                    responseId.TryGetInt32(out int id) &&
                    _pendingRequests.TryRemove(id, out TaskCompletionSource<JsonElement>? completion))
                {
                    completion.TrySetResult(message);
                    continue;
                }

                HandleProtocolEvent(message);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            failure = ex;
        }
        finally
        {
            Exception reason = failure ?? new IOException("FiveM DevTools connection closed.");
            foreach ((int id, TaskCompletionSource<JsonElement> completion) in _pendingRequests)
            {
                if (_pendingRequests.TryRemove(id, out _))
                    completion.TrySetException(reason);
            }

            if (failure is not null)
            {
                try { socket.Abort(); }
                catch { }
            }
        }
    }

    private void HandleProtocolEvent(JsonElement message)
    {
        if (!message.TryGetProperty("method", out JsonElement method) ||
            !string.Equals(method.GetString(), "Runtime.bindingCalled", StringComparison.Ordinal) ||
            !message.TryGetProperty("params", out JsonElement parameters) ||
            !parameters.TryGetProperty("name", out JsonElement name) ||
            !string.Equals(name.GetString(), ChatChangedBinding, StringComparison.Ordinal) ||
            !parameters.TryGetProperty("payload", out JsonElement payloadElement))
        {
            return;
        }

        try
        {
            if (!TryDecodeObserverPayload(
                    payloadElement.GetString() ?? string.Empty,
                    out ObservedChatSnapshot? snapshot) ||
                snapshot is null)
                return;

            _observedSnapshots.Writer.TryWrite(snapshot);
        }
        catch (Exception ex)
        {
            DiagnosticLogger.Error("FiveM chat-change event could not be decoded.", ex);
        }
    }

    private static bool TryDecodeObserverPayload(
        string payload,
        out ObservedChatSnapshot? snapshot)
    {
        snapshot = null;
        try
        {
            ObserverEnvelope? envelope = JsonSerializer.Deserialize<ObserverEnvelope>(payload);
            if (envelope is null || string.IsNullOrWhiteSpace(envelope.LinesJson))
                return false;

            CapturedChatLine[] lines = JsonSerializer.Deserialize<CapturedChatLine[]>(
                envelope.LinesJson) ?? Array.Empty<CapturedChatLine>();
            DateTimeOffset observedAt = envelope.ObservedAtUnixMilliseconds > 0
                ? DateTimeOffset.FromUnixTimeMilliseconds(envelope.ObservedAtUnixMilliseconds)
                : DateTimeOffset.UtcNow;
            snapshot = new ObservedChatSnapshot(lines, observedAt);
            return true;
        }
        catch
        {
            return false;
        }
    }

    internal static void RunEventCaptureSmokeTest()
    {
        var line = new CapturedChatLine(
            "[15:27:41] (( test ))",
            new[] { new ChatColorRun(0, 23, 220, 30, 40) });
        const long observedMilliseconds = 1_788_797_261_250;
        string payload = JsonSerializer.Serialize(new ObserverEnvelope
        {
            ObservedAtUnixMilliseconds = observedMilliseconds,
            LinesJson = JsonSerializer.Serialize(new[] { line })
        });

        if (!TryDecodeObserverPayload(payload, out ObservedChatSnapshot? snapshot) ||
            snapshot is null ||
            snapshot.ObservedAtUtc != DateTimeOffset.FromUnixTimeMilliseconds(observedMilliseconds) ||
            snapshot.Lines.Count != 1 ||
            snapshot.Lines[0].Text != line.Text ||
            snapshot.Lines[0].ColorRuns.Count != 1)
        {
            throw new InvalidOperationException("Immediate FiveM chat event decoding failed.");
        }

        string[] requiredObserverFeatures =
        {
            "new MutationObserver",
            "requestAnimationFrame",
            "ObservedAtUnixMilliseconds:Date.now()",
            "getComputedStyle(node,'::before')",
            ChatChangedBinding
        };
        if (requiredObserverFeatures.Any(feature =>
                !InstallChatObserverExpression.Contains(feature, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException("Immediate FiveM chat observer is incomplete.");
        }
    }

    private async Task<JsonElement> ReceiveAsync(
        ClientWebSocket socket,
        CancellationToken cancellationToken)
    {
        ValueWebSocketReceiveResult first = await socket.ReceiveAsync(
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
            ValueWebSocketReceiveResult result = await socket.ReceiveAsync(
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

    private sealed class ObserverEnvelope
    {
        public long ObservedAtUnixMilliseconds { get; set; }
        public string LinesJson { get; set; } = string.Empty;
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
        _connectionGate.Dispose();
        _sendGate.Dispose();
        _http.Dispose();
    }

    private sealed class ServerHint
    {
        public string? Address { get; set; }
        public string? Name { get; set; }
    }

    private sealed record ServerTimeZoneHint(
        string? TimeZoneId,
        int? UtcOffsetMinutes)
    {
        public static ServerTimeZoneHint Empty { get; } = new(null, null);
    }
}
