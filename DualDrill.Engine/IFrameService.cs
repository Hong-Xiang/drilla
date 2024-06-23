using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DualDrill.Engine;

public interface IFrameService
{
    ValueTask OnFrameAsync(FrameContext frameContext, CancellationToken cancellation);
}

