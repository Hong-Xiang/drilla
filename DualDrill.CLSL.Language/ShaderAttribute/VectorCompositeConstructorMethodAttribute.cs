using DualDrill.CLSL.Language.Operation;
using DualDrill.CLSL.Language.Types;

namespace DualDrill.CLSL.Language.ShaderAttribute;

[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
public class VectorCompositeConstructorMethodAttribute
    : Attribute
    , IShaderOperationMethodAttribute
{
    public IOperation GetOperation(IShaderType resultType, IEnumerable<IShaderType> parameterTypes)
    {
        if (resultType is IVecType v)
        {
            return VectorCompositeConstructionOperation.Get(v, parameterTypes);
        }
        else
        {
            throw new ArgumentException($"resultType must be a vector type, got {resultType.Name}", nameof(resultType));
        }
    }
}
