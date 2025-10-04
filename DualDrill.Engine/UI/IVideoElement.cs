using DualDrill.Engine.Media;

namespace DualDrill.Engine.UI;

public interface IVideoElement
{
    ValueTask SetStream(IMediaStream? stream);
    ValueTask RemoveStream();
    Task<IMediaStream> GetStream();
}
