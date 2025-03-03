using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.Types;

namespace DualDrill.CLSL.Language.LinearInstruction;

public interface IIndirectionInstruction : IInstruction
{
    public IShaderType TargetType { get; }
}

public sealed record class IndirectionInstruction(IShaderType TargetType)
    : IIndirectionInstruction
{
    public TResult Accept<TVisitor, TResult>(TVisitor visitor)
        where TVisitor : IStructuredStackInstructionVisitor<TResult>
        => visitor.VisitIndirection(this);

    public IEnumerable<Label> ReferencedLabels => [];
    public IEnumerable<VariableDeclaration> ReferencedLocalVariables => [];
    public override string ToString() => $"indirection : {TargetType.Name}";
}