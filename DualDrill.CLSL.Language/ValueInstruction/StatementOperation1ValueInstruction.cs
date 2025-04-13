using System.CodeDom.Compiler;
using DualDrill.CLSL.Language.FunctionBody;
using DualDrill.CLSL.Language.Operation;
using DualDrill.CLSL.Language.Types;
using DualDrill.CLSL.Language.Value;

namespace DualDrill.CLSL.Language.ValueInstruction;

public sealed record class StatementOperation1ValueInstruction<TOperation, TOperand>(
    IValue<TOperand> Operand
) : IStatementValueInstruction
    where TOperation : IUnaryStatementOperation<TOperation>
    where TOperand : IShaderType<TOperand>
{
    public IOperation Operation => TOperation.Instance;

    public void Dump(ILocalDeclarationContext context, IndentedTextWriter writer)
    {
        writer.Write(TOperation.Instance.Name);
        writer.Write("(");
        Operand.Dump(context, writer);
        writer.WriteLine(")");
    }

    public IEnumerable<IValue> ReferencedValues => [];
}