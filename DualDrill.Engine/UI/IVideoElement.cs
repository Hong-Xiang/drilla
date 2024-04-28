using DualDrill.Engine.WebRTC;

namespace DualDrill.Engine.UI;

public interface IVideoElement
{
    ValueTask SetStream(IMediaStream? stream);
}
