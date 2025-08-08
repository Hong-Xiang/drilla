using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.ShaderAttribute;

namespace DualDrill.CLSL.Language.Types;

public sealed class StructureType
    : IShaderType<StructureType>
{
    public StructureDeclaration Declaration { get; }
    public string Name => Declaration.Name;

    public StructureType(StructureDeclaration declaration)
    {
        Declaration = declaration;
        PtrType = new(() => new PtrType(this));
        ZeroConstructor = new FunctionDeclaration(
            declaration.Name,
            [],
            new FunctionReturn(this, []),
            [
                new ShaderRuntimeMethodAttribute(),
            ]
        );
    }

    public IRefType GetRefType()
    {
        throw new NotImplementedException();
    }

    private Lazy<IPtrType> PtrType { get; }

    public IPtrType GetPtrType() => PtrType.Value;

    public FunctionDeclaration ZeroConstructor { get; }
    public static StructureType Instance => throw new NotSupportedException();
}