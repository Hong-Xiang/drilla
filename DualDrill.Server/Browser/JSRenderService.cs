using Microsoft.JSInterop;

namespace DualDrill.Server.Browser;

public sealed class JSRenderService(IJSObjectReference JSRenderContext) : IAsyncDisposable
{
    public async ValueTask Render(double t, double scale)
    {
        await JSRenderContext.InvokeVoidAsync("render", t, scale);
    }

    public async ValueTask DisposeAsync()
    {
        await JSRenderContext.DisposeAsync().ConfigureAwait(false);
    }
}
