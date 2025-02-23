using System.CodeDom.Compiler;
using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.LinearInstruction;
using DualDrill.CLSL.Language.Types;

namespace DualDrill.CLSL.Language.AbstractSyntaxTree.Expression;

public sealed record class VectorAccessExpression(IExpression Base, IExpression Index) : IExpression
{
    public IShaderType Type => ((IVecType)Base.Type).ElementType;

    public TResult Accept<TResult>(IExpressionVisitor<TResult> visitor)
        => throw new NotImplementedException();

    public IEnumerable<IInstruction> ToInstructions()
    {
        throw new NotImplementedException();
    }

    public IEnumerable<VariableDeclaration> ReferencedVariables { get; }

    public void Dump(ILocalDeclarationContext context, IndentedTextWriter writer)
    {
        throw new NotImplementedException();
    }
}