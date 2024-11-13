using DualDrill.CLSL.Language.IR.Declaration;
using DualDrill.CLSL.Language.IR.Statement;
using DualDrill.CLSL.Language.IR;
using DualDrill.CLSL.Language.Types;
using System.Numerics;
using System.Reflection;
using DualDrill.Common.Nat;

namespace DualDrill.CLSL.Tests;

public class CSharpToILSLIRParserTest
{
    static MethodInfo GetMethodInfo<T>(Func<T> f) => f.Method;
    static MethodInfo GetMethodInfo<TA, TB>(Func<TA, TB> f) => f.Method;

    private void AssertFunctionParsedCorrectly(
        IShaderType? expectedReturnType,
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
        AssertFunctionParsedCorrectly(new IntType(N32.Instance),
            [
                new ReturnStatement(new LiteralValueExpression(new IntLiteral(42)))
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
        AssertFunctionParsedCorrectly(new VecType<R4, FloatType>(),
            [
                new ReturnStatement(
                    new FunctionCallExpression(
                        VecType<R4, FloatType>.Constructors[4],
                        [
                            new LiteralValueExpression(new FloatLiteral(0.5f)),
                            new LiteralValueExpression(new FloatLiteral(0.0f)),
                            new LiteralValueExpression(new FloatLiteral(1.0f)),
                            new LiteralValueExpression(new FloatLiteral(1.0f)),
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
                        VecType<R4, FloatType>.Constructors[4],
                        [
                            new LiteralValueExpression(new FloatLiteral(0.5f)),
                            new LiteralValueExpression(new FloatLiteral(0.0f)),
                            new LiteralValueExpression(new FloatLiteral(1.0f)),
                            new LiteralValueExpression(new FloatLiteral(1.0f)),
                        ]);
        var v1 = new VariableDeclaration(DeclarationScope.Function,
                                         "v1",
                                         new VecType<R4, FloatType>(),
                                         []);
        var v2 = new VariableDeclaration(DeclarationScope.Function,
                                         "v2",
                                         new VecType<R4, FloatType>(),
                                         []);
        AssertFunctionParsedCorrectly(new FloatType(),
            [
                new VariableOrValueStatement( v1 ),
                new VariableOrValueStatement( v2 ),
                new ReturnStatement(
                    new FunctionCallExpression(
                        VecType<R4, FloatType>.Dot,
                        [
                            new VariableIdentifierExpression(v1),
                            new VariableIdentifierExpression(v2),
                        ])
                )
            ], parsed);
    }

}
