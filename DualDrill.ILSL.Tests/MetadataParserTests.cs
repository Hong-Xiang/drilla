﻿using DualDrill.ILSL.Frontend;
using DualDrill.ILSL.IR.Declaration;
using System.Numerics;

namespace DualDrill.ILSL.Tests;

public class MetadataParserTests
{
    sealed class SimpleStructDeclarationShader : IShaderModule
    {
        public struct VertexOutput
        {
            [Builtin(BuiltinBinding.position)]
            public Vector4 ClipPosition { get; set; }

            [Location(0)]
            public Vector2 InteropPosition { get; set; }
        }

        [Vertex]
        static VertexOutput vs([Builtin(BuiltinBinding.position)] uint in_vertex_index)
        {
            VertexOutput result = default;
            var x = (float)(1 - (int)in_vertex_index) * 0.5f;
            var y = (float)((int)(in_vertex_index & 1u) * 2 - 1) * 0.5f;
            result.ClipPosition = new Vector4(x, y, 0.0f, 1.0f);
            result.InteropPosition = new Vector2(x, y);
            return result;
        }

        [Fragment]
        [return: Location(0)]
        static Vector4 fs_main(VertexOutput vout)
        {
            return new Vector4(vout.InteropPosition.X, vout.InteropPosition.Y, 0.1f, 1.0f);
        }
    }

    [Fact]
    void SimpleStructDeclarationParseTest()
    {
        var parser = new MetadataParser();
        var module = parser.ParseModule(new SimpleStructDeclarationShader());

        Assert.Equal(3, module.Declarations.Length);

        var structDecl = module.Declarations.OfType<StructureDeclaration>().Single();
        Assert.Equal(nameof(SimpleStructDeclarationShader.VertexOutput), structDecl.Name);
        Assert.Equal(2, structDecl.Members.Length);
        Assert.Equal("ClipPosition", structDecl.Members[0].Name);
        Assert.Equal(new VecType<R4, FloatType<B32>>(), structDecl.Members[0].Type);
        Assert.Equal("InteropPosition", structDecl.Members[1].Name);
        Assert.Equal(new VecType<R2, FloatType<B32>>(), structDecl.Members[1].Type);
    }

    // https://webgpufundamentals.org/webgpu/lessons/webgpu-uniforms.html
    sealed class SimpleUniformShader : IShaderModule
    {
        public struct OurStruct
        {
            public Vector4 color;
            public Vector2 scale;
            public Vector2 offset;
        }

        [Group(0)]
        [Binding(0)]
        [Uniform]
        OurStruct ourStruct { get; }

        [Vertex]
        [return: Builtin(BuiltinBinding.position)]
        Vector4 vs(
            [Builtin(BuiltinBinding.vertex_index)] uint vertexIndex
        )
        {
            Vector2 pos = new();
            if (vertexIndex == 0)
            {
                pos = new Vector2(0.0f, 0.5f);
            }
            if (vertexIndex == 1)
            {
                pos = new Vector2(-0.5f, -0.5f);
            }
            if (vertexIndex == 2)
            {
                pos = new Vector2(0.5f, -0.5f);
            }
            return new Vector4(
                pos * ourStruct.scale + ourStruct.offset, 0.0f, 1.0f
            );
        }

        [Fragment]
        [return: Location(0)]
        Vector4 fs()
        {
            return ourStruct.color;
        }
    }

    [Fact]
    void SimpleUniformDeclarationParseTest()
    {
        var parser = new MetadataParser();
        var module = parser.ParseModule(new SimpleUniformShader());

        Assert.Equal(4, module.Declarations.Length);

        var uniformDecl = module.Declarations.OfType<VariableDeclaration>().Single();
        Assert.Equal("ourStruct", uniformDecl.Name);
        Assert.Equal(0, uniformDecl.Attributes.OfType<GroupAttribute>().Single().Binding);
        Assert.Equal(0, uniformDecl.Attributes.OfType<BindingAttribute>().Single().Binding);
        Assert.Single(uniformDecl.Attributes.OfType<UniformAttribute>());
        Assert.IsType<StructureDeclaration>(uniformDecl.Type);
    }
}
