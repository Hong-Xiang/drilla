using System.Collections.Immutable;
using DualDrill.CLSL.Backend;
using DualDrill.CLSL.Language;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.FunctionBody;
using DualDrill.CLSL.Language.Instruction;
using DualDrill.CLSL.Language.Operation;
using DualDrill.CLSL.Language.Region;
using DualDrill.CLSL.Language.Symbol;
using DualDrill.CLSL.Language.Transform;
using DualDrill.CLSL.Language.Types;
using DualDrill.Common.Nat;

namespace DualDrill.CLSL.Test;

public sealed class InstructionBoundaryTests
{
    [Fact]
    public void ResultlessInstructionsRetainTheirOperands()
    {
        var ptr = ShaderValue.Intermediate(ShaderType.I32);
        var value = ShaderValue.Intermediate(ShaderType.I32);
        var nop = Instruction<IShaderValue, IShaderValue>.Create(NopOperation.Instance, null, []);
        var store = Instruction<IShaderValue, IShaderValue>.Create(new StoreOperation(), null, [ptr, value]);

        Assert.Null(nop.Result);
        Assert.Empty(nop.Operands);
        Assert.Null(store.Result);
        Assert.Equal(new IShaderValue[] { ptr, value }, store.Operands);
        Assert.Same(ptr, store[0]);
        Assert.Null(store.Select(static operand => operand, static result => result).Result);
    }

    [Fact]
    public void MissingOperandAndResultAreRejected()
    {
        var value = ShaderValue.Intermediate(ShaderType.I32);
        var load = Instruction<IShaderValue, IShaderValue>.Create(new LoadOperation(), null, [value]);
        Assert.Throws<ArgumentException>(() => load.RequireResult());

        var missing = load with { Result = value, Operand0 = null };
        Assert.Throws<ArgumentException>(() => _ = missing[0]);
        Assert.Throws<ArgumentException>(() => missing.Operands.ToArray());
        Assert.Throws<ArgumentException>(() =>
            Instruction<IShaderValue, IShaderValue>.Create(new LoadOperation(), value, [null!]));
    }

    [Fact]
    public void VariadicOperandsCannotBeTruncatedOrSkipped()
    {
        var value = ShaderValue.Intermediate(ShaderType.I32);
        var call = Instruction<IShaderValue, IShaderValue>.Create(
            new CallOperation(new FunctionType([ShaderType.I32, ShaderType.I32], ShaderType.I32)),
            value, [value, value, value]);

        Assert.Equal(3, call.Operands.Count());
        foreach (var malformed in new[]
                 {
                     call with { RestOperands = [] },
                     call with { RestOperands = [value, value] },
                     call with { RestOperands = [null!] },
                     call with { Operand1 = null }
                 })
        {
            Assert.Throws<ArgumentException>(() => malformed.Operands.ToArray());
            Assert.Throws<ArgumentException>(() => _ = malformed[0]);
        }
    }

    [Fact]
    public void DirectTargetRejectsMalformedVectorSetWithoutDroppingStoredValues()
    {
        var operation = VectorComponentSetOperation<N2, VecType<N2, FloatType<N32>>, Swizzle.X>.Instance;
        var ptr = ShaderValue.Intermediate(operation.LeftType);
        var value = ShaderValue.Intermediate(operation.RightType);
        var extra = ShaderValue.Intermediate(operation.RightType);
        var valid = Instruction<IShaderValue, IShaderValue>.Create(operation, null, [ptr, value]);
        foreach (var malformed in new[]
                 {
                         valid with { OperandCount = 3, RestOperands = [] },
                         valid with { RestOperands = [extra] }
                     })
        {
            var module = Module(malformed);
            var body = Assert.Single(module.FunctionDefinitions).Value;
            Assert.Contains(ptr, body.UsedValues());
            Assert.Contains(value, body.UsedValues());
            if (!malformed.RestOperands.IsEmpty)
                Assert.Contains(extra, body.UsedValues());
            var error = Assert.Throws<NotSupportedException>(() => new SlangTargetLowering().Lower(module));
            Assert.Contains("invalid vector component set signature", error.Message);
        }
    }

    [Fact]
    public void NonUnitCallWithoutResultIsRejectedAtEvaluationAndTarget()
    {
        var callee = new FunctionDeclaration(
            "GetValue", [], new FunctionReturn(ShaderType.I32, []), []);
        var call = Instruction<IShaderValue, IShaderValue>.Create(
            new CallOperation((FunctionType)callee.Type), null, [callee]);

        Assert.Throws<ArgumentException>(() => Module(call).RunPass(new FunctionToOperationPass()));
        var error = Assert.Throws<NotSupportedException>(() =>
            new SlangTargetLowering().Lower(Module(call)));
        Assert.Contains("non-Unit call requires a result", error.Message);
    }

    [Fact]
    public void SetterCallRewritePreservesExactReceiverAndValueArity()
    {
        IBinaryStatementOperation[] setters =
        [
            VectorComponentSetOperation<N2, VecType<N2, FloatType<N32>>, Swizzle.X>.Instance,
            VectorSwizzleSetOperation<Swizzle.Pattern<N2, Swizzle.X, Swizzle.Y>, FloatType<N32>>.Instance
        ];
        foreach (var setter in setters)
        {
            var callee = setter.Function;
            var ptr = ShaderValue.Intermediate(setter.LeftType);
            var value = ShaderValue.Intermediate(setter.RightType);
            var extra = ShaderValue.Intermediate(setter.RightType);
            var call = new CallOperation((FunctionType)callee.Type);
            var valid = Instruction<IShaderValue, IShaderValue>.Create(call, null, [callee, ptr, value]);

            var normalized = Module(valid).RunPass(new FunctionToOperationPass());
            var entry = Assert.Single(normalized.FunctionDefinitions).Value;
            var rewritten = Assert.Single(entry[entry.Entry].Body.Elements);
            Assert.Same(setter, rewritten.Operation);
            Assert.Null(rewritten.Result);
            Assert.Equal(2, rewritten.OperandCount);
            Assert.Same(ptr, rewritten[0]);
            Assert.Same(value, rewritten[1]);

            foreach (var malformed in new[]
                     {
                         Instruction<IShaderValue, IShaderValue>.Create(call, null, [callee, ptr, value, extra]),
                         Instruction<IShaderValue, IShaderValue>.Create(call, null, [callee, ptr]),
                         valid with
                         {
                             Operation = new CallOperation(
                                 new FunctionType([setter.LeftType, setter.RightType, setter.RightType], ShaderType.Unit)),
                             OperandCount = 4,
                             RestOperands = [value, extra]
                         }
                     })
            {
                var source = Module(malformed);
                if (malformed.OperandCount == 4)
                    Assert.Contains(extra, Assert.Single(source.FunctionDefinitions).Value.UsedValues());
                var error = Record.Exception(() => source.RunPass(new FunctionToOperationPass()));
                Assert.NotNull(error);
                Assert.Equal("OperationFunctionNotMatchException", error.GetType().Name);
                Assert.Contains("does not match operation", error.Message);
            }
        }
    }

    [Fact]
    public void ResultlessUnitCallLowersAsEffect()
    {
        var callee = new FunctionDeclaration(
            "Observe", [], new FunctionReturn(ShaderType.Unit, []), []);
        var call = Instruction<IShaderValue, IShaderValue>.Create(
            new CallOperation((FunctionType)callee.Type), null, [callee]);
        Assert.Null(call.Result);

        var module = Module(call);
        var calleeLabel = Label.Create("callee");
        var calleeBody = RegionFixture.CreateFunctionBody(
            callee,
            RegionTree.Block(
                calleeLabel, [],
                RegionFixture.Body(
                    calleeLabel, [], [],
                    Terminator.B.ReturnVoid<RegionJump<IShaderValue>, IShaderValue>()),
                null));
        module = module with
        {
            Declarations = [callee, .. module.Declarations],
            FunctionDefinitions = module.FunctionDefinitions.Add(callee, calleeBody)
        };
        module = module.RunPass(new FunctionToOperationPass());
        var target = new SlangTargetLowering().Lower(module);
        var caller = Assert.Single(module.FunctionDefinitions.Keys, function => function.Name == "Entry");
        Assert.Single(target.GetBody(caller).Origins.Instructions, origin => origin.Target is SlangEffect);
    }

    private static ShaderModuleDeclaration<RegionFunctionBody> Module(
        Instruction<IShaderValue, IShaderValue> instruction)
    {
        var function = new FunctionDeclaration(
            "Entry", [], new FunctionReturn(ShaderType.Unit, []), []);
        var label = Label.Create("entry");
        var body = RegionFixture.CreateFunctionBody(
            function,
            RegionTree.Block(
                label, [],
                RegionFixture.Body(
                    label, [], [instruction],
                    Terminator.B.ReturnVoid<RegionJump<IShaderValue>, IShaderValue>()),
                null));
        return new ShaderModuleDeclaration<RegionFunctionBody>(
            [function],
            ImmutableDictionary<FunctionDeclaration, RegionFunctionBody>.Empty.Add(function, body));
    }
}
