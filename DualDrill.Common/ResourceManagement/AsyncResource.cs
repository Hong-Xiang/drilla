
namespace DualDrill.Common.ResourceManagement;

public sealed class AsyncResource
{
    public static async Task<T> CreateAsync<T>(IAsyncEnumerable<Func<IAsyncDisposable, T>> factory)
    {
        var em = factory.GetAsyncEnumerator();
        var resource = new AsyncResource<T>(em);
        await em.MoveNextAsync();
        var result = em.Current(resource);
        return result;
    }
}

public sealed class AsyncResource<T>(IAsyncEnumerator<Func<IAsyncDisposable, T>> Step) : IAsyncDisposable
{
    public async ValueTask DisposeAsync()
    {
        await Step.MoveNextAsync().ConfigureAwait(false);
    }
}
