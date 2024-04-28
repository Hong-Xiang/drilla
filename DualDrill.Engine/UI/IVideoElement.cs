using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DualDrill.Engine.WebRTC;

namespace DualDrill.Engine.UI;

public interface IVideoElement
{
    ValueTask SetStream(IMediaStream? stream);
}
