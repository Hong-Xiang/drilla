using System.CodeDom.Compiler;
using DualDrill.CLSL.Language.AbstractSyntaxTree.Expression;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.FunctionBody;
using DualDrill.CLSL.Language.LinearInstruction;
using DualDrill.CLSL.Language.Symbol;
using DualDrill.Common.CodeTextWriter;

namespace DualDrill.CLSL.Language.AbstractSyntaxTree.Statement;

public sealed record class PushStatement(IExpression Expr) : IStackStatement
{
    public IEnumerable<Label> ReferencedLabels => [];
    public IEnumerable<VariableDeclaration> ReferencedLocalVariables => Expr.ReferencedVariables;
    public IEnumerable<IShaderValue> ReferencedValues => [];

    public IEnumerable<IInstruction> ToInstructions()
        => Expr.ToInstructions();

    public void Dump(ILocalDeclarationContext context, IndentedTextWriter writer)
    {
        writer.WriteLine("push");
        using (writer.IndentedScope())
        {
            Expr.Dump(context, writer);
        }
    }
}