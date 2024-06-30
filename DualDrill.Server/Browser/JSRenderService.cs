using DualDrill.Engine;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using System.Threading.Channels;

namespace DualDrill.Server.Browser;

public sealed class JSRenderService(IJSObjectReference JSRenderContext) : IAsyncDisposable
{
    public async ValueTask DisposeAsync()
    {
        await JSRenderContext.InvokeVoidAsync("dispose").ConfigureAwait(false);
        await JSRenderContext.DisposeAsync().ConfigureAwait(false);
    }

    public IJSObjectReference JSRenderContext { get; } = JSRenderContext;

    public async ValueTask AttachToElementAsync(ElementReference element)
    {
        await JSRenderContext.InvokeVoidAsync("attachToElement", element);
    }
}
