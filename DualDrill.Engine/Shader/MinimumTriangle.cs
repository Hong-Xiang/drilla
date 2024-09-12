using DualDrill.ILSL;
using DualDrill.ILSL.IR;
using DualDrill.ILSL.IR.Declaration;
using DualDrill.ILSL.IR.Expression;
using DualDrill.ILSL.IR.Statement;
using System.Numerics;

namespace DualDrill.Engine.Shader;

public static class MinimumTriangleModule
{
    public static Module CreateModule()
    {
        return new ILSL.IR.Module([VS(), FS()]);
    }

    private static FunctionDeclaration VS()
    {
        var vertex_index = new ParameterDeclaration(Name.Create("vertex_index"), new UIntType<B32>(), [new BuiltinAttribute(BuiltinBinding.vertex_index)]);
        var fRet = new FunctionReturn(
            new VecType<R4, FloatType<B32>>(),
            [new BuiltinAttribute(BuiltinBinding.position)]);
        var x = new VariableDeclaration(DeclarationScope.Function, Name.Create("x"), new FloatType<B32>(), []);
        var y = new VariableDeclaration(DeclarationScope.Function, Name.Create("y"), new FloatType<B32>(), []);
        x.Initializer = SyntaxFactory.f32(
                            SyntaxFactory.Binary(
                                SyntaxFactory.Literal(1),
                                BinaryArithmeticOp.Subtraction,
                                SyntaxFactory.Argument(vertex_index)
                            ));
        y.Initializer = SyntaxFactory.Literal(2.0f);
        return new FunctionDeclaration(Name.Create("vs"),
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
        return new FunctionDeclaration(Name.Create("fs"),
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
        var x = (float)(1 - (int)vertexIndex) * 0.5f;
        var y = (float)((int)(vertexIndex & 1u) * 2 - 1) * 0.5f;
        return new Vector4(x, y, 0.0f, 1.0f);
    }

    [Fragment]
    [return: Location(0)]
    static Vector4 fs()
    {
        return new Vector4(0.5f, 1.0f, 0.5f, 1.0f);
    }

}
