namespace DualDrill.Engine.Connection;

public interface IClientProxy<out TClient>
    where TClient : IClient
{
    TClient Client { get; }
}

public interface IClientObjectReferenceProxy<out TClient, TReference>
    : IClientProxy<TClient>
    where TClient : IClient
{
    TReference Reference { get; }
}
