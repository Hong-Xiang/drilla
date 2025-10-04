using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.ShaderAttribute;
using DualDrill.Graphics;
using DualDrill.CLSL;
using System.Collections.Immutable;
using System.Numerics;
using DualDrill.CLSL.Reflection;

namespace DualDrill.Engine.Shader;

public class ReflectionTestShaderReflection : IReflection
{
    private IShaderModuleReflection _shaderModuleReflection;
    public ReflectionTestShaderReflection()
    {
        _shaderModuleReflection = new ShaderModuleReflection();
    }

    public ImmutableArray<GPUVertexBufferLayout>? GetVertexBufferLayout()
    {
        var vertexBufferLayoutBuilder = _shaderModuleReflection.GetVertexBufferLayoutBuilder<ReflectionTestShader.Vertex, ReflectionTestShader.UserDefinedMeshModel>();
        vertexBufferLayoutBuilder.AddMapping(g => g.Position, h => h.Position)
                                .AddMapping(g => g.Color, h => h.ColorOffset.Color)
                                .AddMapping(g => g.Offset, h => h.ColorOffset.Offset)
                                .AddMapping(g => g.Scale, h => h.Scale);
        return vertexBufferLayoutBuilder.Build();
    }

    public GPUBindGroupLayoutDescriptor? GetBindGroupLayoutDescriptor(IShaderModuleDeclaration module)
    {
        return _shaderModuleReflection.GetBindGroupLayoutDescriptor(module);
    }

    public GPUBindGroupLayoutDescriptor? GetBindGroupLayoutDescriptor() => null;
}

public struct ReflectionTestShader : ISharpShader
{
    public struct Vertex
    {
        [Location(0)]
        public Vector2 Position;

        [Location(1)]
        public Vector4 Color;

        [Location(2)]
        public Vector2 Offset;

        [Location(3)]
        public Vector2 Scale;
    }

    public struct VSOutput
    {
        [Builtin(BuiltinBinding.position)]
        public Vector4 Position;

        [Location(0)]
        public Vector4 Color;
    }

    public struct UserDefinedHostColorOffsetModel
    {
        public Vector4 Color;
        public Vector2 Offset;
    }

    public struct UserDefinedMeshModel
    {
        // Ideally we should support
        // public IGPUBuffer<Vector2> PositionBuffer;
        [VertexStepMode(GPUVertexStepMode.Vertex)] // attribute could be omitted as default
        // buffer index 0
        public Vector2 Position;

        // public IGPUBuffer<Host> ColorOffsetBuffer;
        [VertexStepMode(GPUVertexStepMode.Instance)]
        // buffer index 1
        public UserDefinedHostColorOffsetModel ColorOffset;

        [VertexStepMode(GPUVertexStepMode.Instance)]
        // buffer index 2
        public Vector2 Scale;
    }

    [Vertex]
    VSOutput vs(Vertex vert)
    {
        VSOutput vsOut = new();
        // seems no buintin conversion from Vector3 to Vector2
        var s = new Vector2(vert.Scale.X, vert.Scale.Y);
        vsOut.Position = new Vector4(vert.Position * s + vert.Offset, 0.0f, 1.0f);
        vsOut.Color = vert.Color;
        return vsOut;
    }

    [Fragment]
    Vector4 fs(VSOutput vsOut)
    {
        return vsOut.Color;
    }

}
