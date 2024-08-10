namespace DualDrill.Engine.Media;

public interface IMediaStream
{
    string Id { get; }
    Task<IMediaStreamTrack> GetVideoTrack(int index);
}
