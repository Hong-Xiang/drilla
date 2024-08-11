using DnsClient.Internal;
using DualDrill.Engine;
using DualDrill.Engine.BrowserProxy;
using DualDrill.Engine.Connection;
using DualDrill.Engine.Headless;
using DualDrill.Engine.Media;
using DualDrill.WebView.Interop;
using MessagePipe;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.JSInterop;
using Microsoft.Web.WebView2.Core;
using System.Text.Json;
using System.Threading.Channels;
using System.Windows;

namespace DualDrill.WebView;

public readonly record struct SharedBufferMessage(int SlotIndex, int Offset, int Length)
{
}

public readonly record struct SharedBufferMemory(nint Ptr, int SlotIndex, int Offset, int Length)
{
    public unsafe Span<byte> Span => new Span<byte>((void*)(Ptr + Offset), Length);

    public SharedBufferMessage Message => new SharedBufferMessage(SlotIndex, Offset, Length);
}

public sealed class WebViewService : IWebViewService
{
    private System.Windows.Application? App;
    private Microsoft.Web.WebView2.Wpf.WebView2? WebView;
    private readonly TaskCompletionSource<System.Windows.Application> AppCreatedCompletionSource = new();
    private readonly TaskCompletionSource<int> UIThreadResult = new();

    private readonly Thread UIThread;
    private CoreWebView2SharedBuffer SharedBuffer;

    Channel<SharedBufferMemory> WriteBufferChannel = Channel.CreateUnbounded<SharedBufferMemory>();
    Channel<SharedBufferMemory> ReadBufferChannel = Channel.CreateUnbounded<SharedBufferMemory>();

    private IAsyncEnumerable<SharedBufferMemory> GetAllReadableSlotsAsync(CancellationToken cancellation)
        => ReadBufferChannel.Reader.ReadAllAsync(cancellation);
    private IAsyncEnumerable<SharedBufferMemory> GetAllWriteableSlotsAsync(CancellationToken cancellation)
        => WriteBufferChannel.Reader.ReadAllAsync(cancellation);

    private readonly HeadlessSurface.Option Option;
    int Width => Option.Width;
    int Height => Option.Height;
    ulong TextureBufferSize => (ulong)(4 * Width * Height);

    private HeadlessSurface Surface { get; }
    private IServer WebServer { get; }
    private IHostApplicationLifetime ApplicationLifetime { get; }

    private ILogger<WebViewService> Logger { get; }
    private ISubscriber<SharedBufferReceivedEvent> SharedBufferReceived { get; }
    private ISubscriber<Guid, CapturedStream> CapturedStream { get; }

    public WebViewService(
        HeadlessSurface surface,
        IOptions<HeadlessSurface.Option> canvasOption,
        IServer webServer,
        IHostApplicationLifetime applicationLifetime,
        ILogger<WebViewService> logger,
        ISubscriber<SharedBufferReceivedEvent> sharedBufferReceived,
        ISubscriber<Guid, CapturedStream> capturedStream)
    {
        Surface = surface;
        WebServer = webServer;
        Option = canvasOption.Value;
        UIThread = new Thread(MainUI);
        UIThread.SetApartmentState(ApartmentState.STA);
        ApplicationLifetime = applicationLifetime;
        Logger = logger;
        SharedBufferReceived = sharedBufferReceived;
        CapturedStream = capturedStream;
    }

    async ValueTask<Uri> GetHostedSourceUriAsync(CancellationToken cancellation)
    {
        var tcs = new TaskCompletionSource(cancellation);
        ApplicationLifetime.ApplicationStarted.Register(tcs.SetResult);
        await tcs.Task;
        var address = WebServer.Features.Get<IServerAddressesFeature>();
        var uri = (address?.Addresses.FirstOrDefault(x => new Uri(x).Scheme == "https")) ?? throw new ArgumentNullException("WebView2 Source Uri");
        return new Uri(uri + "/home/webview2");
    }


    public async ValueTask<Application> GetApplicationAsync()
    {
        if (AppCreatedCompletionSource.Task.IsCompleted && App is not null)
        {
            return App;
        }
        else
        {
            return await AppCreatedCompletionSource.Task;
        }
    }

    public async ValueTask SetReadyToWriteAsync(SharedBufferMessage sharedBufferMemory, CancellationToken cancellation)
    {
        await DispatchAsync(() =>
        {
            WriteBufferChannel.Writer.TryWrite(new SharedBufferMemory(SharedBuffer.Buffer, sharedBufferMemory.SlotIndex, sharedBufferMemory.Offset, sharedBufferMemory.Length));
        }, cancellation);
    }

    public void SetReadyToRead(SharedBufferMemory sharedBufferMemory)
    {
        ReadBufferChannel.Writer.TryWrite(sharedBufferMemory);
    }

    public Task<int> GetApplicationResultAsync()
    {
        return UIThreadResult.Task;
    }

    public async ValueTask StartAsync(CancellationToken cancellation)
    {
        var uri = await GetHostedSourceUriAsync(cancellation);
        var targetUri = new Uri($"{uri.Scheme}://localhost:{uri.Port}{uri.PathAndQuery}");
        UIThread.Start(targetUri);
        await WebViewInitializedTaskCompletionSource.Task.ConfigureAwait(false);
    }

    TaskCompletionSource WebViewInitializedTaskCompletionSource = new();
    void MainUI(object? data)
    {
        App = new System.Windows.Application();
        WebView = new()
        {
            Name = "DualDrillWebView2",
            Source = (Uri)data,
        };
        var mainWindow = new Window
        {
            Title = "WPF Window in Console Application",
            Width = 1920,
            Height = 1080,
            Content = WebView
        };
        WebView.CoreWebView2InitializationCompleted += (sender, e) =>
        {
            WebViewInitializedTaskCompletionSource.SetResult();
        };
        AppCreatedCompletionSource.SetResult(App);

        App.Exit += (sender, e) =>
        {
            ApplicationLifetime.StopApplication();
        };

        var result = App.Run(mainWindow);
        UIThreadResult.SetResult(result);
    }

    async ValueTask DispatchAsync(Action action, CancellationToken cancellation)
    {
        var app = await GetApplicationAsync().ConfigureAwait(false);
        await app.Dispatcher.InvokeAsync(action, System.Windows.Threading.DispatcherPriority.Normal, cancellation).Task.ConfigureAwait(false);
    }

    public async ValueTask PostSharedBufferAsync(CancellationToken cancellation)
    {
        using var tcs = new TaskCompletionSourceDotnetObjectReference<IJSObjectReference>();
        await DispatchAsync(() =>
        {
            WebView.CoreWebView2.PostSharedBufferToScript(SharedBuffer, CoreWebView2SharedBufferAccess.ReadOnly, null);
        }, cancellation);
    }

    public ValueTask<IPeerConnection> GetPeerConnectionAsync(Guid clientId)
    {
        throw new NotImplementedException();
    }

    unsafe Span<byte> GetBufferSpan(int slot)
    {
        return new Span<byte>((byte*)SharedBuffer.Buffer + (slot * (int)TextureBufferSize), (int)TextureBufferSize);
    }

    sealed record class BufferToPresent(
        Guid SurfaceId,
        int Offset,
        int Length,
        int Tick,
        int Width,
        int Height
    )
    {
        public string MessageType { get; } = nameof(BufferToPresent);
    }

    sealed record class CaptureStreamSharedBuffer(
        Guid SurfaceId
    )
    {
        public string MessageType { get; } = nameof(CaptureStreamSharedBuffer);
    }

    private async ValueTask<IMediaStream> CaptureImplAsync(HeadlessSurface surface, int frameRate, CancellationToken cancellation)
    {
        await Task.Delay(5000, cancellation);
        var result = new TaskCompletionSource<IMediaStream>();
        using var subscription = CapturedStream.Subscribe(surface.Id, result.SetResult);
        SharedBuffer = WebView.CoreWebView2.Environment.CreateSharedBuffer(TextureBufferSize * (ulong)Option.SlotCount);

        WebView.CoreWebView2.PostSharedBufferToScript(SharedBuffer, CoreWebView2SharedBufferAccess.ReadOnly,
            JsonSerializer.Serialize(new CaptureStreamSharedBuffer(surface.Id)));
        var resultStream = await result.Task;
        surface.OnFrame.Subscribe(
                    async (frame, cancellation) =>
                    {
                        await DispatchAsync(() =>
                        {
                            frame.Data.Span.CopyTo(GetBufferSpan(frame.SlotIndex));
                        }, cancellation);
                        await SendMessage(JsonSerializer.Serialize(new BufferToPresent(
                                            surface.Id,
                                            (int)(frame.SlotIndex * (int)TextureBufferSize),
                                            (int)TextureBufferSize,
                                            0,
                                            surface.Width,
                                            surface.Height
                                        )), cancellation);
                    });
        Logger.LogInformation($"Stream id {resultStream.Id}");
        return resultStream;
    }

    public async ValueTask<IMediaStream> Capture(HeadlessSurface surface, int frameRate)
    {
        var result = new TaskCompletionSource<IMediaStream>();
        await DispatchAsync(async () =>
        {
            await CaptureImplAsync(surface, frameRate, CancellationToken.None);
            result.SetResult(null);
        }, CancellationToken.None);
        return await result.Task;
    }

    public ValueTask CreateCanvas2D()
    {
        throw new NotImplementedException();
    }

    public async ValueTask SendMessage(string data, CancellationToken cancellation)
    {
        await DispatchAsync(() =>
                {


                    if (WebView is null)
                    {
                        Logger.LogWarning($"WebView is not initialized yet");
                        return;
                    }
                    WebView.CoreWebView2.Settings.IsWebMessageEnabled = true;
                    WebView.CoreWebView2.PostWebMessageAsString(data);
                }, cancellation);
    }
}
