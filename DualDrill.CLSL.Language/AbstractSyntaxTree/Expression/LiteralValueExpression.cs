using System.CodeDom.Compiler;
using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.LinearInstruction;
using DualDrill.CLSL.Language.Literal;
using DualDrill.CLSL.Language.Types;

namespace DualDrill.CLSL.Language.AbstractSyntaxTree.Expression;

public sealed record class LiteralValueExpression(ILiteral Literal) : IExpression
{
    public IShaderType Type => Literal.Type;

    public TResult Accept<TResult>(IExpressionVisitor<TResult> visitor)
        => visitor.VisitLiteralValueExpression(this);

    public IEnumerable<IInstruction> ToInstructions()
        => [ShaderInstruction.Const(Literal)];

    public IEnumerable<VariableDeclaration> ReferencedVariables => [];

    public void Dump(ILocalDeclarationContext context, IndentedTextWriter writer)
    {
        writer.WriteLine($"literal {Literal}");
    }
}