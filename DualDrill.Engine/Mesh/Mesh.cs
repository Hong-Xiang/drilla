using DualDrill.Graphics;

namespace DualDrill.Engine.Mesh;

public interface IMesh
{
    string Name { get; }
    GPUVertexBufferLayout BufferLayout { get; }

    uint IndexCount { get; }
    GPUIndexFormat IndexFormat { get; }

    ReadOnlySpan<byte> VertexData { get; }
    ReadOnlySpan<byte> IndexData { get; }
}
