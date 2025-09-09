using DualDrill.CLSL.Language.Operation;
using DualDrill.CLSL.Language.Types;

namespace DualDrill.CLSL.Language.ShaderAttribute.Metadata;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class CompositeConstructorMethodAttribute
    : Attribute
    , IZeroArgumentNewLikeShaderMetadataAttribute
    , IShaderOperationMethodAttribute
{
    public IOperation GetOperation(IShaderType resultType, IEnumerable<IShaderType> parameterTypes)
    {
        throw new NotImplementedException();
    }
}

