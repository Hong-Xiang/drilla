using DualDrill.CLSL.Language.AbstractSyntaxTree.Expression;
using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.Literal;
using DualDrill.CLSL.Language.Operation;
using DualDrill.CLSL.Language.Types;
using DualDrill.Common;
using DualDrill.Common.Nat;

namespace DualDrill.CLSL.Language.LinearInstruction;

public interface IInstruction
{
}

public interface IStackInstruction : IInstruction
{
}

public interface IStructuredStackInstruction : IStackInstruction,
    IStructuredControlFlowElement<IStructuredStackInstruction>
{
    TResult Accept<TVisitor, TResult>(TVisitor visitor)
        where TVisitor : IStructuredStackInstructionVisitor<TResult>;

    IEnumerable<IStructuredStackInstruction> IStructuredControlFlowElement<IStructuredStackInstruction>.
        Instructions => [this];
}

public sealed record class LabelInstruction(Label Label) : IStackInstruction, ILabeledEntity
{
    public IEnumerable<VariableDeclaration> ReferencedLocalVariables => [];
    public IEnumerable<Label> ReferencedLabels => [Label];
}

public interface IJumpInstruction : IStructuredStackInstruction
{
}

public sealed record class BrInstruction(Label Target) : IJumpInstruction
{
    public TResult Accept<TVisitor, TResult>(TVisitor visitor)
        where TVisitor : IStructuredStackInstructionVisitor<TResult>
        => visitor.Visit(this);

    public override string ToString() => $"br {Target}";
    public IEnumerable<Label> ReferencedLabels => [Target];
    public IEnumerable<VariableDeclaration> ReferencedLocalVariables => [];
}

public sealed record class BrIfInstruction(Label Target) : IJumpInstruction
{
    public TResult Accept<TVisitor, TResult>(TVisitor visitor)
        where TVisitor : IStructuredStackInstructionVisitor<TResult>
        => visitor.Visit(this);

    public override string ToString() => $"br_if {Target}";
    public IEnumerable<Label> ReferencedLabels => [Target];
    public IEnumerable<VariableDeclaration> ReferencedLocalVariables => [];
}

public sealed record class ReturnInstruction() : IJumpInstruction
{
    public TResult Accept<TVisitor, TResult>(TVisitor visitor)
        where TVisitor : IStructuredStackInstructionVisitor<TResult>
        => visitor.Visit(this);

    public override string ToString() => "return";
    public IEnumerable<Label> ReferencedLabels => [];
    public IEnumerable<VariableDeclaration> ReferencedLocalVariables => [];
}

public sealed record class NopInstruction
    : IStructuredStackInstruction
        , ISingleton<NopInstruction>
{
    public static NopInstruction Instance { get; } = new();

    public TResult Accept<TVisitor, TResult>(TVisitor visitor)
        where TVisitor : IStructuredStackInstructionVisitor<TResult>
        => visitor.Visit(this);

    public override string ToString() => "nop";
    public IEnumerable<Label> ReferencedLabels => [];
    public IEnumerable<VariableDeclaration> ReferencedLocalVariables => [];
}

public sealed record class ConstInstruction<TLiteral>(TLiteral Literal) : IStructuredStackInstruction
    where TLiteral : ILiteral
{
    public IEnumerable<VariableDeclaration> ReferencedLocalVariables => [];

    public IEnumerable<Label> ReferencedLabels => [];

    public TResult Accept<TVisitor, TResult>(TVisitor visitor)
        where TVisitor : IStructuredStackInstructionVisitor<TResult>
        => visitor.Visit(this);

    public override string ToString() => $"const.{Literal.Type.Name} {Literal}";
}

public sealed record class CallInstruction(FunctionDeclaration Callee) : IStructuredStackInstruction
{
    public IEnumerable<VariableDeclaration> ReferencedLocalVariables => [];

    public IEnumerable<Label> ReferencedLabels => [];

    public TResult Accept<TVisitor, TResult>(TVisitor visitor)
        where TVisitor : IStructuredStackInstructionVisitor<TResult>
        => visitor.Visit(this);

    public override string ToString() => $"call {Callee.Name}({Callee})";
}

public sealed record class LoadSymbolValueInstruction<TTarget>(TTarget Target) : IStructuredStackInstruction
    where TTarget : ILoadStoreTargetSymbol
{
    public IEnumerable<VariableDeclaration> ReferencedLocalVariables =>
        Target is VariableDeclaration v ? [v] : [];

    public IEnumerable<Label> ReferencedLabels => [];

    public TResult Accept<TVisitor, TResult>(TVisitor visitor)
        where TVisitor : IStructuredStackInstructionVisitor<TResult>
        => visitor.Visit(this);

    public override string ToString()
    {
        return $"load {Target}";
    }
}

public sealed record class LoadSymbolAddressInstruction<TTarget>(TTarget Target) : IStructuredStackInstruction
    where TTarget : ILoadStoreTargetSymbol
{
    public IEnumerable<VariableDeclaration> ReferencedLocalVariables =>
        Target is VariableDeclaration v ? [v] : [];

    public IEnumerable<Label> ReferencedLabels => [];

    public TResult Accept<TVisitor, TResult>(TVisitor visitor)
        where TVisitor : IStructuredStackInstructionVisitor<TResult>
        => visitor.Visit(this);

    public override string ToString()
    {
        return $"load.address {Target}";
    }
}

public sealed record class StoreSymbolInstruction<TTarget>(TTarget Target) : IStructuredStackInstruction
    where TTarget : ILoadStoreTargetSymbol
{
    public IEnumerable<VariableDeclaration> ReferencedLocalVariables =>
        Target is VariableDeclaration v ? [v] : [];

    public IEnumerable<Label> ReferencedLabels => [];

    public TResult Accept<TVisitor, TResult>(TVisitor visitor)
        where TVisitor : IStructuredStackInstructionVisitor<TResult>
        => visitor.Visit(this);

    public override string ToString()
    {
        return $"store {Target}";
    }
}

public sealed record class BinaryOperationInstruction<TOperation>
    : ISingleton<BinaryOperationInstruction<TOperation>>
        , IStructuredStackInstruction
    where TOperation : IBinaryOperation<TOperation>
{
    public IEnumerable<VariableDeclaration> ReferencedLocalVariables => [];
    public IEnumerable<Label> ReferencedLabels => [];

    public static BinaryOperationInstruction<TOperation> Instance { get; } = new();
    public TOperation Operation => TOperation.Instance;

    public TResult Accept<TVisitor, TResult>(TVisitor visitor)
        where TVisitor : IStructuredStackInstructionVisitor<TResult>
        => visitor.Visit(this);

    public override string ToString() => TOperation.Instance.Name;
}

public sealed class LogicalNotInstruction
    : IStructuredStackInstruction
        , ISingleton<LogicalNotInstruction>
{
    public IEnumerable<VariableDeclaration> ReferencedLocalVariables => [];
    public IEnumerable<Label> ReferencedLabels => [];

    public static LogicalNotInstruction Instance { get; } = new();

    public TResult Accept<TVisitor, TResult>(TVisitor visitor)
        where TVisitor : IStructuredStackInstructionVisitor<TResult>
        => visitor.Visit(this);
}

public sealed record class DupInstruction : IStructuredStackInstruction, ISingleton<DupInstruction>
{
    public IEnumerable<VariableDeclaration> ReferencedLocalVariables => [];
    public IEnumerable<Label> ReferencedLabels => [];

    public static DupInstruction Instance { get; } = new();

    public TResult Accept<TVisitor, TResult>(TVisitor visitor)
        where TVisitor : IStructuredStackInstructionVisitor<TResult>
        => visitor.Visit(this);

    public override string ToString() => "dup";
}

public sealed record class DropInstruction : IStructuredStackInstruction, ISingleton<DropInstruction>
{
    public IEnumerable<VariableDeclaration> ReferencedLocalVariables => [];
    public IEnumerable<Label> ReferencedLabels => [];

    public static DropInstruction Instance { get; } = new();

    public TResult Accept<TVisitor, TResult>(TVisitor visitor)
        where TVisitor : IStructuredStackInstructionVisitor<TResult>
        => visitor.Visit(this);

    public override string ToString() => "drop";
}

public static class ShaderInstruction
{
    public static IStructuredStackInstruction Nop() => new NopInstruction();
    public static IStructuredStackInstruction Br(Label target) => new BrInstruction(target);
    public static IStructuredStackInstruction BrIf(Label target) => new BrIfInstruction(target);

    public static IStructuredStackInstruction I32Eq() =>
        BinaryOperationInstruction<NumericBinaryOperation<IntType<N32>, BinaryRelation.Eq>>.Instance;

    public static IStructuredStackInstruction LogicalNot() => new LogicalNotInstruction();
    public static IStructuredStackInstruction Dup() => DupInstruction.Instance;
    public static IStructuredStackInstruction Pop() => DropInstruction.Instance;

    public static LoadSymbolValueInstruction<ParameterDeclaration> Load(ParameterDeclaration decl) => new(decl);
    public static LoadSymbolValueInstruction<VariableDeclaration> Load(VariableDeclaration decl) => new(decl);
    public static LoadSymbolValueInstruction<MemberDeclaration> Load(MemberDeclaration decl) => new(decl);

    public static LoadSymbolAddressInstruction<ParameterDeclaration> LoadAddress(ParameterDeclaration decl) =>
        new(decl);

    public static LoadSymbolAddressInstruction<VariableDeclaration> LoadAddress(VariableDeclaration decl) => new(decl);
    public static StoreSymbolInstruction<ParameterDeclaration> Store(ParameterDeclaration decl) => new(decl);
    public static StoreSymbolInstruction<VariableDeclaration> Store(VariableDeclaration decl) => new(decl);

    public static IStructuredStackInstruction Call(FunctionDeclaration decl) => new CallInstruction(decl);

    public static IStructuredStackInstruction Return() => new ReturnInstruction();

    public static IStructuredStackInstruction Const<TLiteral>(TLiteral value)
        where TLiteral : ILiteral
        => new ConstInstruction<TLiteral>(value);
}