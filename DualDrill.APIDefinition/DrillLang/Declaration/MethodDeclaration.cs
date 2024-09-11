using System.Collections.Immutable;
using DualDrill.ApiGen.DrillLang.Types;

namespace DualDrill.ApiGen.DrillLang.Declaration;

public sealed record class MethodDeclaration(
    string Name,
    ImmutableArray<ParameterDeclaration> Parameters,
    ITypeReference ReturnType,
    bool IsStatic = false) : IDeclaration
{
    public bool IsAsync => ReturnType is FutureTypeReference;
}
