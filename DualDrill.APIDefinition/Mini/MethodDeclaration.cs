using System.Collections.Immutable;

namespace DualDrill.ApiGen.Mini;

public sealed record class MethodDeclaration(
    string Name,
    ImmutableArray<ParameterDeclaration> Parameters,
    ITypeReference ReturnType,
    bool IsStatic = false) : IDeclaration
{
    public bool IsAsync => ReturnType is FutureTypeRef;
}
