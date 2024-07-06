﻿using DualDrill.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DualDrill.Engine;

public record struct FrameContext(
    long FrameIndex, 
    Memory<MouseEvent> MouseEvent, 
    IGPUSurface Surface)
{
}
