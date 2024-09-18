using DualDrill.Graphics;
using DualDrill.ILSL.IR.Declaration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace DualDrill.ILSL.Tests;

public class ShaderReflectionTest
{
    [Fact]
    public void UniformBindgroupTest()
    {
        // Assume we have a shder module with following code line
        //
        // @group(0) @binding(0) var<uniform> data: vec2f;
        // 

        var module = new IR.Module([
            new IR.Declaration.VariableDeclaration(
                IR.Declaration.DeclarationScope.Module,
                "data",
                new VecType<R2, FloatType<B32>>(),
                [
                    new GroupAttribute(0),
                    new BindingAttribute(0),
                    new UniformAttribute()
                ]
            )
        ]);

        IShaderModuleReflection reflection = null;
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
    }




}

/// <summary>
/// test cases from https://webgpufundamentals.org/webgpu/lessons/webgpu-vertex-buffers.html
/// </summary>
public class ShaderReflectionBasicVertexLayoutTest
{
    struct Vertex
    {
        [Location(0)]
        public Vector2 Position;

        [Location(1)]
        public Vector4 Color;

        [Location(2)]
        public Vector2 Offset;

        [Location(3)]
        public Vector3 Scale;
    }

    struct VSOutput
    {
        [Builtin(BuiltinBinding.position)]
        public Vector4 Position;

        [Location(0)]
        public Vector4 Color;
    }

    struct ShaderModule : IShaderModule
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



        IShaderModuleReflection reflection = null;
        var vertexMappingBuilder = reflection.GetVertexBufferLayoutBuilder<Vertex, UserDefinedMeshModel>();
        vertexMappingBuilder.AddMapping(g => g.Position, h => h.Position)
                            .AddMapping(g => g.Color, h => h.ColorOffset.Color)
                            .AddMapping(g => g.Offset, h => h.ColorOffset.Offset);
        // TODO:  shaders can add default values, how to support auto type conversion?
        //.AddMapping(g => g.Scale, h => h.Scale); 

        GPUVertexBufferLayout[] expectedLayouts = [
            new() {
                ArrayStride = 2 * 4,
                Attributes = new GPUVertexAttribute[] {
                    new GPUVertexAttribute(){
                        ShaderLocation = 0,
                        Offset = 0,
                        Format = GPUVertexFormat.Float32x2,
                    }
                }
            },
            new(){
                ArrayStride = 6 * 4,
                StepMode = GPUVertexStepMode.Instance,
                Attributes = new GPUVertexAttribute[] {
                    new GPUVertexAttribute(){
                        ShaderLocation = 1,
                        Offset = 0,
                        Format = GPUVertexFormat.Float32x4,
                    },
                    new GPUVertexAttribute(){
                        ShaderLocation = 2,
                        Offset = 4 * 4,
                        Format = GPUVertexFormat.Float32x2,
                    },
                }
            },
            new(){
                ArrayStride = 2 * 4,
                StepMode = GPUVertexStepMode.Instance,
                Attributes = new GPUVertexAttribute[] {
                    new GPUVertexAttribute(){
                        ShaderLocation = 3,
                        Offset = 0,
                        Format = GPUVertexFormat.Float32x2,
                    }
                }
            }
        ];
        // TODO: Equals implementation based on value/sequence equal
        Assert.Equal(expectedLayouts, vertexMappingBuilder.Build());
    }
}
