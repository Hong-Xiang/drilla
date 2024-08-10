using DualDrill.Engine.Connection;
using DualDrill.Engine.Media;
using DualDrill.Engine.UI;
using Microsoft.JSInterop;

namespace DualDrill.Engine.BrowserProxy
{
    public class JsVideoElementProxy(
    IClient BrowserClient,  
    JSClientModule Module,
    IJSObjectReference VideoElement)
    : IVideoElement, IClientObjectReferenceProxy<IClient, IJSObjectReference>, IAsyncDisposable
    {
        public IJSObjectReference Reference => VideoElement;

        public IClient Client => BrowserClient;

        public ValueTask SetStream(IMediaStream? stream) => Module.SetVideoElementStreamAsync(Reference, ((JSMediaStreamProxy)stream).Reference);
        public ValueTask RemoveStream() => Module.RemoveVideoElementStreamAsync(Reference);
        public async Task<IMediaStream> GetStream()
        {
            var stream = await Module.GetProperty<IJSObjectReference>(Reference, "srcObject").ConfigureAwait(false);
            var id = await Module.GetProperty<string>(Reference, "id").ConfigureAwait(false);
            return new JSMediaStreamProxy(Client, Module, stream, id);
        }
        public async ValueTask DisposeAsync()
        {
            await Reference.DisposeAsync();
        }
    }
}
