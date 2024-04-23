
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

public interface IAsyncResourceBuilder<T>
{
    public bool SetResult(T value);
    public ValueTask<bool> Done();
}

struct AsyncResource2Builder<T>(Channel<T> OutChannel, Channel<bool> DoneChannel) : IAsyncResourceBuilder<T>
{
    public readonly bool SetResult(T value) => OutChannel.Writer.TryWrite(value);
    public readonly ValueTask<T> Resource() => OutChannel.Reader.ReadAsync();
    public readonly ValueTask<bool> Done() => DoneChannel.Reader.ReadAsync();
}

public sealed class AsyncResource2<T>(T Value, ChannelWriter<bool> Done, ValueTask DisposeDone) : IAsyncDisposable
{
    public delegate ValueTask AsyncScopedFactory(IAsyncResourceBuilder<T> builder);

    public T Value { get; } = Value;

    public static async ValueTask<AsyncResource2<T>> CreateAsync(AsyncScopedFactory asyncFactory)
    {
        var outChannel = Channel.CreateBounded<T>(1);
        var doneChannel = Channel.CreateBounded<bool>(1);
        var builder = new AsyncResource2Builder<T>(outChannel, doneChannel);
        var result = await builder.Resource().ConfigureAwait(false);
        return new AsyncResource2<T>(result, doneChannel, asyncFactory(builder));
    }

    public async ValueTask DisposeAsync()
    {
        await Done.WriteAsync(true).ConfigureAwait(false);
        await DisposeDone.ConfigureAwait(false);
    }
}