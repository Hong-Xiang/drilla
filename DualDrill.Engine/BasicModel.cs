using DualDrill.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DualDrill.Engine;

public interface IVertexAttribute
{
    int ShaderLocation { get; }
    GPUVertexFormat Format { get; }
    int Offset { get; }
}

public interface IVertexBufferLayout
{
    int ArrayStride { get; }
    GPUVertexStepMode StepMode { get; }
}

public sealed class BasicModel<TVertex>
{
    public int AttributeCount { get; }
}
