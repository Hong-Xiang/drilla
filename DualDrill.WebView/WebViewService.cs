using DnsClient.Internal;
using DualDrill.Engine;
using DualDrill.Engine.Connection;
using DualDrill.Engine.Event;
using DualDrill.Engine.Headless;
using DualDrill.Engine.Media;
using DualDrill.Graphics;
using DualDrill.WebView.Event;
using DualDrill.WebView.Interop;
using MessagePipe;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Web.WebView2.Core;
using R3;
using SIPSorcery.Net;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Windows;

namespace DualDrill.WebView;

public sealed partial class WebViewService(
    IOptions<HeadlessSurface.Option> CanvasOption,
    IServer webServer,
    IHostApplicationLifetime applicationLifetime,
    ILogger<WebViewService> Logger,
    ISubscriber<SharedBufferReceivedEvent> sharedBufferReceived,
    ISubscriber<Guid, CapturedStream> CapturedStream,
    IPublisher<ClientEvent<PointerEvent>> PointerEventPublisher)
    : IWebViewService, IWebViewInteropService
{
    private System.Windows.Application? App;
    private Microsoft.Web.WebView2.Wpf.WebView2? WebView;
    private readonly TaskCompletionSource<System.Windows.Application> AppCreatedCompletionSource = new();
    private readonly TaskCompletionSource<int> UIThreadResult = new();


    private readonly HeadlessSurface.Option Option = CanvasOption.Value;
    int Width => Option.Width;
    int Height => Option.Height;
    ulong TextureBufferSize => (ulong)(4 * Width * Height);

    async ValueTask<Uri> GetHostedSourceUriAsync(CancellationToken cancellation)
    {
        var tcs = new TaskCompletionSource(cancellation);
        applicationLifetime.ApplicationStarted.Register(tcs.SetResult);
        await tcs.Task;
        var address = webServer.Features.Get<IServerAddressesFeature>();
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


    public async ValueTask StartAsync(CancellationToken cancellation)
    {
        var uri = await GetHostedSourceUriAsync(cancellation);
        var targetUri = new Uri($"{uri.Scheme}://localhost:{uri.Port}{uri.PathAndQuery}");
        var uiThread = new Thread(MainUI);
        uiThread.SetApartmentState(ApartmentState.STA);
        uiThread.Start(targetUri);
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
            WebView.CoreWebView2.Settings.IsWebMessageEnabled = true;

            WebView.CoreWebView2.WebMessageReceived += (sender, e) =>
            {
                var data = e.WebMessageAsJson;
                PointerEventPublisher.Publish(new(ClientsManager.ServerId,
                    JsonSerializer.Deserialize<TaggedEvent<PointerEvent>>(data, new JsonSerializerOptions()
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    }).Data));
            };
        };
        AppCreatedCompletionSource.SetResult(App);



        App.Exit += (sender, e) =>
        {
            applicationLifetime.StopApplication();
        };

        var result = App.Run(mainWindow);
    }

    async ValueTask DispatchAsync(Action action, CancellationToken cancellation)
    {
        var app = await GetApplicationAsync().ConfigureAwait(false);
        await app.Dispatcher.InvokeAsync(action, System.Windows.Threading.DispatcherPriority.Normal, cancellation).Task.ConfigureAwait(false);
    }

    private readonly ConcurrentDictionary<Guid, WebView2SharedBuffer> SharedBuffers = [];

    sealed record class WebView2SharedBuffer(
        Guid Id,
        HeadlessSurface.Option Option,
        CoreWebView2SharedBuffer Buffer
    ) : IWebViewSharedBuffer
    {
        unsafe public Span<byte> Span => new Span<byte>((byte*)Buffer.Buffer, (int)Buffer.Size);

        public Span<byte> SlotSpan(int slotIndex)
        {
            var textureBufferSize = Option.Width * Option.Height * 4;
            return Span.Slice(slotIndex * textureBufferSize, textureBufferSize);
        }
    }

    private ulong BufferSize(int width, int height, GPUTextureFormat format)
    {
        var pixels = width * height;
        var bytesPerPixel = format switch
        {
            GPUTextureFormat.BGRA8Unorm => 4,
            GPUTextureFormat.BGRA8UnormSrgb => 4,
            GPUTextureFormat.RGBA8UnormSrgb => 4,
            _ => throw new NotImplementedException($"byte size for format {Enum.GetName(format)}")
        };
        return (ulong)(pixels * bytesPerPixel);
    }

    SerialDisposable SurfaceOnFrameSubscription = new();

    public async ValueTask<IWebViewSharedBuffer> CreateSurfaceSharedBufferAsync(HeadlessSurface surface, CancellationToken cancellation)
    {
        WebView2SharedBuffer? buffer = null;
        if (SharedBuffers.TryGetValue(surface.Id, out var existedBuffer))
        {
            buffer = existedBuffer;
        }

        await DispatchAsync(() =>
        {
            if (buffer is null)
            {
                var bufferSize = BufferSize(
                    surface.Entity.Option.Width, surface.Entity.Option.Height, surface.Entity.Option.Format
                ) * (ulong)Option.SlotCount;
                var rawbuffer = WebView.CoreWebView2.Environment.CreateSharedBuffer(bufferSize);
                buffer = new(surface.Id, surface.Entity.Option, rawbuffer);
                SharedBuffers.TryAdd(surface.Id, buffer);
                SurfaceOnFrameSubscription.Disposable = SubscribeSurfaceOnFrame(surface);
            }

            WebView.CoreWebView2.PostSharedBufferToScript(buffer.Buffer, CoreWebView2SharedBufferAccess.ReadOnly,
                JsonSerializer.Serialize(new { surfaceId = surface.Id }));

        }, cancellation);

        return buffer ?? throw new NullReferenceException("Failed to create shared buffer");
    }

    private IDisposable SubscribeSurfaceOnFrame(HeadlessSurface surface)
    {
        var buffer = SharedBuffers[surface.Id];
        return surface.OnFrame.Subscribe(
          async (frame, cancellation) =>
                      {
                          // TODO: use semaphoreslim to protect data
                          await DispatchAsync(() =>
                          {
                              frame.Data.Span.CopyTo(buffer.SlotSpan(frame.SlotIndex));
                          }, cancellation);
                          await SendMessageAsync(new BufferToPresentEvent(
                                              surface.Id,
                                              (int)(frame.SlotIndex * frame.Data.Length),
                                              frame.Data.Length,
                                              0,
                                              surface.Width,
                                              surface.Height
                                          ), cancellation);
                      });
    }


    private async ValueTask<IMediaStream> CaptureImplAsync(HeadlessSurface surface, int frameRate, CancellationToken cancellation)
    {
        //var sharedBuffer = SharedBuffers[surface.Id];


        //Logger.LogInformation($"Stream id {resultStream.Id}");
        //return resultStream;
        throw new NotImplementedException();
    }

    public async ValueTask<IMediaStream> CaptureAsync(HeadlessSurface surface, int frameRate)
    {
        //var result = new TaskCompletionSource<IMediaStream>();
        //await DispatchAsync(async () =>
        //{
        //    await CaptureImplAsync(surface, frameRate, CancellationToken.None);
        //    result.SetResult(null);
        //}, CancellationToken.None);
        //return await result.Task;
        throw new NotImplementedException();
    }

    private async ValueTask SendMessageAsync(string data, CancellationToken cancellation)
    {
        await DispatchAsync(() =>
        {
            if (WebView?.CoreWebView2 is null)
            {
                Logger.LogWarning($"WebView is not initialized yet");
                return;
            }
            WebView.CoreWebView2.PostWebMessageAsString(data);
        }, cancellation);
    }

    public async ValueTask SendMessageAsync<T>(T data, CancellationToken cancellation)
        where T : notnull
    {
        await SendMessageAsync(JsonSerializer.Serialize(new TaggedEvent<T>(data), new JsonSerializerOptions()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        }), cancellation);
    }
}


sealed record class WebViewPeerConnectionProxy(Guid PeerId) : IPeerConnection
{
    public Guid SelfId => ClientsManager.ServerId;
    public R3.Observable<IDataChannel> OnDataChannel => throw new NotImplementedException();

    public R3.Observable<IMediaStreamTrack> OnTrack => throw new NotImplementedException();

    public R3.Observable<RTCPeerConnectionState> OnConnectionStateChange => throw new NotImplementedException();

    public ValueTask AddTrack(IMediaStreamTrack track)
    {
        throw new NotImplementedException();
    }

    public ValueTask<IDataChannel> CreateDataChannel(string label)
    {
        throw new NotImplementedException();
    }

    public void Dispose()
    {
        throw new NotImplementedException();
    }

    public ValueTask StartAsync(CancellationToken cancellation)
    {
        throw new NotImplementedException();
    }
}
