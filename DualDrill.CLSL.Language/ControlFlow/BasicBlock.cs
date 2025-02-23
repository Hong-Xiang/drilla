using System.CodeDom.Compiler;
using DualDrill.CLSL.Language.LinearInstruction;
using System.Collections.Immutable;
using DualDrill.CLSL.Language.AbstractSyntaxTree.Expression;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.Common;
using DualDrill.Common.CodeTextWriter;

namespace DualDrill.CLSL.Language.ControlFlow;

public interface IBasicBlock<TElement, TResult, TTransfer>
    : ITextDumpable<ILocalDeclarationContext>
{
    public ImmutableArray<TElement> Elements { get; }
    public TResult? Result { get; }
    public ImmutableStack<TTransfer> Inputs { get; }
    public ImmutableStack<TTransfer> Outputs { get; }
}

public sealed record class BasicBlock<TElement>(
    ImmutableArray<TElement> Elements,
    ImmutableStack<VariableDeclaration> Inputs,
    ImmutableStack<VariableDeclaration> Outputs
)
    : IBasicBlock<TElement, IExpression, VariableDeclaration>
    where TElement : ILocalDeclarationReferencingElement
{
    public IExpression? Result { get; } = null;

    public static BasicBlock<TElement> Create(IEnumerable<TElement> instructions)
    {
        return new([..instructions], [], []);
    }

    public void Dump(ILocalDeclarationContext context, IndentedTextWriter writer)
    {
        foreach (var e in Elements)
        {
            e.Dump(context, writer);
        }
    }
}