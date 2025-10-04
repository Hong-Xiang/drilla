using System.CodeDom.Compiler;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.FunctionBody;
using DualDrill.CLSL.Language.Literal;
using DualDrill.CLSL.Language.Types;
using DualDrill.Common.CodeTextWriter;

namespace DualDrill.CLSL.Language.Symbol;

public interface IShaderValue : ITextDumpable<ILocalDeclarationContext>
{
    IShaderType Type { get; }
}

public abstract class ShaderValue : IShaderValue
{
    public abstract IShaderType Type { get; }
    public abstract void Dump(ILocalDeclarationContext context, IndentedTextWriter writer);
    public static ShaderValue Literal(ILiteral literal) => new LiteralValue(literal);
    public static ShaderValue Intermediate(IShaderType type, string? name = null) => new IntermediateValue(type, name);
}

public sealed class IntermediateValue(IShaderType type, string? name) : ShaderValue
{
    public string? Name => name;
    public override IShaderType Type { get; } = type;

    public override void Dump(ILocalDeclarationContext context, IndentedTextWriter writer)
    {
        writer.Write($"%{context.ValueIndex(this)}");
        if (name is not null) writer.Write($"({name})");
    }
}

public sealed class StoragePointerValue(
    VariableDeclaration declaration
) : IShaderValue
{
    public VariableDeclaration VariableDeclaration => declaration;
    public IShaderType Type => declaration.Type.GetPtrType();

    public void Dump(ILocalDeclarationContext context, IndentedTextWriter writer)
    {
        throw new NotImplementedException();
    }
}