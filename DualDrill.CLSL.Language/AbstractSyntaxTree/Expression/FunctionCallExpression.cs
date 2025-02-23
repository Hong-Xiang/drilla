using System.CodeDom.Compiler;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.Types;
using System.Collections.Immutable;
using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.LinearInstruction;
using DualDrill.Common.CodeTextWriter;

namespace DualDrill.CLSL.Language.AbstractSyntaxTree.Expression;

public sealed record class FunctionCallExpression(
    FunctionDeclaration Callee,
    ImmutableArray<IExpression> Arguments)
    : IExpression
{
    public IShaderType Type => Callee.Return.Type;

    public TResult Accept<TResult>(IExpressionVisitor<TResult> visitor)
        => visitor.VisitFunctionCallExpression(this);

    public IEnumerable<IInstruction> ToInstructions()
        =>
        [
            .. Arguments.SelectMany(e => e.ToInstructions()),
            ShaderInstruction.Call(Callee)
        ];

    public IEnumerable<VariableDeclaration> ReferencedVariables
        => Arguments.SelectMany(e => e.ReferencedVariables);

    public bool Equals(FunctionCallExpression? other)
    {
        if (other is null)
        {
            return false;
        }

        return Callee.Equals(other.Callee) && Arguments.SequenceEqual(other.Arguments);
    }

    public void Dump(ILocalDeclarationContext context, IndentedTextWriter writer)
    {
        writer.WriteLine($"call {Callee.Name}");
        using (writer.IndentedScope())
        {
            foreach (var arg in Arguments)
            {
                arg.Dump(context, writer);
            }
        }
    }
}