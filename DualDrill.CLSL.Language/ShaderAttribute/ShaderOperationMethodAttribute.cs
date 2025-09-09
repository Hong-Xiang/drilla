using DualDrill.CLSL.Language.Operation;
using DualDrill.CLSL.Language.Types;

namespace DualDrill.CLSL.Language.ShaderAttribute;

public interface IShaderOperationMethodAttribute : IShaderAttribute
{
    IOperation GetOperation(IShaderType resultType, IEnumerable<IShaderType> parameterTypes);
}

