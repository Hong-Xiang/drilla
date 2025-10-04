using System.CodeDom.Compiler;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.FunctionBody;
using DualDrill.CLSL.Language.Types;

namespace DualDrill.CLSL.Language.Symbol;

public class VariablePointerValue : IShaderValue
{
    internal VariablePointerValue(VariableDeclaration declaration)
    {
        Declaration = declaration;
    }

    public VariableDeclaration Declaration { get; }
    public IShaderType Type => Declaration.Type.GetPtrType(Declaration.AddressSpace);

    public void Dump(ILocalDeclarationContext context, IndentedTextWriter writer)
    {
        throw new NotImplementedException();
    }
}