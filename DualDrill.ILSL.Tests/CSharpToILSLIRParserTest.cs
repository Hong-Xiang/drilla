using DualDrill.ILSL.Frontend;
using DualDrill.ILSL.IR;
using DualDrill.ILSL.IR.Declaration;
using DualDrill.ILSL.IR.Expression;
using DualDrill.ILSL.IR.Statement;
using System.Numerics;
using System.Reflection;

namespace DualDrill.ILSL.Tests;

public class CSharpToILSLIRParserTest
{
    static MethodInfo GetMethodInfo<T>(Func<T> f) => f.Method;
    static MethodInfo GetMethodInfo<TA, TB>(Func<TA, TB> f) => f.Method;

    private void AssertFunctionParsedCorrectly(
        IType? expectedReturnType,
        IEnumerable<IStatement> bodyStatements,
        FunctionDeclaration actual
    )
    {
        Assert.Equal(expectedReturnType, actual.Return.Type);
        Assert.NotNull(actual.Body);
        Assert.Equal((IEnumerable<IStatement>)actual.Body.Statements, bodyStatements);

    }

    [Fact]
    public void BasicReturnTest()
    {
        static int BasicReturn()
        {
            return 42;
        }
        using var parser = new ILSpyFrontend(new ILSpyOption { HotReloadAssemblies = [] });
        var method = GetMethodInfo(BasicReturn);
        var parsed = parser.ParseMethod(method);
        AssertFunctionParsedCorrectly(new IntType<B32>(),
            [
                new ReturnStatement(new LiteralValueExpression(new IntLiteral<B32>(42)))
            ], parsed);
    }

    [Fact]
    public void SimpleVec4ConstructorTest()
    {
        static Vector4 TestMethod(uint vertex_index)
        {
            return new Vector4(0.5f, 0.0f, 1.0f, 1.0f);
        }
        using var parser = new ILSpyFrontend(new ILSpyOption { HotReloadAssemblies = [] });
        var method = GetMethodInfo<uint, Vector4>(TestMethod);
        var parsed = parser.ParseMethod(method);
        AssertFunctionParsedCorrectly(new VecType<R4, FloatType<B32>>(),
            [
                new ReturnStatement(
                    new FunctionCallExpression(
                        VecType<R4, FloatType<B32>>.Constructors[4],
                        [
                            new LiteralValueExpression(new FloatLiteral<B32>(0.5f)),
                            new LiteralValueExpression(new FloatLiteral<B32>(0.0f)),
                            new LiteralValueExpression(new FloatLiteral<B32>(1.0f)),
                            new LiteralValueExpression(new FloatLiteral<B32>(1.0f)),
                        ])
                )
            ], parsed);
    }

    [Fact]
    public void Vec4DotTest()
    {
        static float TestMethod(uint vertex_index)
        {
            var v1 = new Vector4(0.5f, 0.0f, 1.0f, 1.0f);
            var v2 = new Vector4(0.5f, 0.0f, 1.0f, 1.0f);
            return Vector4.Dot(v1, v2);
        }
        using var parser = new ILSpyFrontend(new ILSpyOption { HotReloadAssemblies = [] });
        var method = GetMethodInfo<uint, float>(TestMethod);
        var parsed = parser.ParseMethod(method);
        var vec4CreateExpr = new FunctionCallExpression(
                        VecType<R4, FloatType<B32>>.Constructors[4],
                        [
                            new LiteralValueExpression(new FloatLiteral<B32>(0.5f)),
                            new LiteralValueExpression(new FloatLiteral<B32>(0.0f)),
                            new LiteralValueExpression(new FloatLiteral<B32>(1.0f)),
                            new LiteralValueExpression(new FloatLiteral<B32>(1.0f)),
                        ]);
        var v1 = new VariableDeclaration(DeclarationScope.Function,
                                         "v1",
                                         new VecType<R4, FloatType<B32>>(),
                                         []);
        var v2 = new VariableDeclaration(DeclarationScope.Function,
                                         "v2",
                                         new VecType<R4, FloatType<B32>>(),
                                         []);
        AssertFunctionParsedCorrectly(new FloatType<B32>(),
            [
                new VariableOrValueStatement( v1 ),
                new VariableOrValueStatement( v2 ),
                new ReturnStatement(
                    new FunctionCallExpression(
                        VecType<R4, FloatType<B32>>.Dot,
                        [
                            new VariableIdentifierExpression(v1),
                            new VariableIdentifierExpression(v2),
                        ])
                )
            ], parsed);
    }

}
