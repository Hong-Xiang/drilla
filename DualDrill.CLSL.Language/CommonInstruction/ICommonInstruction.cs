using System.CodeDom.Compiler;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.FunctionBody;
using DualDrill.CLSL.Language.LinearInstruction;
using DualDrill.CLSL.Language.Symbol;
using DualDrill.Common.CodeTextWriter;

namespace DualDrill.CLSL.Language.CommonInstruction;

interface ICommonInstruction : IInstruction
{
    IEnumerable<Label> IDeclarationUser.ReferencedLabels => [];
    IEnumerable<VariableDeclaration> IDeclarationUser.ReferencedLocalVariables => [];

    void ITextDumpable<ILocalDeclarationContext>.Dump(ILocalDeclarationContext context, IndentedTextWriter writer)
    {
        writer.WriteLine(ToString());
    }
}

interface ITermintorCommonInstruction
    : ICommonInstruction,
      ITerminatorStackInstruction
{
}