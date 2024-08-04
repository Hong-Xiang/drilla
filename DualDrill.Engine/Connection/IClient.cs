﻿using DualDrill.Engine.WebRTC;
using MessagePipe;
using System.Threading.Channels;

namespace DualDrill.Engine.Connection;


public interface IClient
{
    public Guid Id { get; }
    public Uri Uri { get; }
    public ValueTask<string> GetConnectionId();
    public ValueTask<IRTCPeerConnection> CreatePeerConnection();
    public ValueTask SendDataStream<T>(Uri uri, IAsyncEnumerable<T> dataStream);
    public ValueTask<IAsyncEnumerable<T>> SubscribeDataStream<T>(Uri uri);
    public ValueTask HubInvokeAsync(Func<object, ValueTask> func);
    public Channel<object> GetOrAddEventChannel(Uri uri);
    ISubscriber<IClient> OnPeerConnected { get; }
}

public interface IClientAsyncCommand<in TClient, T>
    where TClient : IClient
{
    public ValueTask<T> ExecuteAsyncOn(TClient client);
}

public interface IClientAsyncCommand<in TClient>
    where TClient : IClient
{
    public ValueTask ExecuteAsyncOn(TClient client);
}

public static class ClientExtension
{
    public static ValueTask<T> ExecuteCommandAsync<TClient, T>(this TClient client, IClientAsyncCommand<TClient, T> command)
        where TClient : IClient
    {
        return command.ExecuteAsyncOn(client);
    }
    public static ValueTask ExecuteCommandAsync<TClient>(this TClient client, IClientAsyncCommand<TClient> command)
            where TClient : IClient
    {
        return command.ExecuteAsyncOn(client);
    }
}
