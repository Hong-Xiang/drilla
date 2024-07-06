﻿using DualDrill.Graphics.Headless;
using Microsoft.Extensions.Options;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using System.Text.Json.Serialization;
using System.Threading.Channels;
using System.Windows;

namespace DualDrill.Server.WevView2;

public readonly record struct SharedBufferMessage(int SlotIndex, int Offset, int Length)
{
}

public readonly record struct SharedBufferMemory(nint Ptr, int SlotIndex, int Offset, int Length)
{
    public unsafe Span<byte> Span => new Span<byte>((void*)(Ptr + Offset), Length);

    public SharedBufferMessage Message => new SharedBufferMessage(SlotIndex, Offset, Length);
}

public sealed class WebViewService
{
    private System.Windows.Application? App;
    private WebView2? WebView;
    private readonly TaskCompletionSource<System.Windows.Application> AppCreatedCompletionSource = new();
    private readonly TaskCompletionSource<int> UIThreadResult = new();

    private readonly Thread UIThread;
    private CoreWebView2SharedBuffer SharedBuffer;

    Channel<SharedBufferMemory> WriteBufferChannel = Channel.CreateUnbounded<SharedBufferMemory>();
    Channel<SharedBufferMemory> ReadBufferChannel = Channel.CreateUnbounded<SharedBufferMemory>();

    public IAsyncEnumerable<SharedBufferMemory> GetAllReadableSlotsAsync(CancellationToken cancellation)
        => ReadBufferChannel.Reader.ReadAllAsync(cancellation);
    public IAsyncEnumerable<SharedBufferMemory> GetAllWriteableSlotsAsync(CancellationToken cancellation)
        => WriteBufferChannel.Reader.ReadAllAsync(cancellation);

    private readonly HeadlessSurface.Option Option;
    int Width => Option.Width;
    int Height => Option.Height;
    ulong TextureBufferSize => (ulong)(4 * Width * Height);


    public WebViewService(IOptions<HeadlessSurface.Option> canvasOption)
    {
        Option = canvasOption.Value;
        UIThread = new Thread(MainUI);
        UIThread.SetApartmentState(ApartmentState.STA);
    }


    public ValueTask<System.Windows.Application> GetApplicationAsync()
    {
        if (AppCreatedCompletionSource.Task.IsCompleted && App is not null)
        {
            return ValueTask.FromResult(App);
        }
        else
        {
            return new ValueTask<System.Windows.Application>(AppCreatedCompletionSource.Task);
        }
    }

    public async ValueTask SetReadyToWrite(SharedBufferMessage sharedBufferMemory)
    {
        await DispatchAsync(() =>
          {

              WriteBufferChannel.Writer.TryWrite(new SharedBufferMemory(SharedBuffer.Buffer, sharedBufferMemory.SlotIndex, sharedBufferMemory.Offset, sharedBufferMemory.Length));
          }, default);
    }

    public void SetReadyToRead(SharedBufferMemory sharedBufferMemory)
    {
        ReadBufferChannel.Writer.TryWrite(sharedBufferMemory);
    }

    public Task<int> GetApplicationResultAsync()
    {
        return UIThreadResult.Task;
    }

    public void Start(Uri webViewSourceUri)
    {
        UIThread.Start(webViewSourceUri);
    }

    TaskCompletionSource WebViewInitializedTaskCompletionSource = new();

    public Task WebViewInitialized => WebViewInitializedTaskCompletionSource.Task;

    void MainUI(object? data)
    {
        App = new System.Windows.Application();
        WebView = new WebView2()
        {
            Name = "DrillWebView2",
            Source = (Uri)data
        };
        var mainWindow = new Window
        {
            Title = "WPF Window in Console Application",
            Width = 1920,
            Height = 1080,
            Content = WebView
        };
        AppCreatedCompletionSource.SetResult(App);
        WebView.CoreWebView2InitializationCompleted += (sender, e) =>
        {
            WebViewInitializedTaskCompletionSource.SetResult();
        };

        var result = App.Run(mainWindow);
        UIThreadResult.SetResult(result);
    }

    public ValueTask CreateSharedBufferAsync(CancellationToken cancellation)
    {
        return DispatchAsync(() =>
        {
            SharedBuffer = WebView.CoreWebView2.Environment.CreateSharedBuffer(TextureBufferSize * (ulong)Option.SlotCount);
            for (var i = 0; i < Option.SlotCount; i++)
            {
                WriteBufferChannel.Writer.TryWrite(new SharedBufferMemory
                {
                    Ptr = SharedBuffer.Buffer,
                    Length = (int)TextureBufferSize,
                    Offset = i * (int)TextureBufferSize,
                    SlotIndex = i,
                });
            }
        }, cancellation);
    }

    ValueTask DispatchAsync(Action action, CancellationToken cancellation)
    {
        return new ValueTask(App.Dispatcher.InvokeAsync(action, System.Windows.Threading.DispatcherPriority.Normal, cancellation).Task);
    }

    public ValueTask PostSharedBufferAsync(CancellationToken cancellation)
    {
        return DispatchAsync(() =>
        {
            WebView.CoreWebView2.PostSharedBufferToScript(SharedBuffer, CoreWebView2SharedBufferAccess.ReadOnly, null);
        }, cancellation);
    }
}
