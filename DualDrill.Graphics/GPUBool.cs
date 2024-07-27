using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DualDrill.Graphics;

public readonly record struct GPUBool(uint Value)
{
    public static implicit operator GPUBool(bool value) => value ? new(1) : new(0);
}
