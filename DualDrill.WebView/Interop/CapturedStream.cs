using DualDrill.Engine.Media;

namespace DualDrill.WebView.Interop;

public sealed record class WebViewMediaStreamTrack(string StreamId, int Index) : IMediaStreamTrack
{
    public Task Stop()
    {
        throw new NotImplementedException();
    }
}

public sealed record class CapturedStream(Guid SurfaceId, string Id, int VideoTrackCount = 1) : IMediaStream
{
    WebViewMediaStreamTrack[] Tracks = Enumerable.Range(0, VideoTrackCount).Select(i => new WebViewMediaStreamTrack(Id, i)).ToArray();
    public Task<IMediaStreamTrack> GetVideoTrack(int index) => Task.FromResult<IMediaStreamTrack>(Tracks[index]);
}
