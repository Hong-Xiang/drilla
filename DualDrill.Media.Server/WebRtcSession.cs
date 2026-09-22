using System.Diagnostics;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Gst;
using Gst.App;
using Gst.GLib;
using Gst.Interop;
using Gst.Sdp;
using Gst.WebRTC;
using Task = System.Threading.Tasks.Task;

internal sealed class WebRtcSession : IAsyncDisposable
{
    private const int MaxSignalBytes = 64 * 1024;
    private const int MaxCandidateBytes = 4 * 1024;
    private const int MaxRemoteCandidates = 128;
    private const int MaxQueuedSignals = 256;
    private static readonly TimeSpan AnswerTimeout = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan BusPollInterval = TimeSpan.FromMilliseconds(100);
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private static readonly string[] RequiredElements =
    [
        "appsrc",
        "videoconvert",
        "vp8enc",
        "rtpvp8pay",
        "webrtcbin",
        "nicesrc",
        "dtlssrtpenc",
    ];

    private readonly WebSocket _socket;
    private readonly ILogger<WebRtcSession> _logger;
    private readonly VideoSettings _video;
    private readonly CancellationTokenSource _stop;
    private readonly Channel<OutboundSignal> _outgoing = Channel.CreateBounded<OutboundSignal>(
        new BoundedChannelOptions(MaxQueuedSignals)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait,
        });
    private readonly TaskCompletionSource<string> _failure =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly object _remoteCandidateLock = new();
    private readonly List<(uint MLineIndex, string Candidate)> _pendingRemoteCandidates = [];

    private Pipeline? _pipeline;
    private AppSrc? _source;
    private Element? _webrtc;
    private Bus? _bus;
    private CpuBgraInput? _input;
    private GpuFrames? _gpu;
    private byte[]? _pixels;
    private Promise? _offerPromise;
    private Promise? _setLocalOfferPromise;
    private Promise? _setRemoteAnswerPromise;
    private ulong _negotiationHandler;
    private ulong _iceHandler;
    private EventHandler<AppSrc.NeedDataSignalArgs>? _needDataHandler;
    private EventHandler? _enoughDataHandler;
    private Action<Exception>? _exceptionTrap;
    private Task? _sendTask;
    private Task? _receiveTask;
    private Task? _producerTask;
    private Task? _busTask;
    private Task? _answerTimeoutTask;
    private volatile bool _hungry;
    private volatile bool _remoteDescriptionSet;
    private int _remoteCandidateCount;
    private int _negotiationStarted;
    private int _offerSent;
    private int _answerReceived;
    private int _disposed;

    internal WebRtcSession(
        WebSocket socket,
        ILogger<WebRtcSession> logger,
        VideoSettings video,
        CancellationToken requestAborted)
    {
        _socket = socket;
        _logger = logger;
        _video = video;
        _stop = CancellationTokenSource.CreateLinkedTokenSource(requestAborted);
    }

    internal static void EnsureNativeElements()
    {
        string[] missing = RequiredElements.Where(name => ElementFactory.Find(name) is null).ToArray();

        if (missing.Length != 0)
        {
            throw new InvalidOperationException(
                $"Missing required GStreamer elements: {string.Join(", ", missing)}.");
        }
    }

    internal async Task RunAsync()
    {
        _sendTask = SendLoopAsync(_stop.Token);
        _receiveTask = ReceiveLoopAsync(_stop.Token);

        try
        {
            _gpu = await GpuFrames.CreateAsync(_video, _stop.Token);
            _logger.LogInformation(
                "GPU source: {Device}, {Backend}, {AdapterType}",
                _gpu.AdapterInfo.Device, _gpu.AdapterInfo.BackendType, _gpu.AdapterInfo.AdapterType);
            _logger.LogInformation(
                "Video: {Width}x{Height} at {FramesPerSecond} fps; VP8 target bitrate: {TargetBitrate}",
                _video.Width,
                _video.Height,
                _video.FramesPerSecond,
                _video.TargetBitrate?.ToString() ?? "native default (not explicitly set)");
            InitializePipeline();
            _producerTask = ProduceFramesAsync(_stop.Token);
            _busTask = Task.Run(() => MonitorBus(_stop.Token), CancellationToken.None);
            _answerTimeoutTask = WaitForAnswerAsync(_stop.Token);

            if (_pipeline!.SetState(State.Playing) == StateChangeReturn.Failure)
            {
                throw new InvalidOperationException("The media pipeline refused to enter PLAYING.");
            }
        }
        catch (Exception exception)
        {
            Fail("The media session could not start.", exception);
        }

        if (!_failure.Task.IsCompleted)
        {
            Task completed = await Task.WhenAny(
                _receiveTask,
                _failure.Task,
                _busTask!,
                _answerTimeoutTask!);

            try
            {
                await completed;
            }
            catch (OperationCanceledException) when (_stop.IsCancellationRequested)
            {
            }
        }

        if (_failure.Task.IsCompletedSuccessfully &&
            _socket.State is WebSocketState.Open)
        {
            string reason = await _failure.Task;
            await TrySendFinalErrorAsync(reason);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _stop.Cancel();
        await AwaitExpectedCancellation(_producerTask);
        await AwaitExpectedCancellation(_receiveTask);

        _pipeline?.SetState(State.Null);
        await AwaitExpectedCancellation(_busTask);
        DisconnectNativeHandlers();

        _offerPromise?.Dispose();
        _setLocalOfferPromise?.Dispose();
        _setRemoteAnswerPromise?.Dispose();
        _pipeline?.Dispose();
        _pipeline = null;

        _outgoing.Writer.TryComplete();
        await AwaitExpectedCancellation(_sendTask);

        if (_socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
        {
            try
            {
                await _socket.CloseOutputAsync(
                    WebSocketCloseStatus.NormalClosure,
                    "session ended",
                    CancellationToken.None);
            }
            catch (WebSocketException exception)
            {
                _logger.LogDebug(exception, "The browser disconnected before the close reply.");
            }
        }

        _socket.Dispose();
        _stop.Dispose();
        GstSharp.DrainPendingReleases();
        _gpu?.Dispose();
    }

    private void InitializePipeline()
    {
        Pipeline pipeline = Pipeline.New("gpu-webrtc");
        _pipeline = pipeline;

        _source = Make<AppSrc>("appsrc", "cpu-source");
        Element convert = Make<Element>("videoconvert", "convert");
        Element encoder = Make<Element>("vp8enc", "encoder");
        Element payloader = Make<Element>("rtpvp8pay", "payloader");
        _webrtc = Make<Element>("webrtcbin", "peer");

        using Caps rtpCaps = Caps.FromString(
            "application/x-rtp,media=video,encoding-name=VP8,payload=96,clock-rate=90000")
            ?? throw new InvalidOperationException("Could not parse the VP8 RTP caps.");

        _input = new CpuBgraInput(_source, _video);

        encoder.SetProperty("deadline", 1L);
        encoder.SetProperty("cpu-used", 8);
        encoder.SetProperty("keyframe-max-dist", _video.FramesPerSecond);
        if (_video.TargetBitrate is { } targetBitrate)
        {
            encoder.SetProperty("target-bitrate", checked((uint)targetBitrate));
        }
        payloader.SetProperty("pt", 96u);

        if (!pipeline.AddMany(_source, convert, encoder, payloader, _webrtc) ||
            !_source.Link(convert, encoder, payloader) ||
            !payloader.LinkFiltered(_webrtc, rtpCaps))
        {
            throw new InvalidOperationException("Could not assemble the appsrc-to-webrtcbin pipeline.");
        }

        _pixels = new byte[_video.FrameBytes];
        _bus = pipeline.GetBus();

        _needDataHandler = (_, _) => Callback("need-data", () => _hungry = true);
        _enoughDataHandler = (_, _) => Callback("enough-data", () => _hungry = false);
        _source.NeedData += _needDataHandler;
        _source.EnoughData += _enoughDataHandler;

        _negotiationHandler = _webrtc.ConnectSignal("on-negotiation-needed", (_, _) =>
        {
            Callback("on-negotiation-needed", StartOffer);
            return null;
        });
        _iceHandler = _webrtc.ConnectSignal("on-ice-candidate", (_, arguments) =>
        {
            Callback("on-ice-candidate", () => QueueLocalCandidate(arguments));
            return null;
        });

        _exceptionTrap = exception => Fail("A native callback failed.", exception);
        ExceptionTrap.UnhandledException += _exceptionTrap;
    }

    private async Task ProduceFramesAsync(CancellationToken cancellationToken)
    {
        try
        {
            var elapsed = Stopwatch.StartNew();
            TimeSpan deadline = _video.FrameInterval;
            ulong frameNumber = 0;

            while (true)
            {
                await DelayUntilAsync(elapsed, deadline, cancellationToken);

                if (_hungry)
                {
                    await _gpu!.RenderAsync(_pixels!, elapsed.Elapsed, cancellationToken);
                    FlowReturn result = _input!.Push(_pixels);

                    if (cancellationToken.IsCancellationRequested &&
                        result == FlowReturn.Flushing)
                    {
                        return;
                    }

                    if (result != FlowReturn.Ok)
                    {
                        Fail($"appsrc rejected frame {frameNumber} with {result}.");
                        return;
                    }

                    frameNumber++;
                }

                long skippedIntervals =
                    (elapsed.Elapsed - deadline).Ticks / _video.FrameInterval.Ticks;
                deadline += TimeSpan.FromTicks(
                    checked(_video.FrameInterval.Ticks * (skippedIntervals + 1)));
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            Fail("The GPU frame producer failed.", exception);
        }
    }

    private static async Task DelayUntilAsync(
        Stopwatch elapsed,
        TimeSpan deadline,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            TimeSpan remaining = deadline - elapsed.Elapsed;
            if (remaining <= TimeSpan.Zero)
            {
                return;
            }
            await Task.Delay(
                TimeSpan.FromTicks(Math.Max(remaining.Ticks, TimeSpan.TicksPerMillisecond)),
                cancellationToken);
        }
    }

    private void StartOffer()
    {
        if (Interlocked.Exchange(ref _negotiationStarted, 1) != 0)
        {
            return;
        }

        _offerPromise = Promise.NewWithChangeFunc(_ => Callback("create-offer", OnOfferCreated));
        _webrtc!.EmitSignal("create-offer", null, _offerPromise.Handle);
    }

    private void OnOfferCreated()
    {
        if (TakeDescription(_offerPromise, "offer") is not { } offer)
        {
            return;
        }

        using (offer)
        using (SDPMessage sdp = offer.GetSdp())
        {
            string offerSdp = sdp.AsText();
            _setLocalOfferPromise = Promise.NewWithChangeFunc(
                _ => Callback("set-local-description", () => OnLocalOfferSet(offerSdp)));
            _webrtc!.EmitSignal(
                "set-local-description",
                offer,
                _setLocalOfferPromise.Handle);
        }
    }

    private void OnLocalOfferSet(string offerSdp)
    {
        if (PromiseSucceeded(_setLocalOfferPromise, "set-local-description"))
        {
            Volatile.Write(ref _offerSent, 1);
            TryQueue(JsonSerializer.Serialize(new { type = "offer", sdp = offerSdp }));
        }
    }

    private void QueueLocalCandidate(object?[] arguments)
    {
        var (mLineIndex, candidate) = ParseNativeCandidate(arguments);
        TryQueue(JsonSerializer.Serialize(new
        {
            type = "ice",
            sdpMLineIndex = mLineIndex,
            candidate,
        }));
    }

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        byte[] chunk = new byte[4096];
        using MemoryStream message = new(MaxSignalBytes);

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                ValueWebSocketReceiveResult result =
                    await _socket.ReceiveAsync(chunk.AsMemory(), cancellationToken);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    return;
                }

                if (result.MessageType != WebSocketMessageType.Text)
                {
                    Fail("Only text WebSocket messages are accepted.");
                    return;
                }

                if (message.Length + result.Count > MaxSignalBytes)
                {
                    Fail($"A signaling message exceeded {MaxSignalBytes} bytes.");
                    return;
                }

                message.Write(chunk, 0, result.Count);

                if (!result.EndOfMessage)
                {
                    continue;
                }

                string json;

                try
                {
                    json = StrictUtf8.GetString(message.GetBuffer(), 0, checked((int)message.Length));
                }
                catch (DecoderFallbackException exception)
                {
                    Fail("A signaling message was not valid UTF-8.", exception);
                    return;
                }

                message.SetLength(0);

                if (!ProcessClientSignal(json))
                {
                    return;
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (WebSocketException exception)
        {
            Fail("The browser WebSocket receive loop failed.", exception);
        }
    }

    private bool ProcessClientSignal(string json)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(
                json,
                new JsonDocumentOptions
                {
                    AllowTrailingCommas = false,
                    CommentHandling = JsonCommentHandling.Disallow,
                    MaxDepth = 4,
                });
            JsonElement root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("type", out JsonElement typeElement) ||
                typeElement.ValueKind != JsonValueKind.String)
            {
                throw new SignalException("A signaling message needs a string \"type\".");
            }

            return typeElement.GetString() switch
            {
                "answer" => ProcessAnswer(root),
                "ice" => ProcessRemoteCandidate(root),
                string type => throw new SignalException($"Unsupported signaling type \"{type}\"."),
                null => throw new SignalException("The signaling type cannot be null."),
            };
        }
        catch (JsonException exception)
        {
            Fail("Malformed signaling JSON.", exception);
            return false;
        }
        catch (SignalException exception)
        {
            Fail(exception.Message);
            return false;
        }
        catch (Exception exception)
        {
            Fail("The signaling message could not be applied.", exception);
            return false;
        }
    }

    private bool ProcessAnswer(JsonElement root)
    {
        RequireProperties(root, "type", "sdp");

        if (Volatile.Read(ref _offerSent) == 0)
        {
            throw new SignalException("An answer arrived before the server offer.");
        }

        if (Interlocked.Exchange(ref _answerReceived, 1) != 0)
        {
            throw new SignalException("An answer was already received.");
        }

        string sdp = RequiredString(root, "sdp", MaxSignalBytes);

        SDPResult parseResult = SDPMessage.NewFromText(sdp, out SDPMessage? message);

        if (message is null)
        {
            throw new SignalException("The answer contained invalid SDP.");
        }

        using (message)
        {
            if (parseResult != SDPResult.Ok)
            {
                throw new SignalException("The answer contained invalid SDP.");
            }

            using WebRTCSessionDescription answer =
                WebRTCSessionDescription.New(WebRTCSDPType.Answer, message);
            _setRemoteAnswerPromise = Promise.NewWithChangeFunc(
                _ => Callback("set-remote-description", OnRemoteAnswerSet));
            _webrtc!.EmitSignal(
                "set-remote-description",
                answer,
                _setRemoteAnswerPromise.Handle);
        }

        return true;
    }

    private bool ProcessRemoteCandidate(JsonElement root)
    {
        RequireProperties(root, "type", "sdpMLineIndex", "candidate");

        if (++_remoteCandidateCount > MaxRemoteCandidates)
        {
            throw new SignalException($"More than {MaxRemoteCandidates} remote ICE candidates were sent.");
        }

        if (!root.TryGetProperty("sdpMLineIndex", out JsonElement indexElement) ||
            !indexElement.TryGetUInt32(out uint mLineIndex))
        {
            throw new SignalException("\"sdpMLineIndex\" must be an unsigned integer.");
        }

        string candidate = ParseCandidate(root.GetProperty("candidate"));

        lock (_remoteCandidateLock)
        {
            if (!_remoteDescriptionSet)
            {
                _pendingRemoteCandidates.Add((mLineIndex, candidate));
                return true;
            }
        }

        _webrtc!.EmitSignal("add-ice-candidate", mLineIndex, candidate);
        return true;
    }

    private void OnRemoteAnswerSet()
    {
        if (!PromiseSucceeded(_setRemoteAnswerPromise, "set-remote-description"))
        {
            return;
        }

        List<(uint MLineIndex, string Candidate)> pending;

        lock (_remoteCandidateLock)
        {
            _remoteDescriptionSet = true;
            pending = [.. _pendingRemoteCandidates];
            _pendingRemoteCandidates.Clear();
        }

        foreach ((uint mLineIndex, string candidate) in pending)
        {
            _webrtc!.EmitSignal("add-ice-candidate", mLineIndex, candidate);
        }

        TryQueue(JsonSerializer.Serialize(new { type = "status", message = "answer accepted" }));
    }

    private WebRTCSessionDescription? TakeDescription(Promise? promise, string field)
    {
        if (promise is null || promise.Wait() != PromiseResult.Replied)
        {
            Fail($"The {field} promise was not replied to.");
            return null;
        }

        using Structure? reply = promise.GetReply();

        if (reply is null)
        {
            Fail($"The {field} promise had no reply.");
            return null;
        }

        if (reply.HasField("error"))
        {
            Fail($"webrtcbin could not create the {field}: {reply}");
            return null;
        }

        if (reply.GetBoxed<WebRTCSessionDescription>(field) is not { } description)
        {
            Fail($"The {field} promise had no session description.");
            return null;
        }

        return description;
    }

    private bool PromiseSucceeded(Promise? promise, string operation)
    {
        if (promise is null || promise.Wait() != PromiseResult.Replied)
        {
            Fail($"The {operation} promise was not replied to.");
            return false;
        }

        using Structure? reply = promise.GetReply();

        if (reply?.HasField("error") == true)
        {
            Fail($"webrtcbin rejected {operation}: {reply}");
            return false;
        }

        return true;
    }

    private void MonitorBus(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                using Message? message = _bus!.TimedPopFiltered(
                    ClockTime.FromNanoseconds((ulong)BusPollInterval.TotalNanoseconds),
                    MessageType.Error | MessageType.Eos);

                if (message is null)
                {
                    GstSharp.DrainPendingReleases();
                    continue;
                }

                if (message.Type == MessageType.Error)
                {
                    (GException error, string? debug) = message.ParseError();
                    Fail(
                        $"GStreamer error from {message.SourceName ?? "unknown"}: {error.Message}" +
                        (debug is null ? string.Empty : $" ({debug})"));
                    return;
                }

                Fail("The media pipeline ended unexpectedly.");
                return;
            }
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            Fail("The GStreamer bus monitor failed.", exception);
        }
    }

    private async Task WaitForAnswerAsync(CancellationToken cancellationToken)
    {
        await Task.Delay(AnswerTimeout, cancellationToken);

        if (!_remoteDescriptionSet)
        {
            Fail($"No valid browser answer completed within {AnswerTimeout.TotalSeconds:F0} seconds.");
            return;
        }

        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
    }

    private async Task SendLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (OutboundSignal signal in _outgoing.Reader.ReadAllAsync(cancellationToken))
            {
                byte[] payload = Encoding.UTF8.GetBytes(signal.Json);
                await _socket.SendAsync(
                    payload.AsMemory(),
                    WebSocketMessageType.Text,
                    endOfMessage: true,
                    cancellationToken);
                signal.Sent?.TrySetResult();
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            Fail("The WebSocket send loop failed.", exception);
        }
    }

    private async Task TrySendFinalErrorAsync(string reason)
    {
        var sent = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var signal = new OutboundSignal(
            JsonSerializer.Serialize(new { type = "error", message = reason }),
            sent);

        try
        {
            using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(2));
            await _outgoing.Writer.WriteAsync(signal, timeout.Token);
            await sent.Task.WaitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("Timed out sending the final signaling error.");
        }
        catch (ChannelClosedException)
        {
            _logger.LogDebug("The signaling channel closed before the final error was sent.");
        }
    }

    private void TryQueue(string json)
    {
        if (!_outgoing.Writer.TryWrite(new OutboundSignal(json, null)))
        {
            Fail("The bounded signaling send queue is full.");
        }
    }

    private void Callback(string name, Action callback)
    {
        try
        {
            callback();
        }
        catch (Exception exception)
        {
            Fail($"The {name} callback failed.", exception);
        }
    }

    private void Fail(string message, Exception? exception = null)
    {
        if (!_failure.TrySetResult(message))
        {
            return;
        }

        if (exception is null)
        {
            _logger.LogError("{Message}", message);
        }
        else
        {
            _logger.LogError(exception, "{Message}", message);
        }
    }

    private void DisconnectNativeHandlers()
    {
        if (_exceptionTrap is { } exceptionTrap)
        {
            ExceptionTrap.UnhandledException -= exceptionTrap;
            _exceptionTrap = null;
        }

        if (_source is { } source)
        {
            if (_needDataHandler is { } needData)
            {
                source.NeedData -= needData;
            }

            if (_enoughDataHandler is { } enoughData)
            {
                source.EnoughData -= enoughData;
            }
        }

        if (_webrtc is { } webrtc)
        {
            if (_negotiationHandler != 0)
            {
                webrtc.RemoveHandler(_negotiationHandler);
            }

            if (_iceHandler != 0)
            {
                webrtc.RemoveHandler(_iceHandler);
            }
        }
    }

    private static T Make<T>(string factory, string name)
        where T : Element =>
        ElementFactory.Make(factory, name) as T
        ?? throw new InvalidOperationException($"Could not create GStreamer element \"{factory}\".");

    private static string RequiredString(JsonElement root, string name, int maxBytes)
    {
        if (!root.TryGetProperty(name, out JsonElement element) ||
            element.ValueKind != JsonValueKind.String ||
            element.GetString() is not { Length: > 0 } value ||
            Encoding.UTF8.GetByteCount(value) > maxBytes)
        {
            throw new SignalException(
                $"\"{name}\" must be a non-empty string no larger than {maxBytes} UTF-8 bytes.");
        }

        return value;
    }

    private static (uint Index, string Candidate) ParseNativeCandidate(object?[] arguments) =>
        arguments switch
        {
            [uint index, string candidate] => (index, ValidateCandidate(candidate)),
            [uint index, null] => (index, string.Empty),
            _ => throw new SignalException("webrtcbin emitted an invalid ICE candidate."),
        };

    private static string ParseCandidate(JsonElement value) =>
        value.ValueKind == JsonValueKind.String && value.GetString() is { } candidate
            ? ValidateCandidate(candidate)
            : throw new SignalException("\"candidate\" must be a string.");

    private static string ValidateCandidate(string candidate) =>
        Encoding.UTF8.GetByteCount(candidate) <= MaxCandidateBytes
            ? candidate
            : throw new SignalException($"An ICE candidate exceeded {MaxCandidateBytes} UTF-8 bytes.");

    internal static int RunSignalSelfTest()
    {
        using var document = JsonDocument.Parse("""{"candidate":""}""");
        if (ParseNativeCandidate([0u, ""]) != (0u, "")
            || ParseNativeCandidate([0u, null]) != (0u, "")
            || ParseCandidate(document.RootElement.GetProperty("candidate")) != "")
        {
            throw new InvalidOperationException("ICE end-of-candidates parsing failed.");
        }
        Console.WriteLine("Native and browser ICE end-of-candidates parsing passed.");
        return 0;
    }

    private static void RequireProperties(JsonElement root, params string[] expected)
    {
        HashSet<string> remaining = [.. expected];

        foreach (JsonProperty property in root.EnumerateObject())
        {
            if (!remaining.Remove(property.Name))
            {
                throw new SignalException($"Unexpected or duplicate property \"{property.Name}\".");
            }
        }

        if (remaining.Count != 0)
        {
            throw new SignalException($"Missing property \"{remaining.First()}\".");
        }
    }

    private static async Task AwaitExpectedCancellation(Task? task)
    {
        if (task is null)
        {
            return;
        }

        try
        {
            await task;
        }
        catch (OperationCanceledException)
        {
        }
    }

    private sealed record OutboundSignal(string Json, TaskCompletionSource? Sent);

    private sealed class SignalException(string message) : Exception(message);
}
