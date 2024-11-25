using DualDrill.CLSL.Language.Types;

namespace DualDrill.CLSL.Language;

public interface ITargetLanguage<TType>
{
    TType GetType(IShaderType type);
}
