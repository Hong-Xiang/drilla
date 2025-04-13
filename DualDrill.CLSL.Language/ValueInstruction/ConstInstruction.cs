using System.CodeDom.Compiler;
using DualDrill.CLSL.Language.CommonInstruction;
using DualDrill.CLSL.Language.FunctionBody;
using DualDrill.CLSL.Language.Literal;
using DualDrill.CLSL.Language.Types;
using DualDrill.CLSL.Language.Value;
using DualDrill.Common.CodeTextWriter;

namespace DualDrill.CLSL.Language.ValueInstruction;

public sealed record class ConstInstruction<TShaderType, TLiteral>(IOperationValue Result, TLiteral Literal)
    : IExpressionValueInstruction
    where TLiteral : ILiteral<TShaderType>
    where TShaderType : IShaderType<TShaderType>
{
    public override string ToString() => $"{Result} = const.{Literal.Type.Name} {Literal}";
    public IOperationValue ResultValue => Result;

    void ITextDumpable<ILocalDeclarationContext>.Dump(ILocalDeclarationContext context, IndentedTextWriter writer)
    {
        writer.WriteLine($"{context.ValueName(Result)} = const.{Literal.Type.Name} {Literal}");
    }
}