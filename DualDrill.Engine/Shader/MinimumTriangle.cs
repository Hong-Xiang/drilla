using DualDrill.CLSL.Language.IR.ShaderAttribute;
using DualDrill.ILSL;
using DualDrill.Mathematics;

namespace DualDrill.Engine.Shader;

//public static class MinimumTriangleModule
//{
//    public static ILSL.IR.Module CreateModule()
//    {
//        return new ILSL.IR.Module([VS(), FS()]);
//    }

//    private static FunctionDeclaration VS()
//    {
//        var vertex_index = new ParameterDeclaration("vertex_index", new UIntType<N32>(), [new BuiltinAttribute(BuiltinBinding.vertex_index)]);
//        var fRet = new FunctionReturn(
//            new VecType<N4, FloatType<N32>>(),
//            [new BuiltinAttribute(BuiltinBinding.position)]);
//        var x = new VariableDeclaration(DeclarationScope.Function, "x", new FloatType<N32>(), []);
//        var y = new VariableDeclaration(DeclarationScope.Function, "y", new FloatType<N32>(), []);
//        // let x = f32(1 - i32(vertex_index)) * 0.5;
//        x.Initializer = SyntaxFactory.Binary(SyntaxFactory.f32(
//                            SyntaxFactory.Binary(
//                                SyntaxFactory.Literal(1),
//                                BinaryArithmeticOp.Subtraction,
//                                SyntaxFactory.i32(SyntaxFactory.Argument(vertex_index))
//                            )), BinaryArithmeticOp.Multiplication, SyntaxFactory.Literal(0.5f));
//        // let y = f32(i32(vertex_index & 1u) * 2 - 1) * 0.5;
//        y.Initializer = SyntaxFactory.Binary(
//                            SyntaxFactory.f32(
//                                SyntaxFactory.Binary(
//                                    SyntaxFactory.Binary(
//                                        SyntaxFactory.i32(
//                                            SyntaxFactory.Binary(
//                                                SyntaxFactory.Argument(vertex_index),
//                                                BinaryBitwiseOp.BitwiseAnd,
//                                                SyntaxFactory.Literal(1u)
//                                            )
//                                        ),
//                                    BinaryArithmeticOp.Multiplication,
//                                    SyntaxFactory.Literal(2)),
//                                    BinaryArithmeticOp.Subtraction,
//                                    SyntaxFactory.Literal(1)
//                                )
//                            ),
//                            BinaryArithmeticOp.Multiplication,
//                            SyntaxFactory.Literal(0.5f)
//                        );
//        return new FunctionDeclaration("vs",
//                            [vertex_index],
//                            fRet,
//                            [new VertexAttribute()])
//        {
//            Body = new CompoundStatement([
//                SyntaxFactory.Declare(x),
//                SyntaxFactory.Declare(y),
//                SyntaxFactory.Return(
//                    SyntaxFactory.vec4<FloatType<N32>>(
//                        SyntaxFactory.Identifier(x),
//                        SyntaxFactory.Identifier(y),
//                        SyntaxFactory.Literal(0.0f),
//                        SyntaxFactory.Literal(1.0f)
//                    )
//                )
//            ])
//        };
//    }

//    private static FunctionDeclaration FS()
//    {
//        var fRet = new ILSL.IR.Declaration.FunctionReturn(
//            new VecType<N4, FloatType<N32>>(),
//            [new LocationAttribute(0)]
//        );
//        return new FunctionDeclaration("fs",
//                            [],
//                            fRet,
//                            [new FragmentAttribute()])
//        {
//            Body = new CompoundStatement([
//                SyntaxFactory.Return(
//                    SyntaxFactory.vec4<FloatType<N32>>(
//                        SyntaxFactory.Literal(0.5f),
//                        SyntaxFactory.Literal(1.0f),
//                        SyntaxFactory.Literal(0.5f),
//                        SyntaxFactory.Literal(1.0f)
//                    ))
//                ])
//        };
//    }
//}




public struct MinimumTriangle : ISharpShader, IILSLDevelopShaderModule
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
    public static vec4f32 vs([Builtin(BuiltinBinding.vertex_index)] uint vertexIndex)
    {
        // explicit type as a temp workaround for IL treating 1u literal using 1 (int)
        var vi = (int)vertexIndex;
        var x = (1 - vi) * 0.5f;
        var y = ((vi & 1) * 2 - 1) * 0.5f;
        return DMath.vec4(x, y, 0.0f, 1.0f);
    }

    [Fragment]
    [return: Location(0)]
    public static vec4f32 fs()
    {
        return DMath.vec4(1.0f);
    }
}