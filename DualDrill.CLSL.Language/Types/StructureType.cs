using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.ShaderAttribute;
using DualDrill.CLSL.Language.Symbol;

namespace DualDrill.CLSL.Language.Types;

public sealed class StructureType
    : IShaderType<StructureType>
{
    private readonly Dictionary<IAddressSpace, IPtrType> PtrTypes = [];

    public StructureType(StructureDeclaration declaration)
    {
        Declaration = declaration;
        ZeroConstructor = new FunctionDeclaration(
            declaration.Name,
            [],
            new FunctionReturn(this, []),
            [
                new ShaderRuntimeMethodAttribute()
            ]
        );
    }

    public StructureDeclaration Declaration { get; }

    public FunctionDeclaration ZeroConstructor { get; }
    public string Name => Declaration.Name;

    public IRefType GetRefType() => throw new NotImplementedException();

    public IPtrType GetPtrType(IAddressSpace addressSpace)
    {
        lock (PtrTypes)
        {
            if (PtrTypes.TryGetValue(addressSpace, out var ptr)) return ptr;

            var p = new PtrType(this, addressSpace);
            PtrTypes.Add(addressSpace, p);
            return p;
        }
    }

    public T Evaluate<T>(IShaderTypeSemantic<T, T> semantic) => throw new NotImplementedException();

    public static StructureType Instance => throw new NotSupportedException();
}