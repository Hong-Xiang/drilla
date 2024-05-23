using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DualDrill.Graphics;

internal unsafe record struct UnsafePointer<T>(T* Value)
    where T : unmanaged
{
}
