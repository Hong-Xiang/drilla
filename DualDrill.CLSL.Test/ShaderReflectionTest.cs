using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.FunctionBody;
using DualDrill.CLSL.Language.ShaderAttribute;
using DualDrill.CLSL.Language.Types;
using DualDrill.Graphics;
using System.Collections.Immutable;
using System.Numerics;
using DualDrill.CLSL.Reflection;

namespace DualDrill.CLSL.Test;

public class ShaderReflectionTest
{
    [Fact]
    public void UniformBindgroupTest()
    {
        // Assume we have a shder module with following code line
        //
        // @group(0) @binding(0) var<uniform> data: vec2f;
        // 
        //  [GroupAttribute(0)]
        //  [BindingAttribute(0)]
        //  [StageAttribute(GPUShaderStage.Vertex)]
        //  [UniformAttribute()]
        //  Vector2 data;

        var module = ShaderModuleDeclaration<FunctionBody<UnstructuredStackInstructionSequence>>.Empty with
        {
            Declarations =
            [
                new VariableDeclaration(
                    DeclarationScope.Module,
                    "data",
                    ShaderType.Vec2F32,
                    [
                        new GroupAttribute(0),
                        new BindingAttribute(0),
                        new VertexAttribute(),
                        new UniformAttribute()
                    ]
                )
            ]
        };

        IShaderModuleReflection reflection = new ShaderModuleReflection();
        var layout = reflection.GetBindGroupLayoutDescriptor(module);
        var expected = new GPUBindGroupLayoutDescriptor()
        {
            Entries = new GPUBindGroupLayoutEntry[]
            {
                new GPUBindGroupLayoutEntry()
                {
                    Binding = 0,
                    Visibility = GPUShaderStage.Vertex,
                    Buffer = new GPUBufferBindingLayout()
                    {
                        Type = GPUBufferBindingType.Uniform,
                        HasDynamicOffset = false,
                        MinBindingSize = 8
                    }
                }
            }
        };

        Assert.True(expected.Entries.Span.SequenceEqual(layout.Entries.Span));
    }
}

/// <summary>
/// test cases from https://webgpufundamentals.org/webgpu/lessons/webgpu-vertex-buffers.html
/// </summary>
public class ShaderReflectionBasicVertexLayoutTest
{
    struct Vertex
    {
        [Location(0)] public Vector2 Position;

        [Location(1)] public Vector4 Color;

        [Location(2)] public Vector2 Offset;

        [Location(3)] public Vector2 Scale;
    }

    struct VSOutput
    {
        [Builtin(BuiltinBinding.position)] public Vector4 Position;

        [Location(0)] public Vector4 Color;
    }

    struct ShaderModule
    {
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

    struct UserDefinedHostColorOffsetModel
    {
        public Vector4 Color;
        public Vector2 Offset;
    }

    struct UserDefinedMeshModel
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

    [Fact]
    public void VertexBufferLayoutSpecTest()
    {
        IShaderModuleReflection reflection = new ShaderModuleReflection();
        var vertexMappingBuilder = reflection.GetVertexBufferLayoutBuilder<Vertex, UserDefinedMeshModel>();
        vertexMappingBuilder.AddMapping(g => g.Position, h => h.Position)
                            .AddMapping(g => g.Color, h => h.ColorOffset.Color)
                            .AddMapping(g => g.Offset, h => h.ColorOffset.Offset)
                            .AddMapping(g => g.Scale, h => h.Scale);
        // TODO:  shaders can add default values, how to support auto type conversion?

        ImmutableArray<GPUVertexBufferLayout> expectedLayouts =
        [
            new()
            {
                ArrayStride = 2 * 4,
                StepMode = GPUVertexStepMode.Vertex,
                Attributes = new GPUVertexAttribute[]
                {
                    new GPUVertexAttribute()
                    {
                        ShaderLocation = 0,
                        Offset = 0,
                        Format = GPUVertexFormat.Float32x2,
                    }
                }
            },
            new()
            {
                ArrayStride = 6 * 4,
                StepMode = GPUVertexStepMode.Instance,
                Attributes = new GPUVertexAttribute[]
                {
                    new GPUVertexAttribute()
                    {
                        ShaderLocation = 1,
                        Offset = 0,
                        Format = GPUVertexFormat.Float32x4,
                    },
                    new GPUVertexAttribute()
                    {
                        ShaderLocation = 2,
                        Offset = 4 * 4,
                        Format = GPUVertexFormat.Float32x2,
                    },
                }
            },
            new()
            {
                ArrayStride = 2 * 4,
                StepMode = GPUVertexStepMode.Instance,
                Attributes = new GPUVertexAttribute[]
                {
                    new GPUVertexAttribute()
                    {
                        ShaderLocation = 3,
                        Offset = 0,
                        Format = GPUVertexFormat.Float32x2,
                    }
                }
            }
        ];

        // TODO: Equals implementation based on value/sequence equal
        var vertexBufferLayouts = vertexMappingBuilder.Build();
        Assert.True(expectedLayouts[0].ArrayStride == vertexBufferLayouts[0].ArrayStride);
        Assert.True(expectedLayouts[0].StepMode == vertexBufferLayouts[0].StepMode);
        Assert.True(expectedLayouts[0].Attributes.Span.SequenceEqual(vertexBufferLayouts[0].Attributes.Span));

        Assert.True(expectedLayouts[1].ArrayStride == vertexBufferLayouts[1].ArrayStride);
        Assert.True(expectedLayouts[1].StepMode == vertexBufferLayouts[1].StepMode);
        Assert.True(expectedLayouts[1].Attributes.Span.SequenceEqual(vertexBufferLayouts[1].Attributes.Span));

        Assert.True(expectedLayouts[2].ArrayStride == vertexBufferLayouts[2].ArrayStride);
        Assert.True(expectedLayouts[2].StepMode == vertexBufferLayouts[2].StepMode);
        Assert.True(expectedLayouts[2].Attributes.Span.SequenceEqual(vertexBufferLayouts[2].Attributes.Span));
    }
}

public class ShaderReflectionBasicDefaultVertexLayoutTest
{
    struct Vertex
    {
        [Location(0)] public Vector2 Position;

        [Location(1)] public Vector4 Color;

        [Location(2)] public Vector2 Offset;

        [Location(3)] public Vector2 Scale;
    }

    struct VSOutput
    {
        [Builtin(BuiltinBinding.position)] public Vector4 Position;

        [Location(0)] public Vector4 Color;
    }

    struct ShaderModule
    {
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

    [Fact]
    public void VertexBufferLayoutSpecTest()
    {
        IShaderModuleReflection reflection = new ShaderModuleReflection();
        var vertexMappingBuilder = reflection.GetVertexBufferLayoutBuilder<Vertex>();

        ImmutableArray<GPUVertexBufferLayout> expectedLayouts =
        [
            new()
            {
                ArrayStride = 4 * 10,
                Attributes = new GPUVertexAttribute[]
                {
                    new GPUVertexAttribute()
                    {
                        ShaderLocation = 0,
                        Offset = 0,
                        Format = GPUVertexFormat.Float32x2,
                    },
                    new GPUVertexAttribute()
                    {
                        ShaderLocation = 1,
                        Offset = 4 * 2,
                        Format = GPUVertexFormat.Float32x4,
                    },
                    new GPUVertexAttribute()
                    {
                        ShaderLocation = 2,
                        Offset = 4 * 6,
                        Format = GPUVertexFormat.Float32x2,
                    },
                    new GPUVertexAttribute()
                    {
                        ShaderLocation = 3,
                        Offset = 4 * 8,
                        Format = GPUVertexFormat.Float32x2,
                    },
                }
            }
        ];
        var vertexBufferLayouts = vertexMappingBuilder.Build();

        Assert.True(expectedLayouts[0].ArrayStride == vertexBufferLayouts[0].ArrayStride);
        Assert.True(expectedLayouts[0].Attributes.Span.SequenceEqual(vertexBufferLayouts[0].Attributes.Span));
    }
}