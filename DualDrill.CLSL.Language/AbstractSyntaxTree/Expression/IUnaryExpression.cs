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

public interface IUnaryExpression : IExpression
{
    public IExpression Source { get; }
    public IUnaryExpressionOperation Operation { get; }
}

public sealed record class UnaryOperationExpression<TOperation>
    : IUnaryExpression
    where TOperation : IUnaryExpressionOperation<TOperation>
{
    public UnaryOperationExpression(IExpression source)
    {
        // Debug.Assert(source.Type.Equals(TOperation.Instance.SourceType));
        Source = source;
    }

    public IExpression Source { get; }

    public IUnaryExpressionOperation Operation => TOperation.Instance;
    public IShaderType Type => TOperation.Instance.ResultType;

    public TResult Accept<TResult>(IExpressionVisitor<TResult> visitor)
        => TOperation.Instance.EvaluateExpression(visitor, this);

    public IEnumerable<IInstruction> ToInstructions()
        => [..Source.ToInstructions(), ((IOperation)TOperation.Instance).Instruction];

    public IEnumerable<VariableDeclaration> ReferencedVariables => Source.ReferencedVariables;

    public void Dump(ILocalDeclarationContext context, IndentedTextWriter writer)
    {
        writer.WriteLine(TOperation.Instance.Name);
        using (writer.IndentedScope())
        {
            Source.Dump(context, writer);
        }
    }
}