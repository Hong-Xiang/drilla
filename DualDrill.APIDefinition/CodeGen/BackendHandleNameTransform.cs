using DualDrill.ApiGen.DrillLang.Declaration;
using System.Collections.Immutable;

namespace DualDrill.ApiGen.CodeGen;

public sealed class BackendHandleNameTransform(
    ModuleDeclaration Module
) : INameTransform
{
    ImmutableHashSet<string> HandleNames = [.. Module.Handles.Select(h => h.Name)];

    string? INameTransform.TypeReferenceName(string name)
    {
        return HandleNames.Contains(name) ? name + "<TBackend>" : name;
    }
}
