using CommunityToolkit.HighPerformance;
using DualDrill.Graphics;
using System.Collections.Immutable;

namespace DualDrill.Engine.Mesh;

public sealed class Cube : IMesh
{
    public string Name => nameof(Cube);
    static readonly ImmutableArray<float> VertexDataArray = [
   // float4 position, float4 color, float2 uv,
   1, -1, 1, 1,   1, 0, 1, 1,  0, 1,
  -1, -1, 1, 1,  0, 0, 1, 1,  1, 1,
  -1, -1, -1, 1, 0, 0, 0, 1,  1, 0,
  1, -1, -1, 1,  1, 0, 0, 1,  0, 0,
  1, -1, 1, 1,   1, 0, 1, 1,  0, 1,
  -1, -1, -1, 1, 0, 0, 0, 1,  1, 0,

  1, 1, 1, 1,    1, 1, 1, 1,  0, 1,
  1, -1, 1, 1,   1, 0, 1, 1,  1, 1,
  1, -1, -1, 1,  1, 0, 0, 1,  1, 0,
  1, 1, -1, 1,   1, 1, 0, 1,  0, 0,
  1, 1, 1, 1,    1, 1, 1, 1,  0, 1,
  1, -1, -1, 1,  1, 0, 0, 1,  1, 0,

  -1, 1, 1, 1,   0, 1, 1, 1,  0, 1,
  1, 1, 1, 1,    1, 1, 1, 1,  1, 1,
  1, 1, -1, 1,   1, 1, 0, 1,  1, 0,
  -1, 1, -1, 1,  0, 1, 0, 1,  0, 0,
  -1, 1, 1, 1,   0, 1, 1, 1,  0, 1,
  1, 1, -1, 1,   1, 1, 0, 1,  1, 0,

  -1, -1, 1, 1,  0, 0, 1, 1,  0, 1,
  -1, 1, 1, 1,   0, 1, 1, 1,  1, 1,
  -1, 1, -1, 1,  0, 1, 0, 1,  1, 0,
  -1, -1, -1, 1, 0, 0, 0, 1,  0, 0,
  -1, -1, 1, 1,  0, 0, 1, 1,  0, 1,
  -1, 1, -1, 1,  0, 1, 0, 1,  1, 0,

  1, 1, 1, 1,    1, 1, 1, 1,  0, 1,
  -1, 1, 1, 1,   0, 1, 1, 1,  1, 1,
  -1, -1, 1, 1,  0, 0, 1, 1,  1, 0,
  -1, -1, 1, 1,  0, 0, 1, 1,  1, 0,
  1, -1, 1, 1,   1, 0, 1, 1,  0, 0,
  1, 1, 1, 1,    1, 1, 1, 1,  0, 1,

  1, -1, -1, 1,  1, 0, 0, 1,  0, 1,
  -1, -1, -1, 1, 0, 0, 0, 1,  1, 1,
  -1, 1, -1, 1,  0, 1, 0, 1,  1, 0,
  1, 1, -1, 1,   1, 1, 0, 1,  0, 0,
  1, -1, -1, 1,  1, 0, 0, 1,  0, 1,
  -1, 1, -1, 1,  0, 1, 0, 1,  1, 0 ];

    static readonly ImmutableArray<ushort> IndexDataArray = Enumerable.Range(0, 6 * 6).Select(i => (ushort)i).ToImmutableArray();

    static readonly GPUVertexBufferLayout _BufferLayout = new GPUVertexBufferLayout
    {
        ArrayStride = 4 * 10,
        Attributes = (GPUVertexAttribute[])[
                new GPUVertexAttribute {
                                ShaderLocation = 0,
                                Offset = 0,
                                Format = GPUVertexFormat.Float32x4
                            },
                            new GPUVertexAttribute {
                                ShaderLocation = 1,
                                Offset = 4 * 4,
                                Format = GPUVertexFormat.Float32x4
                            },
                            new GPUVertexAttribute {
                                ShaderLocation = 2,
                                Offset = 4 * 8,
                                Format = GPUVertexFormat.Float32x2
                            }
                 ]
    };

    public ulong ArrayStride => 10 * sizeof(float);

    public GPUVertexStepMode StepMode => GPUVertexStepMode.Vertex;

    public ReadOnlySpan<GPUVertexAttribute> Attributes => BufferLayout.Attributes.Span;

    public uint IndexCount => (uint)IndexDataArray.Length;

    public ReadOnlySpan<byte> VertexData => VertexDataArray.AsSpan().AsBytes();

    public ReadOnlySpan<byte> IndexData => IndexDataArray.AsSpan().AsBytes();

    public GPUVertexBufferLayout BufferLayout => _BufferLayout;

    public GPUIndexFormat IndexFormat => GPUIndexFormat.Uint16;
}
