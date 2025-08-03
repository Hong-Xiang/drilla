using DualDrill.CLSL.Language.FunctionBody;
using DualDrill.CLSL.Language.Types;
using DualDrill.CLSL.Language.ValueInstruction;
using DualDrill.Common.CodeTextWriter;
using System.CodeDom.Compiler;

namespace DualDrill.CLSL.Language.Symbol;

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

public sealed class ShaderValue(string? name)
{
    public ShaderValue() : this(null)
    {
    }

    public static ShaderValue Create() => new();
    public static ShaderValue Create(string name) => new(name);
    public string? Name => name;
}
