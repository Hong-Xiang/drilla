using DualDrill.Engine.BrowserProxy;
using DualDrill.Engine.WebRTC;

namespace DualDrill.Engine.UI;

public interface IVideoElement
{
    ValueTask SetStream(IMediaStream? stream);
    ValueTask RemoveStream();
    Task<IMediaStream> GetStream();
}
