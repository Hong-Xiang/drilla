using DualDrill.CLSL.Language.Literal;
using DualDrill.CLSL.Language.Types;
using DualDrill.CLSL.Language.Value;

namespace DualDrill.CLSL.Language.ValueInstruction;

public static class ValueInstructionFactory
{
    public static ConstInstruction<TShaderType, TLiteral> Const<TShaderType, TLiteral>(
        OperationValue<TShaderType> value,
        TLiteral literal)
        where TShaderType : IShaderType<TShaderType>
        where TLiteral : ILiteral<TShaderType>
        => new(value, literal);

    public static ReturnResultValueInstruction<TShaderType> ReturnResult<TShaderType>(IValue<TShaderType> value)
        where TShaderType : IShaderType<TShaderType>
        => new(value);
}