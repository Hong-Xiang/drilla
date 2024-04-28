using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DualDrill.Engine.WebRTC;

public interface IRTCDataChannel
{
    ValueTask Send(Span<byte> data);
    IAsyncEnumerable<Memory<byte>> Message { get; }
}
