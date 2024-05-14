using DualDrill.Engine;
using Microsoft.JSInterop;
using System.Threading.Channels;

namespace DualDrill.Server.Browser;

public sealed class JSRenderService(IJSObjectReference JSRenderContext) : IAsyncDisposable, IRenderService<RenderState>
{
    readonly Channel<RenderState> RenderChannel = Channel.CreateBounded<RenderState>(new BoundedChannelOptions(3)
    {
        FullMode = BoundedChannelFullMode.DropOldest,
    });
    public IAsyncEnumerable<RenderState> States => RenderChannel.Reader.ReadAllAsync();

    public async ValueTask DisposeAsync()
    {
        await JSRenderContext.DisposeAsync().ConfigureAwait(false);
    }

    //public async ValueTask Render(float time, float state)
    //{
    //    await JSRenderContext.InvokeVoidAsync("render", time, state);
    //}

    public async ValueTask Render(RenderState state)
    {
        await RenderChannel.Writer.WriteAsync(state);
    }
}
