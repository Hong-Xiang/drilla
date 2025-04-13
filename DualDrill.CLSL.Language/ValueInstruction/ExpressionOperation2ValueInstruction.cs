using System.CodeDom.Compiler;
using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.FunctionBody;
using DualDrill.CLSL.Language.Operation;
using DualDrill.CLSL.Language.Types;
using DualDrill.CLSL.Language.Value;

namespace DualDrill.CLSL.Language.ValueInstruction;

public interface IExpressionOperation2ValueInstruction
    : IExpressionOperationValueInstruction
{
}

public sealed record class ExpressionOperation2ValueInstruction<TOperation, TOperand1, TOperand2, TResult>(
    OperationValue<TResult> Result,
    IValue<TOperand1> Operand1,
    IValue<TOperand2> Operand2
) : IExpressionOperation2ValueInstruction
    where TOperation : IBinaryExpressionOperation<TOperation>
    where TOperand1 : IShaderType<TOperand1>
    where TOperand2 : IShaderType<TOperand2>
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
        Operand1.Dump(context, writer);
        writer.Write(",");
        Operand2.Dump(context, writer);
        writer.WriteLine(")");
    }
}