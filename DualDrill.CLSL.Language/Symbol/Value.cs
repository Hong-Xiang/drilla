using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.FunctionBody;
using DualDrill.CLSL.Language.Types;
using DualDrill.Common.CodeTextWriter;
using System.CodeDom.Compiler;

namespace DualDrill.CLSL.Language.Symbol;

public interface IShaderValue : ITextDumpable<ILocalDeclarationContext>
{
    IShaderType Type { get; }
}

public sealed class ShaderValue(IShaderType type, string? name) : IShaderValue
{
    public IShaderType Type { get; } = type;
    public static ShaderValue Create(IShaderType type) => new(type, null);
    public static ShaderValue Create(IShaderType type, string name) => new(type, name);

    public void Dump(ILocalDeclarationContext context, IndentedTextWriter writer)
    {
        writer.Write($"%{context.ValueIndex(this)}");
        if (name is not null)
        {
            writer.Write($"({name})");
        }
    }
    public string? Name => name;
}

public sealed class StoragePointerValue(
    VariableDeclaration declaration
) : IShaderValue
{
    public IShaderType Type => declaration.Type.GetPtrType();

    public void Dump(ILocalDeclarationContext context, IndentedTextWriter writer)
    {
        throw new NotImplementedException();
    }
    public VariableDeclaration VariableDeclaration => declaration;
}
