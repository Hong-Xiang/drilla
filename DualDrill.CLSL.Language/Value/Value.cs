using System.CodeDom.Compiler;
using DualDrill.CLSL.Language.FunctionBody;
using DualDrill.CLSL.Language.LinearInstruction;
using DualDrill.CLSL.Language.Operation;
using DualDrill.CLSL.Language.Types;
using DualDrill.CLSL.Language.ValueInstruction;
using DualDrill.Common.CodeTextWriter;

namespace DualDrill.CLSL.Language.Value;

public interface IValue : ITextDumpable<ILocalDeclarationContext>
{
    IShaderType Type { get; }

    IReturnResultValueInstruction GetReturnResultValueInstruction();
}

public interface IValue<TValueType> : IValue
    where TValueType : IShaderType<TValueType>
{
    IShaderType IValue.Type => TValueType.Instance;

    IReturnResultValueInstruction IValue.GetReturnResultValueInstruction()
        => ValueInstructionFactory.ReturnResult(this);
}

public interface IBlockArgumentValue : IValue
{
}

public sealed class BlockArgumentValue<TValueType> : IBlockArgumentValue, IValue<TValueType>
    where TValueType : IShaderType<TValueType>
{
    public void Dump(ILocalDeclarationContext context, IndentedTextWriter writer)
    {
        writer.Write($"%{context.ValueIndex(this)} : {TValueType.Instance.Name}");
    }
}

public interface IOperationValue : IValue
{
}

public sealed class OperationValue<TValueType> : IOperationValue, IValue<TValueType>
    where TValueType : IShaderType<TValueType>
{
    public void Dump(ILocalDeclarationContext context, IndentedTextWriter writer)
    {
        writer.Write($"%{context.ValueIndex(this)} : {TValueType.Instance.Name}");
    }
}

public static class OperationValue
{
    public static OperationValue<TShaderType> Create<TShaderType>()
        where TShaderType : IShaderType<TShaderType>
        => new();
}