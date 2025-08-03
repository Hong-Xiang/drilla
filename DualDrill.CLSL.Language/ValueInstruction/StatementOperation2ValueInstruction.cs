using System.CodeDom.Compiler;
using DualDrill.CLSL.Language.FunctionBody;
using DualDrill.CLSL.Language.Operation;
using DualDrill.CLSL.Language.Symbol;
using DualDrill.CLSL.Language.Types;

namespace DualDrill.CLSL.Language.ValueInstruction;

public sealed record class StatementOperation2ValueInstruction<TOperation, TOperand>(
    IValue<TOperand> Operand1,
    IValue<TOperand> Operand2
) : IStatementValueInstruction
    where TOperation : IUnaryStatementOperation<TOperation>
    where TOperand : IShaderType<TOperand>
{
    public IOperation Operation => TOperation.Instance;

    public void Dump(ILocalDeclarationContext context, IndentedTextWriter writer)
    {
        writer.Write(TOperation.Instance.Name);
        writer.Write("(");
        Operand1.Dump(context, writer);
        writer.Write(",");
        Operand2.Dump(context, writer);
        writer.WriteLine(")");
    }

    public IEnumerable<IValue> ReferencedValues => [];
}