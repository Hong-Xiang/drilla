using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.Expression;
using DualDrill.CLSL.Language.Statement;
using DualDrill.CLSL.Language.Symbol;
using DualDrill.CLSL.Language.Types;
using DualDrill.Common;
using System.Collections.Immutable;

namespace DualDrill.CLSL.Language.FunctionBody;

public sealed record class UnstructuredUntypedStackFunctionBody(
    Label Entry,
    IReadOnlyDictionary<Label, Seq<
        IStatement<
            Unit,
            IExpression<Unit>,
            IShaderSymbol<IMemoryLocationDeclaration>,
            IShaderSymbol<FunctionDeclaration>>,
        ITerminator<Label, Unit>>> BasicBlocks)
{
}

public sealed record class UnstructuredTypedStackFunctionBody(
    Label Entry,
    IReadOnlyDictionary<Label, UnstructuredTypedStackFunctionBody.Instruction> BasicBlocks)
{
    public readonly record struct Instruction(
        IStatement<Unit,
            IExpression<Unit>,
            IShaderSymbol<IMemoryLocationDeclaration>,
            IShaderSymbol<FunctionDeclaration>> Statement,
        ImmutableStack<IShaderType> Output
    )
    {
    }
    public readonly record struct Terminator(
        ITerminator<Label, Unit> Instruction,
        ImmutableStack<IShaderType> Output
    )
    {
    }
    public readonly record struct BasicBlock(
        ImmutableStack<IShaderType> Parameters,
        Seq<Instruction, Terminator> Body
    )
    {
    }
}
