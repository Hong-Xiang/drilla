using DualDrill.Engine.Connection;
using Microsoft.JSInterop;

namespace DualDrill.Engine.BrowserProxy;

public sealed record class JSDisposableProxy(IClient Client, IJSObjectReference Reference)
    : IAsyncDisposable, IClientObjectReferenceProxy<IClient, IJSObjectReference>
{
    bool disposed = false;
    static readonly string JSDisposeMethodName = "dispose";

    public async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }
        disposed = true;
        await Reference.InvokeVoidAsync(JSDisposeMethodName).ConfigureAwait(false);
        await Reference.DisposeAsync().ConfigureAwait(false);
    }
}

