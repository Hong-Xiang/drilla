using DualDrill.Engine.Connection;
using DualDrill.Engine.WebRTC;
using Microsoft.JSInterop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
