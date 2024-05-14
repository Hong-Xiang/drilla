using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DualDrill.Engine;

public interface IRenderService<T>
{
    ValueTask Render(T state);
    IAsyncEnumerable<T> States { get; }
}
