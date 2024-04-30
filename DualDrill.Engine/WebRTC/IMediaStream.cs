namespace DualDrill.Engine.WebRTC;

public interface IMediaStream
{
    string Id { get; }
    Task<IMediaStreamTrack> GetVideoTrack(int index);
}
