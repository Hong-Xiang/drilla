using DualDrill.Engine.Connection;
using DualDrill.Engine.WebRTC;
using Microsoft.JSInterop;

namespace DualDrill.Engine.BrowserProxy;

public sealed class JSMediaStreamProxy(
    IClient BrowserClient,
    JSClientModule Module,
    IJSObjectReference MediaStream,
    string Id)
    : IMediaStream, IClientObjectReferenceProxy<IClient, IJSObjectReference>, IAsyncDisposable
{
    public IClient Client { get; } = BrowserClient;
    public IJSObjectReference Reference { get; } = MediaStream;
    public string Id { get; } = Id;

    public async Task<IJSObjectReference> GetVideoTrack(int index)
    {
        await using var videoTracks = await Reference.InvokeAsync<IJSObjectReference>("getVideoTracks").ConfigureAwait(false);
        return await Module.GetProperty<IJSObjectReference>(videoTracks, index).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        await Reference.DisposeAsync();
    }
}
