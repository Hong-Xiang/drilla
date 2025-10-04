using CommunityToolkit.HighPerformance;
using DualDrill.Graphics;
using System.Collections.Immutable;

namespace DualDrill.Engine.Mesh;

public sealed class Quad : IMesh
{
    static readonly GPUVertexBufferLayout _BufferLayout = new GPUVertexBufferLayout
    {
        ArrayStride = 5 * sizeof(float),
        StepMode = GPUVertexStepMode.Vertex,
        Attributes = (GPUVertexAttribute[])[
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
    ]
    };

    static readonly ImmutableArray<float> _VertexData = [
            -0.5f, -0.5f, 1.0f, 0.0f, 0.0f,
        +0.5f, -0.5f, 0.0f, 1.0f, 0.0f,
        +0.5f, +0.5f, 0.0f, 0.0f, 1.0f,
        -0.5f, +0.5f, 1.0f, 1.0f, 0.0f
        ];

    static readonly ImmutableArray<ushort> _IndexData = [0, 1, 2, 0, 2, 3];

    public string Name => nameof(Quad);
    public uint IndexCount => (uint)_IndexData.Length;
    public GPUIndexFormat IndexFormat => GPUIndexFormat.Uint16;

    public ReadOnlySpan<byte> VertexData => _VertexData.AsSpan().AsBytes();
    public ReadOnlySpan<byte> IndexData => _IndexData.AsSpan().AsBytes();
    public GPUVertexBufferLayout BufferLayout => _BufferLayout;

}

