using DualDrill.CLSL.Language.ControlFlowGraph;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.Literal;
using DualDrill.CLSL.Language.Types;
using DualDrill.CLSL.LinearInstruction;
using DualDrill.CLSL.Compiler;
using System.Collections.Immutable;
using FluentAssertions;
using DualDrill.CLSL.Language.AbstractSyntaxTree.Statement;
using DualDrill.CLSL.Language.AbstractSyntaxTree.Expression;
using DualDrill.CLSL.Language.FunctionBody;

namespace DualDrill.CLSL.Test;


public sealed class ShaderModuleToAbstractSyntaxTreeTests
{
    [Fact]
    void SimpleEmptyModuleShouldWork()
    {
        var moduleStack = new ShaderModuleDeclaration<StructuredStackInstructionFunctionBody>([], ImmutableDictionary<FunctionDeclaration, StructuredStackInstructionFunctionBody>.Empty);
        var moduleAst = moduleStack.ToAbstractSyntaxTreeFunctionBody();
    }

    [Fact]
    void SimpleConstFunctionShouldWork()
    {
        var f = new FunctionDeclaration("foo", [], new FunctionReturn(ShaderType.I32, []), []);
        var body = new StructuredStackInstructionFunctionBody(
            new Block<IStructuredStackInstruction>(
                Label.Create(),
                [BasicBlock<IStructuredStackInstruction>.Create([ShaderInstruction.Const(Literal.Create(42))])]
            )
        );
        var moduleStack = new ShaderModuleDeclaration<StructuredStackInstructionFunctionBody>([f], new Dictionary<FunctionDeclaration, StructuredStackInstructionFunctionBody>()
        {
            [f] = body
        }.ToImmutableDictionary());
        var moduleAst = moduleStack.ToAbstractSyntaxTreeFunctionBody();
        var ast = moduleAst.GetBody(f);
        ast.Statements
            .Should().ContainSingle()
            .Which.Should().BeOfType<ReturnStatement>()
            .Which.Expr.Should().BeOfType<LiteralValueExpression>()
            .Which.Literal.Should().BeOfType<I32Literal>()
            .Which.Value.Should().Be(42);
    }
}
