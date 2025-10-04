using CommunityToolkit.HighPerformance;
using DualDrill.Graphics;
using System.Collections.Immutable;

namespace DualDrill.Engine.Mesh;

public sealed class ScreenQuad : IMesh
{
    static readonly GPUVertexBufferLayout _BufferLayout = new GPUVertexBufferLayout
    {
        ArrayStride = 2 * sizeof(float),
        StepMode = GPUVertexStepMode.Vertex,
        Attributes = (GPUVertexAttribute[])[
            new GPUVertexAttribute
            {
                ShaderLocation = 0,
                Format = GPUVertexFormat.Float32x2,
                Offset = 0
            }
        ]
    };

    static readonly ImmutableArray<float> _VertexData = 
        [
            -1.0f, 1.0f,
            -1.0f, -1.0f,
            1.0f, -1.0f,
            -1.0f, 1.0f,
            1.0f, -1.0f,
            1.0f, 1.0f
        ];

    static readonly ImmutableArray<ushort> _IndexData = [0, 1, 2, 3, 4, 5];

    public string Name => nameof(Quad);
    public uint IndexCount => (uint)_IndexData.Length;
    public GPUIndexFormat IndexFormat => GPUIndexFormat.Uint16;

    public ReadOnlySpan<byte> VertexData => _VertexData.AsSpan().AsBytes();
    public ReadOnlySpan<byte> IndexData => _IndexData.AsSpan().AsBytes();

    public GPUVertexBufferLayout BufferLayout => _BufferLayout;
}

