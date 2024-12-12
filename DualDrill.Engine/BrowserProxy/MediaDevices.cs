using DualDrill.Engine.Connection;
using Microsoft.JSInterop;

namespace DualDrill.Engine.BrowserProxy;

public sealed class MediaDevices(IClient Client, IJSRuntime JSRuntime)
{
    public async ValueTask<JSMediaStreamProxy> GetUserMedia(JSClientModule client, bool audio, bool video)
    {
        var mediaStream = await JSRuntime.InvokeAsync<IJSObjectReference>("navigator.mediaDevices.getUserMedia", new
        {
            audio,
            video
        }).ConfigureAwait(false);
        var id = await client.GetProperty<string>(mediaStream, "id").ConfigureAwait(false);
        return new(Client, client, mediaStream, id);
    }
}
