using DualDrill.CLSL.Language.Instruction;
using DualDrill.CLSL.Language.Operation;
using DualDrill.CLSL.Language.Symbol;
using DualDrill.CLSL.Language.Types;

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
}
