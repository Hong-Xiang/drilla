using DualDrill.CLSL.Language.Types;
using System.Collections.Immutable;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.LinearInstruction;

namespace DualDrill.CLSL.Language.AbstractSyntaxTree.Expression;

public enum SwizzleComponent
{
    x,
    y,
    z,
    w,
    r,
    g,
    b,
    a,
}

public sealed record class VectorSwizzleAccessExpression(IExpression Base, ImmutableArray<SwizzleComponent> Components)
    : IExpression
{
    public IShaderType Type => ((IVecType)Base.Type).ElementType;

    public TResult Accept<TResult>(IExpressionVisitor<TResult> visitor)
        => visitor.VisitVectorSwizzleAccessExpression(this);

    public IEnumerable<IStructuredStackInstruction> ToInstructions()
    {
        throw new NotImplementedException();
    }

    public IEnumerable<VariableDeclaration> ReferencedVariables => Base.ReferencedVariables;
}