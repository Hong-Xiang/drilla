using Microsoft.JSInterop;

namespace DualDrill.Server.BrowserClient;

public sealed record class JSUnmanagedResourceReference(IJSObjectReference Value) : IAsyncDisposable
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
        await Value.InvokeVoidAsync(JSDisposeMethodName).ConfigureAwait(false);
        await Value.DisposeAsync().ConfigureAwait(false);
    }
}
