﻿using DualDrill.CLSL.Language.AbstractSyntaxTree.Expression;
using DualDrill.CLSL.Language.AbstractSyntaxTree.Statement;
using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.FunctionBody;
using DualDrill.CLSL.Language.LinearInstruction;
using DualDrill.CLSL.Language.Literal;
using DualDrill.CLSL.Language.Types;
using FluentAssertions;
using System.Collections.Immutable;
using Xunit.Abstractions;

namespace DualDrill.CLSL.Test;


public sealed class ShaderModuleToAbstractSyntaxTreeTests(ITestOutputHelper Output)
{
    [Fact]
    public void SimpleEmptyModuleShouldWork()
    {
        var moduleStack = new ShaderModuleDeclaration<StructuredStackInstructionFunctionBody>([], ImmutableDictionary<FunctionDeclaration, StructuredStackInstructionFunctionBody>.Empty);
        var moduleAst = moduleStack.ToAbstractSyntaxTreeFunctionBody();
    }

    [Fact]
    public void SimpleConstFunctionShouldWork()
    {
        var f = new FunctionDeclaration("foo", [], new FunctionReturn(ShaderType.I32, []), []);
        var body = new StructuredStackInstructionFunctionBody(
            new Block<IStructuredStackInstruction>(
                Label.Create(),
                [BasicBlock<IStructuredStackInstruction>.Create([
                    ShaderInstruction.Const(Literal.Create(42)),
                    ShaderInstruction.Return()
                ])]
            )
        );
        var moduleStack = new ShaderModuleDeclaration<StructuredStackInstructionFunctionBody>([f], new Dictionary<FunctionDeclaration, StructuredStackInstructionFunctionBody>()
        {
            [f] = body
        }.ToImmutableDictionary());
        var ast = moduleStack.ToAbstractSyntaxTreeFunctionBody();
        Output.WriteLine(ast.Dump());
        var astBody = ast.GetBody(f);
        astBody.Statements
            .Should().ContainSingle()
            .Which.Should().BeOfType<CompoundStatement>()
            .Which.Statements.Should().ContainSingle()
            .Which.Should().BeOfType<ReturnStatement>()
            .Which.Expr.Should().BeOfType<LiteralValueExpression>()
            .Which.Literal.Should().BeOfType<I32Literal>()
            .Which.Value.Should().Be(42);
    }
}
