using System.Collections.Immutable;
using DualDrill.CLSL.Compiler;
using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.FunctionBody;
using DualDrill.CLSL.Language.LinearInstruction;
using DualDrill.CLSL.Language.Literal;
using DualDrill.CLSL.Language.Operation;
using DualDrill.CLSL.Language.Symbol;
using DualDrill.CLSL.Language.Types;
using DualDrill.CLSL.Language.ValueInstruction;
using DualDrill.Common.Nat;
using FluentAssertions;
using Xunit.Abstractions;

namespace DualDrill.CLSL.Test;

public class StackInstructionToValueInstructionTests(ITestOutputHelper testOutput)
{
    private StackBasicBlockToValueBasicBlockTransform Transform = new();

    [Fact]
    public void Return42BasicBlockShouldWork()
    {
        var sbb = new StackInstructionBasicBlock(
            Label.Create(),
            [
                ShaderInstruction.Const(Literal.Create(42)),
                ShaderInstruction.ReturnResult()
            ],
            [],
            []
        );
        var vbb = Transform.Apply(sbb);
        testOutput.WriteLine(vbb.Dump());
        var values = vbb.OperationValues.ToImmutableArray();
        vbb.Elements.Should().SatisfyRespectively(
            x => x.Should().BeOfType<ConstInstruction<IntType<N32>, I32Literal>>().Which.Result.Should().Be(values[0]),
            x => x.Should().BeOfType<ReturnResultValueInstruction<IntType<N32>>>().Which.Value.Should().Be(values[0])
        );
    }

    [Fact]
    public void BasicAddArithmaticShouldWork()
    {
        var sbb = new StackInstructionBasicBlock(
            Label.Create(),
            [
                ShaderInstruction.Const(Literal.Create(40)),
                ShaderInstruction.Const(Literal.Create(2)),
                NumericBinaryArithmeticOperation<IntType<N32>, BinaryArithmetic.Add>.Instance.GetInstruction(),
                ShaderInstruction.ReturnResult()
            ],
            [],
            []
        );
        var vbb = Transform.Apply(sbb);
        testOutput.WriteLine(vbb.Dump());
        var values = vbb.OperationValues.ToImmutableArray();
        vbb.Elements.Should().SatisfyRespectively(
            x => x.Should().BeOfType<ConstInstruction<IntType<N32>, I32Literal>>().Which.Result.Should().Be(values[0]),
            x => x.Should().BeOfType<ConstInstruction<IntType<N32>, I32Literal>>().Which.Result.Should().Be(values[1]),
            x => x.Should().BeAssignableTo<IExpressionOperation2ValueInstruction>(),
            x => x.Should().BeOfType<ReturnResultValueInstruction<IntType<N32>>>().Which.Value.Should().Be(values[2])
        );
    }
}