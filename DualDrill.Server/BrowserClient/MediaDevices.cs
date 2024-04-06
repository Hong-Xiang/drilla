using Microsoft.JSInterop;

namespace DualDrill.Server.BrowserClient;

sealed class JSMediaStream(IJSObjectReference JSStream) : IAsyncDisposable
{
    public async ValueTask DisposeAsync()
    {
        await JSStream.DisposeAsync();
    }
}

sealed class MediaDevices(IJSRuntime JSRuntime)
{
    public async Task<IJSObjectReference> GetUserMedia()
    {
        var rawJsMediaStream = await JSRuntime.InvokeAsync<IJSObjectReference>("navigator.mediaDevices.getUserMedia", new
        {
            audio = false,
            video = true
        }).ConfigureAwait(false);
        return rawJsMediaStream;
    }
}
