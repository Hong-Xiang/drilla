using DualDrill.Engine.Connection;
using Microsoft.JSInterop;

namespace DualDrill.Server.BrowserClient;

public sealed class JSMediaStreamProxy(IBrowserClient BrowserClient, IJSObjectReference MediaStream, string Id)
    : IAsyncDisposable, IClientVideoReference
{
    public IClient Client { get; } = BrowserClient;
    public IJSObjectReference MediaStream { get; } = MediaStream;
    public string Id { get; } = Id;

    public async Task<IJSObjectReference> GetVideoTrack(int index)
    {
        await using var videoTracks = await MediaStream.InvokeAsync<IJSObjectReference>("getVideoTracks").ConfigureAwait(false);
        return await BrowserClient.Module.GetProperty<IJSObjectReference>(videoTracks, index).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        await MediaStream.DisposeAsync();
    }
}

sealed class MediaDevices(IJSRuntime JSRuntime)
{
    public async Task<JSMediaStreamProxy> GetUserMedia(IBrowserClient client, bool audio, bool video)
    {
        Console.WriteLine($"GetUserMedia {client}");
        var mediaStream = await JSRuntime.InvokeAsync<IJSObjectReference>("navigator.mediaDevices.getUserMedia", new
        {
            audio,
            video
        }).ConfigureAwait(false);
        var id = await client.Module.GetProperty<string>(mediaStream, "id").ConfigureAwait(false);
        Console.WriteLine($"Camera video id {id} of client {client.Id}");
        return new(client, mediaStream, id);
    }
}
