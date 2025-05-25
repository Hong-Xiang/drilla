using System.CodeDom.Compiler;
using System.Collections.Immutable;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.FunctionBody;
using DualDrill.Common.CodeTextWriter;

namespace DualDrill.CLSL.Language.ControlFlow;

[Obsolete("Migrate to FunctionBody.IBasicBlock2")]
public sealed record class BasicBlock<TElement>(
    ImmutableArray<TElement> Elements,
    ImmutableStack<VariableDeclaration> Inputs,
    ImmutableStack<VariableDeclaration> Outputs
)
    : ITextDumpable<ILocalDeclarationContext>
    where TElement : IDeclarationUser
{
    public static BasicBlock<TElement> Create(IEnumerable<TElement> instructions)
    {
        return new([.. instructions], [], []);
    }

    public void Dump(ILocalDeclarationContext context, IndentedTextWriter writer)
    {
        foreach (var e in Elements)
        {
            e.Dump(context, writer);
        }
    }
}