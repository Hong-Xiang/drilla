using DualDrill.CLSL.Language.Types;

namespace DualDrill.CLSL.Language.Declaration;

public interface IMemoryLocationDeclaration
{
    IShaderType Type { get; }
}
