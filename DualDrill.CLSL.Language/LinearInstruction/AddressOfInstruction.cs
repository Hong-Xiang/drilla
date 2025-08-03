using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.Symbol;
using DualDrill.CLSL.Language.Types;
using DualDrill.CLSL.Language.ValueInstruction;

namespace DualDrill.CLSL.Language.LinearInstruction;

public interface IAddressOfInstruction : IInstruction
{
    public IShaderType TargetType { get; }
}

public sealed record class AddressOfInstruction(IShaderType TargetType)
    : IAddressOfInstruction
{
    public TResult Accept<TVisitor, TResult>(TVisitor visitor)
        where TVisitor : IStructuredStackInstructionVisitor<TResult>
        => visitor.VisitAddressOf(this);

    public IEnumerable<IValueInstruction> CreateValueInstruction(Stack<IValue> stack)
    {
        throw new NotImplementedException();
    }

    public IEnumerable<Label> ReferencedLabels => [];
    public IEnumerable<VariableDeclaration> ReferencedLocalVariables => [];
    public override string ToString() => $"address_of : {TargetType.Name})";
}