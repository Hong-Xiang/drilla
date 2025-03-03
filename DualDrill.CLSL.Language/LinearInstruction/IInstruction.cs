using System.CodeDom.Compiler;
using DualDrill.CLSL.Language.AbstractSyntaxTree.Expression;
using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.Literal;
using DualDrill.CLSL.Language.Operation;
using DualDrill.CLSL.Language.Types;
using DualDrill.Common;
using DualDrill.Common.CodeTextWriter;
using DualDrill.Common.Nat;

namespace DualDrill.CLSL.Language.LinearInstruction;

public interface IInstruction : IBasicBlockElement
{
    TResult Accept<TVisitor, TResult>(TVisitor visitor)
        where TVisitor : IStructuredStackInstructionVisitor<TResult>;

    void ITextDumpable<ILocalDeclarationContext>.Dump(ILocalDeclarationContext context, IndentedTextWriter writer)
    {
        switch (this)
        {
            case StoreSymbolInstruction<VariableDeclaration> v:
                writer.WriteLine($"store {context.VariableName(v.Target)}");
                break;
            case LoadSymbolValueInstruction<VariableDeclaration> v:
                writer.WriteLine($"load {context.VariableName(v.Target)}");
                break;

            default:
                writer.WriteLine(ToString());
                break;
        }
    }
}

public interface IJumpInstruction : IInstruction
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
    : IInstruction
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

public sealed record class ConstInstruction<TLiteral>(TLiteral Literal) : IInstruction
    where TLiteral : ILiteral
{
    public IEnumerable<VariableDeclaration> ReferencedLocalVariables => [];

    public IEnumerable<Label> ReferencedLabels => [];

    public TResult Accept<TVisitor, TResult>(TVisitor visitor)
        where TVisitor : IStructuredStackInstructionVisitor<TResult>
        => visitor.Visit(this);

    public override string ToString() => $"const.{Literal.Type.Name} {Literal}";
}

public sealed record class CallInstruction(FunctionDeclaration Callee) : IInstruction
{
    public IEnumerable<VariableDeclaration> ReferencedLocalVariables => [];

    public IEnumerable<Label> ReferencedLabels => [];

    public TResult Accept<TVisitor, TResult>(TVisitor visitor)
        where TVisitor : IStructuredStackInstructionVisitor<TResult>
        => visitor.Visit(this);

    public override string ToString() => $"call({Callee})";
}

public sealed record class LoadSymbolValueInstruction<TTarget>(TTarget Target) : IInstruction
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

public sealed record class LoadSymbolAddressInstruction<TTarget>(TTarget Target) : IInstruction
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

public sealed record class StoreSymbolInstruction<TTarget>(TTarget Target) : IInstruction
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

public sealed class LogicalNotInstruction
    : IInstruction
    , ISingleton<LogicalNotInstruction>
{
    public IEnumerable<VariableDeclaration> ReferencedLocalVariables => [];
    public IEnumerable<Label> ReferencedLabels => [];

    public static LogicalNotInstruction Instance { get; } = new();

    public TResult Accept<TVisitor, TResult>(TVisitor visitor)
        where TVisitor : IStructuredStackInstructionVisitor<TResult>
        => visitor.Visit(this);
}

public sealed record class DupInstruction : IInstruction, ISingleton<DupInstruction>
{
    public IEnumerable<VariableDeclaration> ReferencedLocalVariables => [];
    public IEnumerable<Label> ReferencedLabels => [];

    public static DupInstruction Instance { get; } = new();

    public TResult Accept<TVisitor, TResult>(TVisitor visitor)
        where TVisitor : IStructuredStackInstructionVisitor<TResult>
        => visitor.Visit(this);

    public override string ToString() => "dup";
}

public sealed record class DropInstruction : IInstruction, ISingleton<DropInstruction>
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
    public static IInstruction Nop() => new NopInstruction();
    public static IInstruction Br(Label target) => new BrInstruction(target);
    public static IInstruction BrIf(Label target) => new BrIfInstruction(target);

    public static IInstruction I32Eq() =>
        BinaryExpressionOperationInstruction<NumericBinaryRelationalOperation<IntType<N32>, BinaryRelational.Eq>>
            .Instance;

    public static IInstruction LogicalNot() => new LogicalNotInstruction();
    public static IInstruction Dup() => DupInstruction.Instance;
    public static IInstruction Pop() => DropInstruction.Instance;
    public static IInstruction AddressOf(IShaderType t) => new AddressOfInstruction(t);
    public static IInstruction Indirection(IShaderType t) => new IndirectionInstruction(t);

    public static LoadSymbolValueInstruction<ParameterDeclaration> Load(ParameterDeclaration decl) => new(decl);
    public static LoadSymbolValueInstruction<VariableDeclaration> Load(VariableDeclaration decl) => new(decl);
    public static LoadSymbolValueInstruction<MemberDeclaration> Load(MemberDeclaration decl) => new(decl);

    public static LoadSymbolAddressInstruction<ParameterDeclaration> LoadAddress(ParameterDeclaration decl) =>
        new(decl);

    public static LoadSymbolAddressInstruction<VariableDeclaration> LoadAddress(VariableDeclaration decl) => new(decl);
    public static StoreSymbolInstruction<ParameterDeclaration> Store(ParameterDeclaration decl) => new(decl);
    public static StoreSymbolInstruction<VariableDeclaration> Store(VariableDeclaration decl) => new(decl);
    public static StoreSymbolInstruction<MemberDeclaration> Store(MemberDeclaration decl) => new(decl);

    public static IInstruction Call(FunctionDeclaration decl) => new CallInstruction(decl);

    public static IInstruction Return() => new ReturnInstruction();

    public static IInstruction Const<TLiteral>(TLiteral value)
        where TLiteral : ILiteral
        => new ConstInstruction<TLiteral>(value);
}