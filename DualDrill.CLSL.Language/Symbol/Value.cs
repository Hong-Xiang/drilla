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
}

public sealed class LiteralValue(ILiteral value) : ShaderValue
{
    public ILiteral Value => value;
    public override IShaderType Type => value.Type;
    public override void Dump(ILocalDeclarationContext context, IndentedTextWriter writer)
    {
        writer.Write(Value);
    }
}




public sealed class BindShaderValue(IShaderType type, string? name) : IShaderValue
{
    public string? Name => name;
    public IShaderType Type { get; } = type;

    public void Dump(ILocalDeclarationContext context, IndentedTextWriter writer)
    {
        writer.Write($"%{context.ValueIndex(this)}");
        if (name is not null) writer.Write($"({name})");
    }

    public static BindShaderValue Create(IShaderType type) => new(type, null);

    public static BindShaderValue Create(IShaderType type, string name) => new(type, name);
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
