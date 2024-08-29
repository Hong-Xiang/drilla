using CommunityToolkit.HighPerformance;
using DualDrill.Graphics;
using System.Collections.Immutable;

namespace DualDrill.Engine.Mesh;

public sealed class WebGPULogo : IMesh
{
    static readonly GPUVertexBufferLayout _BufferLayout = new GPUVertexBufferLayout
    {
        ArrayStride = 5 * sizeof(float),
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
                            }, ]
    };
    public ulong ArrayStride => _BufferLayout.ArrayStride;

    public GPUVertexStepMode StepMode => GPUVertexStepMode.Vertex;

    public ReadOnlySpan<GPUVertexAttribute> Attributes => _BufferLayout.Attributes.Span;

    //private readonly ImmutableArray<GPUVertexAttribute> _attributes = [
    //            new GPUVertexAttribute
    //                        {
    //                            ShaderLocation = 0,
    //                            Format = GPUVertexFormat.Float32x2,
    //                            Offset = 0
    //                        },
    //                        new GPUVertexAttribute
    //                        {
    //                            ShaderLocation = 1,
    //                            Format = GPUVertexFormat.Float32x3,
    //                            Offset = 2 * sizeof(float),
    //           },

    //];
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

    private readonly ImmutableArray<ushort> _IndexData = [
        0,  1,  2,
        3,  4,  5,
        6,  7,  8,
        9, 10, 11,
        12, 13, 14,
        0 // padding
        ];

    public ReadOnlySpan<byte> VertexData => _VertexData.AsSpan().AsBytes();
    public ReadOnlySpan<byte> IndexData => _IndexData.AsSpan().AsBytes();

    public uint IndexCount { get; } = 15;

    public GPUVertexBufferLayout BufferLayout => _BufferLayout;

    public string Name => nameof(WebGPULogo);

    public GPUIndexFormat IndexFormat => GPUIndexFormat.Uint16;
}

