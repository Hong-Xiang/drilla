using System.CodeDom.Compiler;
using DualDrill.CLSL.Language.CommonInstruction;
using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.FunctionBody;
using DualDrill.CLSL.Language.Symbol;
using DualDrill.CLSL.Language.Types;
using DualDrill.Common.CodeTextWriter;

namespace DualDrill.CLSL.Language.ValueInstruction;

public interface IReturnResultValueInstruction : IValueInstruction
{
}

public sealed record class ReturnResultValueInstruction<TShaderType>(
    IValue<TShaderType> Value
) : IReturnResultValueInstruction, ITerminatorValueInstruction
    where TShaderType : IShaderType<TShaderType>
{
    public ISuccessor ToSuccessor()
        => Successor.Terminate();

    public IEnumerable<IValue> ReferencedValues => [];

    void ITextDumpable<ILocalDeclarationContext>.Dump(ILocalDeclarationContext context, IndentedTextWriter writer)
    {
        writer.Write($"return ");
        Value.Dump(context, writer);
        writer.WriteLine();
    }
}