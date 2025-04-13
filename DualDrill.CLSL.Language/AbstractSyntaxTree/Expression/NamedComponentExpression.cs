using System.CodeDom.Compiler;
using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.FunctionBody;
using DualDrill.CLSL.Language.LinearInstruction;
using DualDrill.CLSL.Language.Types;
using DualDrill.Common.CodeTextWriter;

namespace DualDrill.CLSL.Language.AbstractSyntaxTree.Expression;

public sealed record class NamedComponentExpression(IExpression Base, MemberDeclaration Component) : IExpression
{
    public IShaderType Type => Component.Type;

    public TResult Accept<TResult>(IExpressionVisitor<TResult> visitor)
        => visitor.VisitNamedComponentExpression(this);

    public IEnumerable<IInstruction> ToInstructions()
        =>
        [
            ..Base.ToInstructions(),
            ShaderInstruction.Load(Component)
        ];

    public IEnumerable<VariableDeclaration> ReferencedVariables => Base.ReferencedVariables;

    public void Dump(ILocalDeclarationContext context, IndentedTextWriter writer)
    {
        writer.WriteLine($"component access <{Component.Name}>");
        using (writer.IndentedScope())
        {
            using (writer.IndentedScope())
            {
                Base.Dump(context, writer);
            }
        }
    }
}