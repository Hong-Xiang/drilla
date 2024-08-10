using DualDrill.Engine.Connection;
using MessagePipe;
using Microsoft.AspNetCore.SignalR;
using System.Runtime.InteropServices;

namespace DualDrill.Server;

public sealed record class ClientMessage<T>(
    Guid Source,
    T Payload
)
{
}

public interface IDrillHubClient
{
    Task<string> HubInvoke(string funcHandle);
    Task Offer(Guid source, string sdp);
    Task Answer(Guid source, string sdp);
    Task AddIceCandidate(Guid source, string? candidate);
    Task Emit(string data);
}

sealed class DrillHub(
    ILogger<DrillHub> Logger,
    IAsyncPublisher<PairIdentity, OfferPayload> OfferPublisher,
    IAsyncPublisher<PairIdentity, AnswerPayload> AnswerPublisher,
    IAsyncPublisher<PairIdentity, AddIceCandidatePayload> AddIceCandidatePublisher,
    ClientStore ClientStore) : Hub<IDrillHubClient>
{
    static readonly string ClientIdKey = "ClientId";

    public override async Task OnConnectedAsync()
    {
        await base.OnConnectedAsync();
    }

    private Guid? ClientId
    {
        get
        {
            return Context.Items[ClientIdKey] switch
            {
                Guid value => value,
                _ => null
            };
        }
        set
        {
            Context.Items[ClientIdKey] = value;
        }
    }
    public async Task SetClientId(Guid id)
    {
        ClientId = id;
        ClientStore.UpdateConnectionId(id, Context.ConnectionId);
    }
    public async Task Offer(Guid target, string sdp)
    {
        Logger.LogInformation("Empty {id}", Guid.Empty);
        Logger.LogInformation("offer called with {source} -> {target}", ClientId, target);
        var clientId = ClientId ?? throw new InvalidOperationException("ClientId not set");
        await OfferPublisher.PublishAsync(new(clientId, target), new(sdp));
    }
    public async Task Answer(Guid target, string sdp)
    {
        Logger.LogInformation("Answer called with {source} -> {target}", ClientId, target);
        var clientId = ClientId ?? throw new InvalidOperationException("ClientId not set");
        await AnswerPublisher.PublishAsync(new(clientId, target), new(sdp));
    }

    public async Task AddIceCandidate(Guid target, string data)
    {
        Logger.LogInformation("AddIceCandidate called with {source} -> {target}", ClientId, target);
        var clientId = ClientId ?? throw new InvalidOperationException("ClientId not set");
        await AddIceCandidatePublisher.PublishAsync(new(clientId, target), new(data));
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await base.OnDisconnectedAsync(exception);
        if (exception is not null)
        {
            Logger.LogError(exception, "{ConnectionId} Disconnected With Expcetion", Context.ConnectionId);
        }
        else
        {
            Logger.LogInformation("{ConnectionId} Disconnected", Context.ConnectionId);
        }
    }

    public async Task<string> HubInvokeAsync(string funcHandle)
    {
        var action = nint.Parse(funcHandle);
        var handle = GCHandle.FromIntPtr(action);
        if (handle.Target is Func<object, ValueTask> go)
        {
            await go(this);
        }
        return "done-from-server";
    }
}

