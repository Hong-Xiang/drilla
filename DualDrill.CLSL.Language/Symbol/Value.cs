using DualDrill.CLSL.Language.FunctionBody;
using DualDrill.CLSL.Language.Types;
using DualDrill.Common.CodeTextWriter;
using System.CodeDom.Compiler;

namespace DualDrill.CLSL.Language.Symbol;

public interface IShaderValue : ITextDumpable<ILocalDeclarationContext>
{
    IShaderType Type { get; }
}

public sealed class ShaderValue(string? name) : IShaderValue
{
    public ShaderValue() : this(null)
    {
    }

    public static ShaderValue Create() => new();
    public static ShaderValue Create(string name) => new(name);

    public void Dump(ILocalDeclarationContext context, IndentedTextWriter writer)
    {
        writer.Write($"%{context.ValueIndex(this)}");
        if (name is not null)
        {
            writer.Write($"({name})");
        }
    }

    public string? Name => name;

    public IShaderType Type => throw new NotImplementedException();
}
