using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.Operation;
using DualDrill.CLSL.Language.ShaderAttribute;
using DualDrill.CLSL.Language.Symbol;

namespace DualDrill.CLSL.Language.Types;

public sealed class StructureType
    : IShaderType<StructureType>
{
    public StructureDeclaration Declaration { get; }
    public string Name => Declaration.Name;

    public StructureType(StructureDeclaration declaration)
    {
        Declaration = declaration;
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
    private Dictionary<IAddressSpace, IPtrType> PtrTypes = [];

    public IPtrType GetPtrType(IAddressSpace addressSpace)
    {
        lock (PtrTypes)
        {
            if (PtrTypes.TryGetValue(addressSpace, out var ptr))
            {
                return ptr;
            }
            else
            {
                var p = new PtrType(this, addressSpace);
                PtrTypes.Add(addressSpace, p);
                return p;
            }
        }
    }

    public T Evaluate<T>(IShaderTypeSemantic<T, T> semantic)
    {
        throw new NotImplementedException();
    }

    public FunctionDeclaration ZeroConstructor { get; }
    public static StructureType Instance => throw new NotSupportedException();
}