using DualDrill.CLSL.Language.AbstractSyntaxTree.Expression;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.LinearInstruction;
using DualDrill.CLSL.Language.ShaderAttribute;

namespace DualDrill.CLSL.Language.Operation.Pointer;

[Obsolete]
public interface IAddressOfSymbolOperation : IOperation
{
}

[Obsolete]
public sealed record class AddressOfSymbolOperation(ILoadStoreTargetSymbol Symbol) : IAddressOfSymbolOperation
{
    public FunctionDeclaration Function => throw new NotImplementedException();

    public string Name => $"& {Symbol}";

    public IInstruction Instruction => throw new NotImplementedException();

    public IOperationMethodAttribute GetOperationMethodAttribute()
        => throw new NotSupportedException();
}
