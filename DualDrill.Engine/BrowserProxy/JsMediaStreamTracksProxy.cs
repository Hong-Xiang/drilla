using DualDrill.Engine.Connection;
using DualDrill.Engine.Media;
using Microsoft.JSInterop;

namespace DualDrill.Engine.BrowserProxy
{
    public class JsMediaStreamTracksProxy(
    IClient BrowserClient,
    JSClientModule Module,
    IJSObjectReference MediaStreamTracks)
    : IMediaStreamTrack, IClientObjectReferenceProxy<IClient, IJSObjectReference>, IAsyncDisposable
    {
        public IClient Client { get; } = BrowserClient;
        public IJSObjectReference Reference { get; } = MediaStreamTracks;

        public async Task Stop()
        {
            await Reference.InvokeVoidAsync("stop").ConfigureAwait(false);
        }

        public async ValueTask DisposeAsync()
        {
            await Reference.DisposeAsync();
        }
    }

}
