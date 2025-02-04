using DualDrill.CLSL.Language.Operation;
using DualDrill.Common;

namespace DualDrill.CLSL.Language.ShaderAttribute;

public interface IOperationMethodAttribute : IZeroArgumentNewLikeShaderMetadataAttribute
{
    IOperation Operation { get; }
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Method)]
public sealed class OperationMethodAttribute<TOperation>
    : Attribute, IOperationMethodAttribute
    where TOperation : IOperation<TOperation>
{
    public IOperation Operation => TOperation.Instance;
}
