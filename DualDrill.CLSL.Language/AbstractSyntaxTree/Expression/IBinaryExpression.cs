using System.CodeDom.Compiler;
using System.Diagnostics;
using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.FunctionBody;
using DualDrill.CLSL.Language.LinearInstruction;
using DualDrill.CLSL.Language.Operation;
using DualDrill.CLSL.Language.Types;
using DualDrill.Common.CodeTextWriter;

namespace DualDrill.CLSL.Language.AbstractSyntaxTree.Expression;

public interface IBinaryExpression : IExpression
{
    public IExpression L { get; }
    public IExpression R { get; }
    public IBinaryExpressionOperation Operation { get; }
}

public sealed record class BinaryOperationExpression<TOperation>
    : IBinaryExpression
    where TOperation : IBinaryExpressionOperation<TOperation>
{
    public BinaryOperationExpression(IExpression left, IExpression right)
    {
        var opLt = TOperation.Instance.LeftType;
        var opRt = TOperation.Instance.RightType;
        //Debug.Assert(left.Type.Equals(opLt));
        //Debug.Assert(right.Type.Equals(opRt));
        L = left;
        R = right;
    }

    public IExpression L { get; }
    public IExpression R { get; }
    public IBinaryExpressionOperation Operation => TOperation.Instance;
    public IShaderType Type => TOperation.Instance.ResultType;

    public TResult Accept<TResult>(IExpressionVisitor<TResult> visitor)
        => visitor.VisitBinaryExpression(this);

    public IEnumerable<IInstruction> ToInstructions()
        =>
        [
            ..L.ToInstructions(),
            ..R.ToInstructions(),
            TOperation.Instance.Instruction
        ];

    public IEnumerable<VariableDeclaration> ReferencedVariables =>
    [
        ..L.ReferencedVariables,
        ..R.ReferencedVariables
    ];

    public void Dump(ILocalDeclarationContext context, IndentedTextWriter writer)
    {
        writer.WriteLine($"{TOperation.Instance.Name}");
        using (writer.IndentedScope())
        {
            L.Dump(context, writer);
            R.Dump(context, writer);
        }
    }
}