
using System.Threading.Channels;

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

public interface IAsyncResourceBuilder<T> : IAsyncDisposable
{
    public bool SetResult(T value);
    public ValueTask<bool> Done();
}

sealed class AsyncResourceBuilder<T>
    : IAsyncResourceBuilder<T>, IAsyncDisposable
{
    readonly Channel<T> OutChannel = Channel.CreateBounded<T>(1);
    readonly Channel<bool> DoneChannel = Channel.CreateBounded<bool>(1);
    public bool SetResult(T value) => OutChannel.Writer.TryWrite(value);
    public ValueTask<T> Resource() => OutChannel.Reader.ReadAsync();
    public ValueTask<bool> Done() => DoneChannel.Reader.ReadAsync();

    Task DisposeFinished { get; }

    public AsyncResourceBuilder(Func<IAsyncResourceBuilder<T>, Task> factory)
    {
        DisposeFinished = factory(this);
    }

    public async ValueTask DisposeAsync()
    {
        DoneChannel.Writer.TryWrite(true);
        if (DisposeFinished is not null)
        {
            await DisposeFinished.ConfigureAwait(false);
        }
    }
}


public sealed class AsyncResource2
{
    public static async ValueTask<T> CreateAsync<T>(Func<IAsyncResourceBuilder<T>, Task> asyncFactory)
    {
        var builder = new AsyncResourceBuilder<T>(asyncFactory);
        return await builder.Resource().ConfigureAwait(false);
    }
}