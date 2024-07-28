using DualDrill.Graphics;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DualDrill.Engine;

public interface IVertexBufferLayout
{
    ulong ArrayStride { get; }
    GPUVertexStepMode StepMode { get; }
    ReadOnlySpan<GPUVertexAttribute> Attributes { get; }


}

public sealed class WebGPULogo : IVertexBufferLayout
{
    public ulong ArrayStride => 5UL * sizeof(float);

    public GPUVertexStepMode StepMode => GPUVertexStepMode.Vertex;

    public ReadOnlySpan<GPUVertexAttribute> Attributes => _attributes.AsSpan();

    private readonly ImmutableArray<GPUVertexAttribute> _attributes = [
                new GPUVertexAttribute
                            {
                                ShaderLocation = 0,
                                Format = GPUVertexFormat.Float32x2,
                                Offset = 0
                            },
                            new GPUVertexAttribute
                            {
                                ShaderLocation = 1,
                                Format = GPUVertexFormat.Float32x3,
                                Offset = 2 * sizeof(float),
               },

    ];
    private readonly ImmutableArray<float> _VertexData = [
        0.5f,   0.0f,    0.0f, 0.353f, 0.612f,
1.0f,   0.866f,  0.0f, 0.353f, 0.612f,
0.0f,   0.866f,  0.0f, 0.353f, 0.612f,

0.75f,  0.433f,  0.0f, 0.4f,   0.7f,
1.25f,  0.433f,  0.0f, 0.4f,   0.7f,
1.0f,   0.866f,  0.0f, 0.4f,   0.7f,

    1.0f,   0.0f,    0.0f, 0.463f, 0.8f,
    1.25f,  0.433f,  0.0f, 0.463f, 0.8f,
    0.75f,  0.433f,  0.0f, 0.463f, 0.8f,

    1.25f,  0.433f,  0.0f, 0.525f, 0.91f,
    1.375f, 0.65f,   0.0f, 0.525f, 0.91f,
    1.125f, 0.65f,   0.0f, 0.525f, 0.91f,

    1.125f, 0.65f,   0.0f, 0.576f, 1.0f,
    1.375f, 0.65f,   0.0f, 0.576f, 1.0f,
    1.25f,  0.866f,  0.0f, 0.576f, 1.0f, ];

    private readonly ImmutableArray<UInt16> _IndexData = [
        0,  1,  2,
        3,  4,  5,
        6,  7,  8,
        9, 10, 11,
        12, 13, 14,
        0 // padding
        ];

    public ReadOnlySpan<float> VertexData => _VertexData.AsSpan();
    public ReadOnlySpan<UInt16> IndexData => _IndexData.AsSpan();

    public int IndexCount { get; } = 15;

    public ulong VertexBufferByteLength => (ulong)VertexData.Length * sizeof(float);
    public ulong IndexBufferByteLength => (ulong)IndexData.Length * sizeof(UInt16);
}

public sealed class Quad : IVertexBufferLayout
{
    public ulong ArrayStride => 5UL * sizeof(float);

    public GPUVertexStepMode StepMode => GPUVertexStepMode.Vertex;

    public ReadOnlySpan<GPUVertexAttribute> Attributes => _attributes.AsSpan();

    private readonly ImmutableArray<GPUVertexAttribute> _attributes = [
                new GPUVertexAttribute
                            {
                                ShaderLocation = 0,
                                Format = GPUVertexFormat.Float32x2,
                                Offset = 0
                            },
                            new GPUVertexAttribute
                            {
                                ShaderLocation = 1,
                                Format = GPUVertexFormat.Float32x3,
                                Offset = 2 * sizeof(float),
               },

    ];
    private readonly ImmutableArray<float> _VertexData = [
        -0.5f, -0.5f, 1.0f, 0.0f, 0.0f,
        +0.5f, -0.5f, 0.0f, 1.0f, 0.0f,
        +0.5f, +0.5f, 0.0f, 0.0f, 1.0f,
        -0.5f, +0.5f, 1.0f, 1.0f, 0.0f
    ];
    private readonly ImmutableArray<UInt16> _IndexData = [0, 1, 2, 0, 2, 3];

    public ReadOnlySpan<float> VertexData => _VertexData.AsSpan();
    public ReadOnlySpan<UInt16> IndexData => _IndexData.AsSpan();

    public ulong VertexBufferByteLength => (ulong)VertexData.Length * sizeof(float);
    public ulong IndexBufferByteLength => (ulong)IndexData.Length * sizeof(UInt16);
}

public sealed class BasicModel<TVertex>
{
    public int AttributeCount { get; }
}
