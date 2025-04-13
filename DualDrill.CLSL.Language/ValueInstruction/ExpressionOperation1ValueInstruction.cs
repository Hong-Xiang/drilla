using System.CodeDom.Compiler;
using DualDrill.CLSL.Language.FunctionBody;
using DualDrill.CLSL.Language.Operation;
using DualDrill.CLSL.Language.Types;
using DualDrill.CLSL.Language.Value;

namespace DualDrill.CLSL.Language.ValueInstruction;

public sealed record class ExpressionOperation1ValueInstruction<TOperation, TOperand, TResult>(
    OperationValue<TResult> Result,
    IValue<TOperand> Operand
) : IExpressionValueInstruction
    where TOperation : IUnaryExpressionOperation<TOperation>
    where TOperand : IShaderType<TOperand>
    where TResult : IShaderType<TResult>
{
    public IOperationValue ResultValue => Result;
    public IOperation Operation => TOperation.Instance;

    public void Dump(ILocalDeclarationContext context, IndentedTextWriter writer)
    {
        Result.Dump(context, writer);
        writer.Write(" = ");
        writer.Write(TOperation.Instance.Name);
        writer.Write("(");
        Operand.Dump(context, writer);
        writer.WriteLine(")");
    }
}