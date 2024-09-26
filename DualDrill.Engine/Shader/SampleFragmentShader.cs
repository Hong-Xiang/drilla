using DualDrill.Graphics;
using DualDrill.ILSL;
using DualDrill.ILSL.IR;
using DualDrill.ILSL.IR.Declaration;
using DualDrill.ILSL.IR.Expression;
using DualDrill.ILSL.IR.Statement;
using System.Collections.Immutable;
using System.Numerics;

namespace DualDrill.Engine.Shader;

public static class SampleFragmentShaderModule
{
    public static Module CreateModule()
    {
        return new ILSL.IR.Module([VS(), FS()]);
    }

    private static FunctionDeclaration VS()
    {
        var vertex_index = new ParameterDeclaration("vertex_index", new UIntType<B32>(), [new BuiltinAttribute(BuiltinBinding.vertex_index)]);
        var fRet = new FunctionReturn(
            new VecType<R4, FloatType<B32>>(),
            [new BuiltinAttribute(BuiltinBinding.position)]);
        var x = new VariableDeclaration(DeclarationScope.Function, "x", new FloatType<B32>(), []);
        var y = new VariableDeclaration(DeclarationScope.Function, "y", new FloatType<B32>(), []);
        // let x = f32(1 - i32(vertex_index)) * 0.5;
        x.Initializer = SyntaxFactory.Binary(SyntaxFactory.f32(
                            SyntaxFactory.Binary(
                                SyntaxFactory.Literal(1),
                                BinaryArithmeticOp.Subtraction,
                                SyntaxFactory.i32(SyntaxFactory.Argument(vertex_index))
                            )), BinaryArithmeticOp.Multiplication, SyntaxFactory.Literal(0.5f));
        // let y = f32(i32(vertex_index & 1u) * 2 - 1) * 0.5;
        y.Initializer = SyntaxFactory.Binary(
                            SyntaxFactory.f32(
                                SyntaxFactory.Binary(
                                    SyntaxFactory.Binary(
                                        SyntaxFactory.i32(
                                            SyntaxFactory.Binary(
                                                SyntaxFactory.Argument(vertex_index),
                                                BinaryBitwiseOp.BitwiseAnd,
                                                SyntaxFactory.Literal(1u)
                                            )
                                        ),
                                    BinaryArithmeticOp.Multiplication,
                                    SyntaxFactory.Literal(2)),
                                    BinaryArithmeticOp.Subtraction,
                                    SyntaxFactory.Literal(1)
                                )
                            ),
                            BinaryArithmeticOp.Multiplication,
                            SyntaxFactory.Literal(0.5f)
                        );
        return new FunctionDeclaration("vs",
                            [vertex_index],
                            fRet,
                            [new VertexAttribute()])
        {
            Body = new CompoundStatement([
                SyntaxFactory.Declare(x),
                SyntaxFactory.Declare(y),
                SyntaxFactory.Return(
                    SyntaxFactory.vec4<FloatType<B32>>(
                        SyntaxFactory.Identifier(x),
                        SyntaxFactory.Identifier(y),
                        SyntaxFactory.Literal(0.0f),
                        SyntaxFactory.Literal(1.0f)
                    )
                )
            ])
        };
    }

    private static FunctionDeclaration FS()
    {
        var fRet = new ILSL.IR.Declaration.FunctionReturn(
            new VecType<R4, FloatType<B32>>(),
            [new LocationAttribute(0)]
        );
        return new FunctionDeclaration("fs",
                            [],
                            fRet,
                            [new FragmentAttribute()])
        {
            Body = new CompoundStatement([
                SyntaxFactory.Return(
                    SyntaxFactory.vec4<FloatType<B32>>(
                        SyntaxFactory.Literal(0.5f),
                        SyntaxFactory.Literal(1.0f),
                        SyntaxFactory.Literal(0.5f),
                        SyntaxFactory.Literal(1.0f)
                    ))
                ])
        };
    }
}


public class SampleFragmentShaderReflection : IReflection
{
    private IShaderModuleReflection _shaderModuleReflection;
    public SampleFragmentShaderReflection()
    {
        _shaderModuleReflection = new ShaderModuleReflection();
    }

    public ImmutableArray<GPUVertexBufferLayout>? GetVertexBufferLayout()
    {
        var vertexBufferLayoutBuilder = _shaderModuleReflection.GetVertexBufferLayoutBuilder<SampleFragmentShader.VertexInput>();
        return vertexBufferLayoutBuilder.Build();
    }

    public GPUBindGroupLayoutDescriptor? GetBindGroupLayoutDescriptor(ILSL.IR.Module module)
    {
        return _shaderModuleReflection.GetBindGroupLayoutDescriptor(module);
    }

    public GPUBindGroupLayoutDescriptorBuffer? GetBindGroupLayoutDescriptorBuffer(ILSL.IR.Module module)
    {
        return _shaderModuleReflection.GetBindGroupLayoutDescriptorBuffer(module);
    }
}


public struct SampleFragmentShader : IShaderModule
{
    public struct VertexInput
    {
        [Location(0)] public Vector2 position;
    }

    [Uniform]
    [Group(0)]
    [Binding(0)]
    float iTime;

    [Vertex]
    [return: Builtin(BuiltinBinding.position)]
    Vector4 vs(VertexInput vertex)
    {
        return new Vector4(vertex.position.X, vertex.position.Y, 0.0f, 1.0f);
    }

    [Fragment]
    [return: Location(0)]
    Vector4 fs([Builtin(BuiltinBinding.position)] Vector4 fragCoord)
    {
        // Courtesy https://www.shadertoy.com/view/lsX3W4
        //float iTime = 0.0f;
        Vector2 iResolution = new Vector2(800.0f, 600.0f);
        Vector2 p = new Vector2(
          (2.0f * fragCoord.X - iResolution.X) / iResolution.Y,
          (2.0f * fragCoord.Y - iResolution.Y) / iResolution.Y
        );
        // animation
        float tz = 0.5f - 0.5f * ((float)Math.Cos(0.225f * iTime));
        float zoo = (float)Math.Pow(0.5f, 13.0f * tz);
        Vector2 c = new Vector2(-0.05f, 0.6805f) + p * zoo;
        // distance to Mandelbrot
        float di = 1.0f;
        Vector2 z = new Vector2(0.0f, 0.0f);
        float m2 = 0.0f;
        Vector2 dz = new Vector2(0.0f, 0.0f);
        for (int i = 0; i < 300; i = i + 1)
        {
            if (m2 > 1024.0f)
            {
                di = 0.0f;
                break;
            }
            // Z' -> 2·Z·Z' + 1
            dz = 2.0f * new Vector2(z.X * dz.X - z.Y * dz.Y, z.X * dz.Y + z.Y * dz.X) + new Vector2(1.0f, 0.0f);
            // Z -> Z² + c
            z = new Vector2(z.X * z.X - z.Y * z.Y, 2.0f * z.X * z.Y) + c;
            //m2 = dot(z, z);
            m2 = Vector2.Dot(z, z);
        }
        // distance
        // d(c) = |Z|·log|Z|/|Z'|
        float d = 0.5f * (float)(Math.Sqrt(Vector2.Dot(z, z) / Vector2.Dot(dz, dz)) * Math.Log(Vector2.Dot(z, z)));
        if (di > 0.5f)
        {
            d = 0.0f;
        }
        // do some soft coloring based on distance
        float d_clamped = (float)Math.Clamp(Math.Pow(4.0f * d / zoo, 0.2f), 0.0f, 1.0f);
        Vector3 col = new Vector3(d_clamped);
        return new Vector4(col, 1.0f);
    }
}
