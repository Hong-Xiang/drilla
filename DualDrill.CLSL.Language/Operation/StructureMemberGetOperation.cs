using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.ShaderAttribute;
using DualDrill.CLSL.Language.Types;

namespace DualDrill.CLSL.Language.Operation;

public sealed class StructureMemberGetOperation(StructureType owner, MemberDeclaration member)
    : IUnaryExpressionOperation
{
    public StructureType Owner { get; } = owner;
    public MemberDeclaration Member { get; } = member;
    public IShaderType SourceType => Owner;
    public IShaderType ResultType => Member.Type;
    public string Name => $"get.{Owner.Name}.{Member.Name}";
    public FunctionDeclaration Function => throw new NotSupportedException("Structure member reads are IR-only.");
    public IOperationMethodAttribute GetOperationMethodAttribute() =>
        throw new NotSupportedException("Structure member reads are IR-only.");
    public TR Evaluate<TX, TR>(IUnaryExpressionOperationSemantic<TX, TR> semantic, TX context) =>
        throw new NotSupportedException("Structure member reads do not have intrinsic dispatch.");
}
