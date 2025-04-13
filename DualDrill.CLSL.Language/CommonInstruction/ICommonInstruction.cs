using System.CodeDom.Compiler;
using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.FunctionBody;
using DualDrill.CLSL.Language.LinearInstruction;
using DualDrill.CLSL.Language.ValueInstruction;
using DualDrill.Common.CodeTextWriter;

namespace DualDrill.CLSL.Language.CommonInstruction;

interface ICommonInstruction : IInstruction, IValueInstruction
{
    IEnumerable<Label> ILocalDeclarationReferencingElement.ReferencedLabels => [];
    IEnumerable<VariableDeclaration> ILocalDeclarationReferencingElement.ReferencedLocalVariables => [];

    void ITextDumpable<ILocalDeclarationContext>.Dump(ILocalDeclarationContext context, IndentedTextWriter writer)
    {
        writer.WriteLine(ToString());
    }
}

interface ITermintorCommonInstruction
    : ICommonInstruction,
      ITerminatorStackInstruction,
      ITerminatorValueInstruction
{
}