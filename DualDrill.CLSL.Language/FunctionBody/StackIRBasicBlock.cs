using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.Symbol;
using DualDrill.CLSL.Language.Types;
using DualDrill.Common;
using DualDrill.Common.CodeTextWriter;
using System.CodeDom.Compiler;
using System.Collections.Immutable;

namespace DualDrill.CLSL.Language.FunctionBody;

public sealed record class StackIRBasicBlock(
    Label Label,
    ITerminator<Label, Unit> Terminator,
    ImmutableStack<IShaderType> Inputs,
    ImmutableStack<IShaderType> Outputs,
    ImmutableArray<StackIRInstruction> Instructions
    ) : IBasicBlock2
{
    public ISuccessor Successor => Terminator.ToSuccessor();

    public IEnumerable<VariableDeclaration> ReferencedLocalVariables => [];

    public IEnumerable<IValue> ReferencedValues => [];

    public void Dump(ILocalDeclarationContext context, IndentedTextWriter writer)
    {
        Label.Dump(context, writer);
        writer.WriteLine(":");


        using (writer.IndentedScope())
        {
            foreach (var stmt in Instructions)
            {
                stmt.Dump(context, writer);
            }
            var t = Terminator.Select(context.LabelName2, _ => string.Empty);
            writer.WriteLine(t);
        }
    }
}

