using DualDrill.ILSL;
using DualDrill.ILSL.IR;
using DualDrill.ILSL.IR.Declaration;
using DualDrill.ILSL.IR.Expression;
using DualDrill.ILSL.IR.Statement;
using System.Numerics;
using DualDrill.Engine.Mesh;
namespace DualDrill.Graphics;

public static class MinimumTriangleModule
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




public struct MinimumTriangle : IShaderModule, IILSLDevelopShaderModule
{
    string IILSLDevelopShaderModule.ILSLWGSLExpectedCode => """
      @vertex fn vs(@builtin(vertex_index) vertex_index : u32) 
        -> @builtin(position) vec4f 
      {
        let x = f32(1 - i32(vertex_index)) * 0.5;
        let y = f32(i32(vertex_index & 1u) * 2 - 1) * 0.5;
        return vec4f(x, y, 0.0, 1.0);
      }
 
      @fragment fn fs() -> @location(0) vec4f {
        return vec4f(0.5, 1.0, 0.5, 1.0);
      }
      """;



    [Vertex]
    [return: Builtin(BuiltinBinding.position)]
    static Vector4 vs(
        [Builtin(BuiltinBinding.vertex_index)] uint vertexIndex
    )
    {
        // explicit type as a temp workaround for IL treating 1u literal using 1 (int)
        int c = (int)(vertexIndex - 0u);
        var x = (float)(1 - c) * 0.5f;
        var d = (int)(1 & c);
        var y = (float)(d * 2 - 1) * 0.5f;
        return new Vector4(x, y, 0.0f, 1.0f);
    }

    [Fragment]
    [return: Location(0)]
    static Vector4 fs()
    {
        float x = 0.0f;
        if (x < 0.5f)
        {
            int test1 = 1;
        }
        else if (x < 0.75f)
        {
            int test2 = 2;
        }
        else
        {
            int test3 = 3;
        }
        return new Vector4(0.5f, 0.0f, 1.0f, 1.0f);
    }

}
