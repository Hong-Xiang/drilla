using DualDrill.Engine.Media;
using R3;

namespace DualDrill.Engine.Connection;

public interface IPeerConnection : IDisposable
{
    Guid SelfId { get; }
    Guid PeerId { get; }
    ValueTask<IDataChannel> CreateDataChannel(string label);
    Observable<IDataChannel> OnDataChannel { get; }
    ValueTask AddTrack(IMediaStreamTrack track);
    Observable<IMediaStreamTrack> OnTrack { get; }
    ValueTask StartAsync(CancellationToken cancellation);
}
