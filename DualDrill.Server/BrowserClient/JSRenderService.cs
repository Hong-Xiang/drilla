using Microsoft.JSInterop;

namespace DualDrill.Server.BrowserClient;

public sealed class JSRenderService(IJSObjectReference JSRenderContext) : IAsyncDisposable
{
    public async ValueTask Render(double t)
    {
        await JSRenderContext.InvokeVoidAsync("render", t);
    }

    public async ValueTask DisposeAsync()
    {
        await JSRenderContext.DisposeAsync().ConfigureAwait(false);
    }
}
